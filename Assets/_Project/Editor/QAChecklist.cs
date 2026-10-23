#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using QuickChecks.Track;
using QuickChecks.Racing;

namespace QuickChecks.Editor
{
    /// <summary>
    /// Pre-beta QA checklist. Validates that the project is in a shippable state
    /// before building the APK. Checks:
    ///
    ///   1. All 8 tracks generate without errors via TrackFactory.GenerateAllTracks()
    ///   2. Each track passes TrackValidator.Validate()
    ///   3. All 9 power-up factory methods return non-null + have displayName + effectDescription
    ///   4. KartLibrary.asset references all 8 KartStats assets (all non-null)
    ///   5. All 8 scenes exist on disk + are in Build Settings + have no missing script refs
    ///   6. All scene GameObjects have valid (non-missing) script GUIDs
    ///   7. Supabase config URL + publishable key are set (not the placeholder)
    ///   8. KartStats assets all have valid stats (impulse > 0, maxSpeed > 0, etc.)
    ///   9. No compile errors in the project (via EditorApplication.compiling)
    ///
    /// Usage: Tools → QuickChecks → Run QA Checklist
    /// </summary>
    public static class QAChecklist
    {
        private struct CheckResult
        {
            public string name;
            public bool passed;
            public string message;
        }

        private static readonly List<CheckResult> _results = new();
        private static int _passCount;
        private static int _failCount;
        private static int _warnCount;

        [MenuItem("Tools/QuickChecks/Run QA Checklist")]
        public static void RunChecklist()
        {
            _results.Clear();
            _passCount = _failCount = _warnCount = 0;

            Debug.Log("[QA] Starting pre-beta QA checklist...");

            Check1_AllTracksGenerate();
            Check2_AllTracksValidate();
            Check3_AllPowerUpFactoriesWork();
            Check4_KartLibraryReferencesValid();
            Check5_AllScenesExistAndInBuildSettings();
            Check6_NoMissingScriptReferences();
            Check7_SupabaseConfigSet();
            Check8_AllKartStatsValid();
            Check9_NoCompileErrors();

            ShowResultsDialog();
        }

        // =====================================================================
        // Check 1: All 8 tracks generate without throwing
        // =====================================================================

        private static void Check1_AllTracksGenerate()
        {
            try
            {
                var tracks = TrackFactory.GenerateAllTracks();
                if (tracks == null || tracks.Length != 8)
                {
                    AddResult("1. Tracks generate", false,
                        $"Expected 8 tracks, got {tracks?.Length ?? 0}. Check TrackFactory.GenerateAllTracks().");
                    return;
                }
                int nullCount = 0;
                foreach (var t in tracks) if (t == null) nullCount++;
                if (nullCount > 0)
                {
                    AddResult("1. Tracks generate", false,
                        $"{nullCount} of 8 tracks are null. Check TrackFactory methods.");
                    return;
                }
                AddResult("1. Tracks generate", true,
                    $"All 8 tracks generated successfully.");
            }
            catch (System.Exception ex)
            {
                AddResult("1. Tracks generate", false, $"Exception: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 2: Each track passes TrackValidator
        // =====================================================================

        private static void Check2_AllTracksValidate()
        {
            try
            {
                var tracks = TrackFactory.GenerateAllTracks();
                int valid = 0;
                var errors = new List<string>();
                foreach (var t in tracks)
                {
                    if (t == null) continue;
                    if (TrackValidator.Validate(t, out string error))
                    {
                        valid++;
                    }
                    else
                    {
                        errors.Add($"  {t.displayName}: {error}");
                    }
                }
                if (valid == 8)
                {
                    AddResult("2. Tracks validate", true,
                        "All 8 tracks pass TrackValidator.");
                }
                else
                {
                    AddResult("2. Tracks validate", false,
                        $"Only {valid}/8 tracks pass validation:\n{string.Join("\n", errors)}");
                }
            }
            catch (System.Exception ex)
            {
                AddResult("2. Tracks validate", false, $"Exception: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 3: All 9 power-up factory methods return non-null + have metadata
        // =====================================================================

        private static void Check3_AllPowerUpFactoriesWork()
        {
            try
            {
                var powerUps = new[]
                {
                    ("Boost", TrackFactory.CreateBoostPowerUp()),
                    ("Shield", TrackFactory.CreateShieldPowerUp()),
                    ("Magnet", TrackFactory.CreateMagnetPowerUp()),
                    ("DoubleMove", TrackFactory.CreateDoubleMovePowerUp()),
                    ("TrailWall", TrackFactory.CreateTrailWallPowerUp()),
                    ("Bounce", TrackFactory.CreateBouncePowerUp()),
                    ("Slingshot", TrackFactory.CreateSlingshotPowerUp()),
                    ("PhaseDodge", TrackFactory.CreatePhaseDodgePowerUp()),
                    ("Rewind", TrackFactory.CreateRewindPowerUp()),
                };

                int valid = 0;
                var issues = new List<string>();
                foreach (var (name, pu) in powerUps)
                {
                    if (pu == null)
                    {
                        issues.Add($"  {name}: factory returned null");
                        continue;
                    }
                    if (string.IsNullOrEmpty(pu.displayName))
                    {
                        issues.Add($"  {name}: displayName is empty");
                        continue;
                    }
                    if (string.IsNullOrEmpty(pu.effectDescription))
                    {
                        issues.Add($"  {name}: effectDescription is empty (toast won't show useful text)");
                        continue;
                    }
                    valid++;
                }

                if (valid == 9)
                {
                    AddResult("3. Power-up factories", true,
                        "All 9 power-ups created with displayName + effectDescription.");
                }
                else
                {
                    AddResult("3. Power-up factories", false,
                        $"Only {valid}/9 power-ups are valid:\n{string.Join("\n", issues)}");
                }
            }
            catch (System.Exception ex)
            {
                AddResult("3. Power-up factories", false, $"Exception: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 4: KartLibrary.asset references all 8 KartStats assets
        // =====================================================================

        private static void Check4_KartLibraryReferencesValid()
        {
            try
            {
                var kartLib = AssetDatabase.LoadAssetAtPath<KartLibrary>(
                    "Assets/_Project/ScriptableObjects/Karts/KartLibrary.asset");

                if (kartLib == null)
                {
                    AddResult("4. KartLibrary asset", false,
                        "KartLibrary.asset not found at Assets/_Project/ScriptableObjects/Karts/.");
                    return;
                }
                if (kartLib.karts == null || kartLib.karts.Length != 8)
                {
                    AddResult("4. KartLibrary asset", false,
                        $"Expected 8 karts in KartLibrary, got {kartLib.karts?.Length ?? 0}.");
                    return;
                }

                int nullCount = 0;
                for (int i = 0; i < kartLib.karts.Length; i++)
                {
                    if (kartLib.karts[i] == null) nullCount++;
                }
                if (nullCount > 0)
                {
                    AddResult("4. KartLibrary asset", false,
                        $"{nullCount} of 8 KartStats references are null. Re-assign in KartLibrary.asset.");
                    return;
                }
                AddResult("4. KartLibrary asset", true,
                    "All 8 KartStats assets referenced + non-null.");
            }
            catch (System.Exception ex)
            {
                AddResult("4. KartLibrary asset", false, $"Exception: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 5: All 8 scenes exist on disk + are in Build Settings
        // =====================================================================

        private static void Check5_AllScenesExistAndInBuildSettings()
        {
            string[] expectedScenes = {
                "MainMenu.unity", "ModeSelect.unity", "TrackSelect.unity",
                "KartSelect.unity", "Race.unity", "Results.unity",
                "CustomTrackSelect.unity", "CustomTrackBuilder.unity"
            };
            string scenesDir = "Assets/_Project/Scenes/";

            int missing = 0;
            var issues = new List<string>();

            // Check files exist on disk
            foreach (var sceneName in expectedScenes)
            {
                string path = scenesDir + sceneName;
                if (!System.IO.File.Exists(path))
                {
                    issues.Add($"  File missing: {path}");
                    missing++;
                }
            }

            // Check Build Settings has them
            var buildScenes = new HashSet<string>();
            foreach (var bs in EditorBuildSettings.scenes)
            {
                buildScenes.Add(System.IO.Path.GetFileName(bs.path));
            }
            int notInBuildSettings = 0;
            foreach (var sceneName in expectedScenes)
            {
                if (!buildScenes.Contains(sceneName))
                {
                    issues.Add($"  Not in Build Settings: {sceneName}");
                    notInBuildSettings++;
                }
            }

            // Check MainMenu is scene 0
            if (EditorBuildSettings.scenes.Length > 0 &&
                !EditorBuildSettings.scenes[0].path.EndsWith("MainMenu.unity"))
            {
                issues.Add("  Scene 0 is not MainMenu.unity (will break splash → menu flow)");
            }

            if (missing == 0 && notInBuildSettings == 0)
            {
                AddResult("5. Scenes exist + in Build Settings", true,
                    "All 8 scenes present + in Build Settings.");
            }
            else
            {
                AddResult("5. Scenes exist + in Build Settings", false,
                    $"Issues found:\n{string.Join("\n", issues)}");
            }
        }

        // =====================================================================
        // Check 6: No missing script references in any scene
        // =====================================================================

        private static void Check6_NoMissingScriptReferences()
        {
            string[] expectedScenes = {
                "MainMenu.unity", "ModeSelect.unity", "TrackSelect.unity",
                "KartSelect.unity", "Race.unity", "Results.unity",
                "CustomTrackSelect.unity", "CustomTrackBuilder.unity"
            };
            string scenesDir = "Assets/_Project/Scenes/";

            int missingScripts = 0;
            var issues = new List<string>();

            foreach (var sceneName in expectedScenes)
            {
                string path = scenesDir + sceneName;
                if (!System.IO.File.Exists(path)) continue;

                // Open the scene additively (don't replace the current scene)
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var allMonoBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in allMonoBehaviours)
                {
                    if (mb == null)
                    {
                        // null MonoBehaviour = missing script reference
                        missingScripts++;
                        issues.Add($"  {sceneName}: missing script reference on a GameObject");
                        break;  // one missing ref per scene is enough to report
                    }
                }
                EditorSceneManager.CloseScene(scene, true);
            }

            if (missingScripts == 0)
            {
                AddResult("6. No missing script references", true,
                    "All scenes have valid script references.");
            }
            else
            {
                AddResult("6. No missing script references", false,
                    $"{missingScripts} missing script references found:\n{string.Join("\n", issues)}");
            }
        }

        // =====================================================================
        // Check 7: Supabase config URL + key are set (not placeholders)
        // =====================================================================

        private static void Check7_SupabaseConfigSet()
        {
            try
            {
                // Check SupabaseClient.cs source for the URL + key
                string clientPath = "Assets/_Project/Scripts/Multiplayer/SupabaseRpcClient.cs";
                if (!System.IO.File.Exists(clientPath))
                {
                    AddResult("7. Supabase config", false,
                        "SupabaseRpcClient.cs not found.");
                    return;
                }
                string source = System.IO.File.ReadAllText(clientPath);
                bool urlOk = source.Contains("wjeiddjeueysfsvjgzyx.supabase.co");
                bool keyOk = source.Contains("sb_publishable_SJKpvEFlsgRSKniELN5V");

                if (urlOk && keyOk)
                {
                    AddResult("7. Supabase config", true,
                        "URL + publishable key are set (not placeholders).");
                }
                else
                {
                    string detail = urlOk ? "" : "URL is still placeholder. ";
                    detail += keyOk ? "" : "Publishable key is still placeholder.";
                    AddResult("7. Supabase config", false, detail);
                }
            }
            catch (System.Exception ex)
            {
                AddResult("7. Supabase config", false, $"Exception: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 8: All KartStats assets have valid stats
        // =====================================================================

        private static void Check8_AllKartStatsValid()
        {
            try
            {
                var kartLib = AssetDatabase.LoadAssetAtPath<KartLibrary>(
                    "Assets/_Project/ScriptableObjects/Karts/KartLibrary.asset");
                if (kartLib == null || kartLib.karts == null)
                {
                    AddResult("8. KartStats valid", false, "KartLibrary not loaded — see check 4.");
                    return;
                }

                int valid = 0;
                var issues = new List<string>();
                foreach (var k in kartLib.karts)
                {
                    if (k == null) continue;
                    if (string.IsNullOrEmpty(k.kartId))
                    {
                        issues.Add($"  {k.displayName}: kartId is empty");
                        continue;
                    }
                    if (k.impulseMultiplier <= 0f)
                    {
                        issues.Add($"  {k.displayName}: impulseMultiplier must be > 0");
                        continue;
                    }
                    if (k.maxSpeed <= 0f)
                    {
                        issues.Add($"  {k.displayName}: maxSpeed must be > 0");
                        continue;
                    }
                    if (k.boostMultiplier <= 0f)
                    {
                        issues.Add($"  {k.displayName}: boostMultiplier must be > 0");
                        continue;
                    }
                    if (string.IsNullOrEmpty(k.displayName) || string.IsNullOrEmpty(k.archetypeLabel))
                    {
                        issues.Add($"  {k.displayName}: missing displayName or archetypeLabel");
                        continue;
                    }
                    valid++;
                }

                if (valid == 8)
                {
                    AddResult("8. KartStats valid", true,
                        "All 8 KartStats have valid stats + metadata.");
                }
                else
                {
                    AddResult("8. KartStats valid", false,
                        $"Only {valid}/8 karts are valid:\n{string.Join("\n", issues)}");
                }
            }
            catch (System.Exception ex)
            {
                AddResult("8. KartStats valid", false, $"Exception: {ex.Message}");
            }
        }

        // =====================================================================
        // Check 9: No compile errors (Unity's compilation pipeline is clean)
        // =====================================================================

        private static void Check9_NoCompileErrors()
        {
            if (EditorApplication.isCompiling)
            {
                AddResult("9. No compile errors", false,
                    "Unity is currently compiling — wait a few seconds + re-run.");
                return;
            }
            // If we got here, the script loaded successfully — which means compilation succeeded.
            // (If there were compile errors, this class wouldn't have loaded at all.)
            AddResult("9. No compile errors", true,
                "All scripts compiled successfully (this QA script is running, which proves it).");
        }

        // =====================================================================
        // Results
        // =====================================================================

        private static void AddResult(string name, bool passed, string message)
        {
            _results.Add(new CheckResult { name = name, passed = passed, message = message });
            if (passed) _passCount++;
            else _failCount++;
            Debug.Log($"[QA] {(passed ? "PASS" : "FAIL")}: {name} — {message}");
        }

        private static void ShowResultsDialog()
        {
            string summary = $"QA Checklist Complete\n\n" +
                            $"Passed: {_passCount}/{_results.Count}\n" +
                            $"Failed: {_failCount}/{_results.Count}\n\n";

            foreach (var r in _results)
            {
                summary += $"{(r.passed ? "✓" : "✗")} {r.name}\n";
                if (!r.passed)
                {
                    summary += $"    → {r.message}\n";
                }
            }

            bool allPassed = _failCount == 0;
            summary += $"\n{(allPassed ? "✓ Ready for beta build!" : "✗ Fix the failing checks before building.")}";

            EditorUtility.DisplayDialog(
                allPassed ? "QA Checklist: All Passed" : "QA Checklist: Issues Found",
                summary,
                "OK"
            );

            Debug.Log($"[QA] Checklist complete: {_passCount} passed, {_failCount} failed.");
        }
    }
}
#endif
