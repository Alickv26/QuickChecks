#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuickChecks.Editor
{
    /// <summary>
    /// Automated QA checklist for beta release. Runs a battery of checks against
    /// tracks, scenes, scripts, karts, power-ups, Supabase config, and custom tracks.
    /// Outputs a full report to the console + displays a summary dialog.
    ///
    /// Usage: Tools → QuickChecks → Run QA Checklist
    /// </summary>
    public static class QAChecklist
    {
        private static readonly List<string> _passed = new();
        private static readonly List<string> _failed = new();
        private static readonly List<string> _warnings = new();

        [MenuItem("Tools/QuickChecks/Run QA Checklist")]
        public static void RunAllChecks()
        {
            _passed.Clear();
            _failed.Clear();
            _warnings.Clear();

            Debug.Log("[QA] ================================================");
            Debug.Log("[QA] QuickChecks Beta QA Checklist — starting...");
            Debug.Log("[QA] ================================================");

            CheckTracksGenerate();
            CheckScenesInBuildSettings();
            CheckScenesHaveNoMissingScripts();
            CheckKartStatsAssets();
            CheckKartLibraryReferences();
            CheckPowerUpFactories();
            CheckSupabaseConfig();
            CheckCustomTrackLibrary();
            CheckAudioAssets();

            // Final report
            Debug.Log("[QA] ================================================");
            Debug.Log($"[QA] PASSED:  {_passed.Count}");
            Debug.Log($"[QA] WARNINGS: {_warnings.Count}");
            Debug.Log($"[QA] FAILED: {_failed.Count}");
            Debug.Log("[QA] ================================================");

            string status = _failed.Count == 0
                ? "READY FOR BETA" + (_warnings.Count > 0 ? $" (with {_warnings.Count} warnings)" : " (clean)")
                : $"NOT READY — {_failed.Count} failures";

            string report = $"QA Checklist Results:\n\n" +
                            $"✓ Passed:   {_passed.Count}\n" +
                            $"⚠ Warnings: {_warnings.Count}\n" +
                            $"✗ Failed:   {_failed.Count}\n\n" +
                            $"Status: {status}\n\n";

            if (_failed.Count > 0)
            {
                report += "FAILURES:\n";
                foreach (var f in _failed) report += $"  ✗ {f}\n";
            }
            if (_warnings.Count > 0)
            {
                report += "\nWARNINGS:\n";
                foreach (var w in _warnings) report += $"  ⚠ {w}\n";
            }

            EditorUtility.DisplayDialog("QA Checklist", report, "OK");
        }

        // =====================================================================
        // Check 1: All 8 tracks generate without exceptions
        // =====================================================================

        private static void CheckTracksGenerate()
        {
            Debug.Log("[QA] Check 1: Track generation...");
            try
            {
                var tracks = Track.TrackFactory.GenerateAllTracks();
                if (tracks == null || tracks.Length == 0)
                {
                    Fail("TrackFactory.GenerateAllTracks() returned null or empty");
                    return;
                }
                if (tracks.Length != 8)
                {
                    Fail($"Expected 8 tracks, got {tracks.Length}");
                    return;
                }
                for (int i = 0; i < tracks.Length; i++)
                {
                    var t = tracks[i];
                    if (t == null) { Fail($"Track {i + 1} is null"); continue; }
                    if (string.IsNullOrEmpty(t.trackId)) Fail($"Track {i + 1} ({t.displayName}) has empty trackId");
                    if (string.IsNullOrEmpty(t.displayName)) Fail($"Track {i + 1} ({t.trackId}) has empty displayName");
                    if (t.splinePoints == null || t.splinePoints.Length < 4)
                        Fail($"Track {i + 1} ({t.displayName}) has < 4 spline points");
                    if (t.parSwipeCount <= 0) Warn($"Track {i + 1} ({t.displayName}) has parSwipeCount={t.parSwipeCount}");
                    if (t.trackIndex != i) Warn($"Track {i + 1} ({t.displayName}) has trackIndex={t.trackIndex}, expected {i}");
                    // Tracks 1-3 should be free, 4-8 paid
                    bool expectedFree = i < 3;
                    if (t.isFree != expectedFree)
                        Warn($"Track {i + 1} ({t.displayName}) isFree={t.isFree}, expected {expectedFree}");
                }
                Pass($"All 8 tracks generate successfully with valid fields");
            }
            catch (System.Exception ex)
            {
                Fail($"TrackFactory.GenerateAllTracks() threw: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 2: All 8 scenes are in Build Settings + paths exist
        // =====================================================================

        private static void CheckScenesInBuildSettings()
        {
            Debug.Log("[QA] Check 2: Scenes in Build Settings...");
            string[] expectedScenes = {
                "MainMenu", "ModeSelect", "TrackSelect", "KartSelect",
                "Race", "Results", "CustomTrackSelect", "CustomTrackBuilder"
            };

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length == 0)
            {
                Fail("Build Settings has 0 scenes. Run Tools → QuickChecks → Build Game Scenes first.");
                return;
            }

            foreach (var sceneName in expectedScenes)
            {
                string path = $"Assets/_Project/Scenes/{sceneName}.unity";
                bool inBuild = buildScenes.Any(s => s.path == path && s.enabled);
                if (!inBuild)
                {
                    Fail($"Scene '{sceneName}.unity' not found in Build Settings (or disabled)");
                }
                if (!File.Exists(path))
                {
                    Fail($"Scene file doesn't exist on disk: {path}");
                }
            }

            // Verify MainMenu is scene 0 (entry point)
            if (buildScenes[0].path != "Assets/_Project/Scenes/MainMenu.unity")
            {
                Fail($"Scene 0 should be MainMenu.unity, got: {buildScenes[0].path}");
            }

            Pass($"All 8 scenes present in Build Settings, MainMenu is scene 0");
        }

        // =====================================================================
        // Check 3: Scenes have no missing script references
        // =====================================================================

        private static void CheckScenesHaveNoMissingScripts()
        {
            Debug.Log("[QA] Check 3: Scene missing script references...");
            string[] expectedScenes = {
                "MainMenu", "ModeSelect", "TrackSelect", "KartSelect",
                "Race", "Results", "CustomTrackSelect", "CustomTrackBuilder"
            };
            int totalMissing = 0;
            foreach (var sceneName in expectedScenes)
            {
                string path = $"Assets/_Project/Scenes/{sceneName}.unity";
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var allMonoBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in allMonoBehaviours)
                {
                    // A MonoBehaviour with a null instance ID but non-null GameObject = missing script
                    if (mb == null || (mb is MonoBehaviour mono && mono == null))
                    {
                        totalMissing++;
                        Warn($"Missing script reference in scene '{sceneName}'");
                    }
                }
                EditorSceneManager.CloseScene(scene, true);
            }
            if (totalMissing == 0)
                Pass("No missing script references in any scene");
            else
                Fail($"Found {totalMissing} missing script references across scenes");
        }

        // =====================================================================
        // Check 4: All 8 KartStats assets load + have valid fields
        // =====================================================================

        private static void CheckKartStatsAssets()
        {
            Debug.Log("[QA] Check 4: KartStats assets...");
            string[] kartNames = {
                "Kart_Starter", "Kart_Drifter", "Kart_Precision", "Kart_Speedster",
                "Kart_Bulldozer", "Kart_Phantom", "Kart_Vector", "Kart_Apex"
            };

            foreach (var name in kartNames)
            {
                string path = $"Assets/_Project/ScriptableObjects/Karts/{name}.asset";
                var kart = AssetDatabase.LoadAssetAtPath<Racing.KartStats>(path);
                if (kart == null)
                {
                    Fail($"KartStats asset not found: {path}");
                    continue;
                }
                if (string.IsNullOrEmpty(kart.kartId)) Fail($"{name}: empty kartId");
                if (string.IsNullOrEmpty(kart.displayName)) Fail($"{name}: empty displayName");
                if (string.IsNullOrEmpty(kart.archetypeLabel)) Warn($"{name}: empty archetypeLabel");
                if (string.IsNullOrEmpty(kart.description)) Warn($"{name}: empty description");
                if (kart.impulseMultiplier < 0.5f || kart.impulseMultiplier > 2f)
                    Fail($"{name}: impulseMultiplier {kart.impulseMultiplier} out of range [0.5, 2]");
                if (kart.maxSpeed < 100f || kart.maxSpeed > 500f)
                    Fail($"{name}: maxSpeed {kart.maxSpeed} out of range [100, 500]");
            }
            Pass("All 8 KartStats assets load with valid fields");
        }

        // =====================================================================
        // Check 5: KartLibrary.asset references all 8 karts
        // =====================================================================

        private static void CheckKartLibraryReferences()
        {
            Debug.Log("[QA] Check 5: KartLibrary references...");
            string libPath = "Assets/_Project/ScriptableObjects/Karts/KartLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<Racing.KartLibrary>(libPath);
            if (lib == null)
            {
                Fail($"KartLibrary.asset not found at {libPath}");
                return;
            }
            if (lib.karts == null || lib.karts.Length != 8)
            {
                Fail($"KartLibrary has {lib.karts?.Length ?? 0} karts, expected 8");
                return;
            }
            for (int i = 0; i < lib.karts.Length; i++)
            {
                if (lib.karts[i] == null)
                {
                    Fail($"KartLibrary.karts[{i}] is null");
                }
            }
            Pass("KartLibrary.asset references all 8 karts");
        }

        // =====================================================================
        // Check 6: Power-up factory methods return non-null + have displayName
        // =====================================================================

        private static void CheckPowerUpFactories()
        {
            Debug.Log("[QA] Check 6: Power-up factories...");
            var factoryMethods = new System.Func<Racing.PowerUpBase>[] {
                Track.TrackFactory.CreateBoostPowerUp,
                Track.TrackFactory.CreateShieldPowerUp,
                Track.TrackFactory.CreateMagnetPowerUp,
                Track.TrackFactory.CreateDoubleMovePowerUp,
                Track.TrackFactory.CreateTrailWallPowerUp,
                Track.TrackFactory.CreateBouncePowerUp,
                Track.TrackFactory.CreateSlingshotPowerUp,
                Track.TrackFactory.CreatePhaseDodgePowerUp,
                Track.TrackFactory.CreateRewindPowerUp,
            };

            foreach (var method in factoryMethods)
            {
                try
                {
                    var pu = method();
                    if (pu == null) Fail($"{method.Method.Name} returned null");
                    else if (string.IsNullOrEmpty(pu.displayName)) Warn($"{method.Method.Name}: empty displayName");
                    else if (string.IsNullOrEmpty(pu.effectDescription)) Warn($"{method.Method.Name}: empty effectDescription");
                }
                catch (System.Exception ex)
                {
                    Fail($"{method.Method.Name} threw: {ex.Message}");
                }
            }
            Pass("All 9 power-up factory methods return valid instances with displayName");
        }

        // =====================================================================
        // Check 7: Supabase config (URL + publishable key)
        // =====================================================================

        private static void CheckSupabaseConfig()
        {
            Debug.Log("[QA] Check 7: Supabase config...");
            // Check the SupabaseClient config (the source of truth, since it spawns the RpcClient)
            var supabaseGo = new GameObject("[QACheck_SupabaseClient]");
            var client = supabaseGo.AddComponent<Multiplayer.SupabaseClient>();

            // Use reflection to read the private serialized fields
            var urlField = typeof(Multiplayer.SupabaseClient).GetField("supabaseUrl",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keyField = typeof(Multiplayer.SupabaseClient).GetField("publishableKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            string url = urlField?.GetValue(client) as string ?? "";
            string key = keyField?.GetValue(client) as string ?? "";

            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://"))
                Fail($"Supabase URL invalid: '{url}'");
            else if (!url.Contains(".supabase.co"))
                Warn($"Supabase URL doesn't look like a Supabase project URL: '{url}'");

            if (string.IsNullOrEmpty(key) || !key.StartsWith("sb_publishable_"))
                Fail($"Supabase publishable key invalid: '{key?.Substring(0, System.Math.Min(20, key?.Length ?? 0))}...'");
            else
                Pass($"Supabase config valid (URL: {url}, key prefix: {key.Substring(0, 15)}...)");

            // Cleanup
            Object.DestroyImmediate(supabaseGo);
        }

        // =====================================================================
        // Check 8: CustomTrackLibrary save/load round-trip
        // =====================================================================

        private static void CheckCustomTrackLibrary()
        {
            Debug.Log("[QA] Check 8: CustomTrackLibrary round-trip...");
            try
            {
                // Create a test track
                var testTrack = new Track.CustomTrackData
                {
                    trackId = "qa_test_track",
                    trackName = "QA Test Track",
                    splinePoints = new List<Track.CustomTrackData.SerializableVector2>
                    {
                        new(-5, 0), new(0, 5), new(5, 0), new(0, -5)
                    },
                    obstacles = new List<Track.CustomTrackData.PlacedObstacle>
                    {
                        new() { type = "Wall", position = new(0, 0), size = new(2, 2) }
                    },
                    powerUps = new List<Track.CustomTrackData.PlacedPowerUp>
                    {
                        new() { type = "Boost", position = new(2, 0) }
                    }
                };

                // Save + reload
                Track.CustomTrackLibrary.Save(testTrack);
                var loaded = Track.CustomTrackLibrary.FindById("qa_test_track");

                if (loaded == null) Fail("CustomTrackLibrary.FindById returned null after Save");
                else if (loaded.trackName != "QA Test Track") Fail("Round-trip trackName mismatch");
                else if (loaded.splinePoints.Count != 4) Fail("Round-trip splinePoints count mismatch");
                else if (loaded.obstacles.Count != 1) Fail("Round-trip obstacles count mismatch");
                else if (loaded.powerUps.Count != 1) Fail("Round-trip powerUps count mismatch");
                else Pass("CustomTrackLibrary save/load round-trip works");

                // Test ToTrackDefinition conversion
                var def = Track.CustomTrackLibrary.ToTrackDefinition(loaded);
                if (def == null) Fail("ToTrackDefinition returned null");
                else if (def.obstacleSpawns == null || def.obstacleSpawns.Length != 1)
                    Fail("ToTrackDefinition obstacleSpawns mismatch");
                else if (def.powerUpSpawns == null || def.powerUpSpawns.Length != 1)
                    Fail("ToTrackDefinition powerUpSpawns mismatch");
                else if (def.powerUpSpawns[0].powerUp == null)
                    Fail("ToTrackDefinition powerUp is null (CreatePowerUpFromType failed?)");
                else Pass("CustomTrackLibrary.ToTrackDefinition conversion works");

                // Cleanup: delete the test track
                Track.CustomTrackLibrary.Delete("qa_test_track");
            }
            catch (System.Exception ex)
            {
                Fail($"CustomTrackLibrary round-trip threw: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 9: Audio assets (track.mp3 exists)
        // =====================================================================

        private static void CheckAudioAssets()
        {
            Debug.Log("[QA] Check 9: Audio assets...");
            string musicPath = "Assets/_Project/Audio/Music/track.mp3";
            if (File.Exists(musicPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
                if (clip != null)
                    Pass($"Music track.mp3 loaded ({clip.length:F1}s)");
                else
                    Warn($"track.mp3 exists but couldn't load as AudioClip: {musicPath}");
            }
            else
            {
                Warn($"Music track.mp3 not found at {musicPath} — game will run without music");
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static void Pass(string msg)
        {
            _passed.Add(msg);
            Debug.Log($"[QA] ✓ {msg}");
        }

        private static void Fail(string msg)
        {
            _failed.Add(msg);
            Debug.LogError($"[QA] ✗ {msg}");
        }

        private static void Warn(string msg)
        {
            _warnings.Add(msg);
            Debug.LogWarning($"[QA] ⚠ {msg}");
        }
    }
}
#endif
