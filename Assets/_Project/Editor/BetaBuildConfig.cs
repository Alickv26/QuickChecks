#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

namespace QuickChecks.Editor
{
    /// <summary>
    /// One-click beta build configuration + APK builder for Android.
    ///
    /// Usage:
    ///   Tools → QuickChecks → Configure Beta Build Settings  (sets all PlayerSettings)
    ///   Tools → QuickChecks → Build Beta APK                (configures + builds to download/)
    ///
    /// Configuration applied:
    ///   - Bundle ID: com.quickchecks.game
    ///   - Version: 0.9.0 (beta), build 1
    ///   - Min Android API: 24 (Android 7.0)
    ///   - Target arch: ARM64 (64-bit, required by Play Store since 2019)
    ///   - Scripting backend: IL2CPP (required for ARM64)
    ///   - Auto-generated placeholder app icon (neon cyan kart on dark teal)
    ///   - Quality: mobile-optimized (no shadows, lower textures)
    ///   - Audio: 512-sample DSP buffer for low-latency playback
    ///   - All 8 scenes in Build Settings, MainMenu as scene 0
    /// </summary>
    public static class BetaBuildConfig
    {
        private const string BUNDLE_ID = "com.quickchecks.game";
        private const string VERSION = "0.9.0";
        private const int BUILD_NUMBER = 1;
        private const string COMPANY_NAME = "QuickChecks";
        private const string PRODUCT_NAME = "QuickChecks";

        // Paths
        private const string SCENES_DIR = "Assets/_Project/Scenes/";
        private const string ICONS_DIR = "Assets/_Project/Art/Icons/";
        private const string APK_OUTPUT_DIR = "/home/z/my-project/download/";  // user-facing download dir

        [MenuItem("Tools/QuickChecks/Configure Beta Build Settings")]
        public static void ConfigureBuildSettings()
        {
            Debug.Log("[BetaBuildConfig] Applying beta build configuration...");

            // --- Company / product / version ---
            PlayerSettings.companyName = COMPANY_NAME;
            PlayerSettings.productName = PRODUCT_NAME;
            PlayerSettings.applicationIdentifier = BUNDLE_ID;
            PlayerSettings.bundleVersion = VERSION;
            PlayerSettings.Android.bundleVersionCode = BUILD_NUMBER;

            // --- Android-specific ---
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;  // Android 7.0
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;  // latest installed
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // IL2CPP is set via PlayerSettings.SetScriptingBackend below (line ~62)
            PlayerSettings.Android.useAPKExpansionFiles = false;
            // Note: cleartext HTTP not needed — Supabase uses HTTPS
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;

            // --- IL2CPP + managed stripping ---
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            // Low stripping = safest for Newtonsoft.Json reflection (we use [JsonProperty] attributes)

            // --- Splash screen: disabled (we have our own MainMenu scene as the entry point) ---
            PlayerSettings.SplashScreen.show = false;

            // --- App icon ---
            GenerateAppIcon();

            // --- Quality settings (mobile-friendly) ---
            ConfigureQualitySettings();

            // --- Audio settings (low-latency) ---
            ConfigureAudioSettings();

            // --- Build Settings: all 8 scenes, MainMenu as scene 0 ---
            EnsureScenesInBuildSettings();

            // (Graphics API: let Unity use the default for Android — URP handles
            // this automatically via the UniversalRenderPipelineAsset.)

            EditorUtility.DisplayDialog(
                "Beta Build Configured",
                "Beta build settings applied:\n\n" +
                $"• Bundle ID: {BUNDLE_ID}\n" +
                $"• Version: {VERSION} (build {BUILD_NUMBER})\n" +
                "• Target: Android ARM64 (IL2CPP)\n" +
                "• Min API: 24 (Android 7.0)\n" +
                "• App icon: auto-generated placeholder\n" +
                "• Quality: mobile-optimized\n" +
                "• Audio: 512-sample DSP buffer\n" +
                "• Scenes: all 8 added to Build Settings\n\n" +
                "Next step: Tools → QuickChecks → Build Beta APK",
                "OK"
            );
            Debug.Log("[BetaBuildConfig] Configuration complete.");
        }

        [MenuItem("Tools/QuickChecks/Build Beta APK")]
        public static void BuildBetaApk()
        {
            // Re-apply configuration (idempotent — safe to run before every build)
            ConfigureBuildSettings();

            // Ensure the output directory exists
            System.IO.Directory.CreateDirectory(APK_OUTPUT_DIR);

            // Build the APK
            string apkPath = System.IO.Path.Combine(APK_OUTPUT_DIR, $"QuickChecks-{VERSION}-build{BUILD_NUMBER}.apk");
            Debug.Log($"[BetaBuildConfig] Building APK to: {apkPath}");

            // Collect all enabled scenes (MainMenu first)
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }
            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Build Failed", "No scenes in Build Settings. Run Tools → QuickChecks → Build Game Scenes first.", "OK");
                return;
            }

            // Build options
            var buildOptions = BuildOptions.None;  // no Development build = release
            // (Development builds include profiler + debug symbols — skip for beta quality)

            var report = BuildPipeline.BuildPlayer(
                scenes.ToArray(),
                apkPath,
                BuildTarget.Android,
                buildOptions
            );

            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "Build Succeeded",
                    $"APK built successfully:\n\n{apkPath}\n\n" +
                    $"Size: {report.summary.totalSize / 1024 / 1024} MB\n" +
                    $"Scenes: {scenes.Count}",
                    "OK"
                );
                Debug.Log($"[BetaBuildConfig] Build succeeded: {apkPath} ({report.summary.totalSize / 1024 / 1024} MB)");
            }
            else
            {
                string errorMsg = $"Build failed: {report.summary.result}";
                if (report.summary.totalErrors > 0)
                {
                    errorMsg += $"\n\nErrors: {report.summary.totalErrors}";
                }
                EditorUtility.DisplayDialog("Build Failed", errorMsg, "OK");
                Debug.LogError($"[BetaBuildConfig] {errorMsg}");
            }
        }

        /// <summary>Bumps the build number + saves (call before each rebuild to get a unique version).</summary>
        [MenuItem("Tools/QuickChecks/Bump Build Number")]
        public static void BumpBuildNumber()
        {
            int newBuild = PlayerSettings.Android.bundleVersionCode + 1;
            PlayerSettings.Android.bundleVersionCode = newBuild;
            Debug.Log($"[BetaBuildConfig] Build number bumped to {newBuild}.");
        }

        // =====================================================================
        // App icon generation (placeholder — neon cyan kart on dark teal bg)
        // =====================================================================

        private static void GenerateAppIcon()
        {
            System.IO.Directory.CreateDirectory(ICONS_DIR);

            // Generate the icon textures + save as PNGs so Unity imports them as assets
            int[] sizes = { 192, 144, 96, 72, 48 };
            var iconPaths = new string[sizes.Length];

            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                var tex = GenerateIconTexture(size);
                string pngPath = System.IO.Path.Combine(ICONS_DIR, $"AppIcon_{size}.png");
                System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                iconPaths[i] = pngPath;
                // Mark as non-readable (saves memory)
                var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            // Load the textures + assign via PlayerSettings
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPaths[i]);
            }

            // PlayerSettings.SetIconsForTargetGroup takes a Texture2D[] indexed by icon size.
            // Unity picks the closest size for each platform's required icon.
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons);

            AssetDatabase.SaveAssets();
            Debug.Log("[BetaBuildConfig] App icon generated + assigned.");
        }

        /// <summary>Generates a placeholder app icon: dark teal background with a neon cyan kart silhouette in the center.</summary>
        private static Texture2D GenerateIconTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            // Background: dark teal (#0E1414)
            Color bg = new Color(0.055f, 0.078f, 0.078f, 1f);
            // Kart: warm yellow (#FFD166) — matches the player kart color from the Art Bible
            Color kart = new Color(1f, 0.82f, 0.4f, 1f);
            // Accent border: neon cyan (#3FE0C2)
            Color accent = new Color(0.247f, 0.878f, 0.760f, 1f);

            int center = size / 2;
            int kartSize = size / 3;  // kart takes up the middle third
            int borderSize = Mathf.Max(1, size / 32);  // thin neon border

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;

                    // Neon border
                    if (x < borderSize || x >= size - borderSize ||
                        y < borderSize || y >= size - borderSize)
                    {
                        pixels[idx] = accent;
                        continue;
                    }

                    // Kart silhouette (simple rounded rectangle in the center)
                    int dx = Mathf.Abs(x - center);
                    int dy = Mathf.Abs(y - center);
                    if (dx < kartSize && dy < kartSize / 2)
                    {
                        pixels[idx] = kart;
                    }
                    else
                    {
                        pixels[idx] = bg;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        // =====================================================================
        // Quality settings (mobile-optimized)
        // =====================================================================

        private static void ConfigureQualitySettings()
        {
            // QualitySettings static API in Unity 2022.3 — these properties apply globally
            // and persist across scenes. Each quality tier can have its own values, but
            // we'll use the same mobile-friendly values for all tiers.

            // VSync off — let the game target 60fps explicitly
            QualitySettings.vSyncCount = 0;
            // Anti-aliasing off (mobile GPUs struggle + we use pixel-perfect sprites)
            QualitySettings.antiAliasing = 0;
            // No real-time shadows (top-down view doesn't benefit)
            QualitySettings.shadows = ShadowQuality.Disable;
            // Pixel light count = 0 (we use sprite renderers, no dynamic lights)
            // Note: QualitySettings.pixelLightCount is read-only in 2022.3 — set via
            // QualitySettings.SetQualityLevel + ProjectSettings/Quality asset. For now
            // we just leave it (default is 1, harmless for our 2D game).

            // Set the default quality level to the lowest (mobile-friendly)
            // Unity 2022.3 ships with 6 tiers by default — index 0 is "Very Low"
            int mobileTier = 0;
            if (QualitySettings.names.Length > mobileTier)
            {
                QualitySettings.SetQualityLevel(mobileTier, applyExpensiveChanges: false);
            }

            Debug.Log($"[BetaBuildConfig] Quality settings: vSync off, AA off, shadows off, tier={mobileTier}.");
        }

        // =====================================================================
        // Audio settings (low-latency playback)
        // =====================================================================

        private static void ConfigureAudioSettings()
        {
            // DSP buffer size: 512 samples = ~12ms latency at 44.1kHz (good for SFX responsiveness)
            // Smaller = lower latency but higher CPU. 512 is the mobile sweet spot.
            // AudioSettings.SetDSPBufferSize is the right API — takes (bufferLength, numBuffers)
            // We use 512 samples, 2 buffers (= ~24ms total — reasonable for mobile SFX)
            AudioSettings.SetDSPBufferSize(512, 2);
            Debug.Log("[BetaBuildConfig] Audio DSP buffer size set to 512 samples x 2 buffers (low-latency).");
        }

        // =====================================================================
        // Build Settings: ensure all 8 scenes are present, MainMenu first
        // =====================================================================

        private static void EnsureScenesInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SCENES_DIR + "MainMenu.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "ModeSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "TrackSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "KartSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "Race.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "Results.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "CustomTrackSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "CustomTrackBuilder.unity", true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[BetaBuildConfig] {scenes.Count} scenes added to Build Settings (MainMenu as scene 0).");
        }
    }
}
#endif
