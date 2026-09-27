#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using QuickChecks.Prototype;

namespace QuickChecks.Editor
{
    /// <summary>
    /// One-click scene builder for the Week 1 prototype.
    /// Creates Assets/_Project/Scenes/Prototype.unity with a single GameObject
    /// that has PrototypeBootstrapper attached. The bootstrapper builds the
    /// rest of the scene at runtime (in Awake), so the saved scene file stays tiny.
    ///
    /// Usage: menu Tools > QuickChecks > Build Prototype Scene
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string SCENE_PATH = "Assets/_Project/Scenes/Prototype.unity";
        private const string INPUT_SETTINGS_PATH = "Assets/_Project/ScriptableObjects/Settings/InputSettings.asset";
        private const string SWIPE_EVENT_PATH = "Assets/_Project/ScriptableObjects/Events/SwipeEvent.asset";
        private const string GHOST_SWIPE_EVENT_PATH = "Assets/_Project/ScriptableObjects/Events/GhostSwipeEvent.asset";
        private const string KART_STATS_PATH = "Assets/_Project/ScriptableObjects/Karts/Kart_Starter.asset";
        private const string GHOST_KART_STATS_PATH = "Assets/_Project/ScriptableObjects/Karts/Kart_Ghost.asset";

        [MenuItem("Tools/QuickChecks/Build Prototype Scene")]
        public static void BuildScene()
        {
            // Ensure Scenes folder exists.
            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");

            // Create ScriptableObject assets so the bootstrapper can wire them up persistently.
            // Note: must use concrete non-generic subclasses (SwipeEvent, GhostSwipeEvent)
            // because Unity's CreateInstance<T>() cannot instantiate generic ScriptableObject
            // types like GameEventSO<SwipeData> — it returns null, which then causes
            // AssetDatabase.CreateAsset to throw ArgumentNullException.
            var inputSettings = EnsureAsset<QuickChecks.Input.InputSettings>(INPUT_SETTINGS_PATH);
            var swipeEvent = EnsureAsset<QuickChecks.Core.SwipeEvent>(SWIPE_EVENT_PATH);
            var ghostSwipeEvent = EnsureAsset<QuickChecks.Core.GhostSwipeEvent>(GHOST_SWIPE_EVENT_PATH);
            var kartStats = EnsureKartStats(KART_STATS_PATH, "kart_starter", "Starter");
            var ghostKartStats = EnsureKartStats(GHOST_KART_STATS_PATH, "kart_ghost", "Ghost");

            // CRITICAL: Force-update tuning values on existing assets.
            // Without this, old asset files with stale values (e.g., maxImpulseMagnitude=1500
            // from a previous build) will persist and the kart will move too fast.
            ForceUpdateInputSettings(inputSettings);
            ForceUpdateKartStats(kartStats, "kart_starter", "Starter");
            ForceUpdateKartStats(ghostKartStats, "kart_ghost", "Ghost");

            // Abort if any asset failed to create — better than continuing with nulls.
            if (inputSettings == null || swipeEvent == null || ghostSwipeEvent == null ||
                kartStats == null || ghostKartStats == null)
            {
                EditorUtility.DisplayDialog(
                    "Prototype Scene Build Failed",
                    "One or more ScriptableObject assets could not be created.\n\n" +
                    "Check the Console (Window > General > Console) for the specific error.\n\n" +
                    "Common causes:\n" +
                    "  • Compile errors in the project (fix red errors first)\n" +
                    "  • Asset import still in progress (wait for import to finish, retry)\n" +
                    "  • Permission issues in Assets/_Project folder",
                    "OK"
                );
                return;
            }

            // Create new empty scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Add PrototypeBootstrapper GameObject.
            var go = new GameObject("PrototypeBootstrapper");
            var bootstrapper = go.AddComponent<PrototypeBootstrapper>();

            // Wire up the [SerializeField] private fields via SerializedObject.
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("inputSettings").objectReferenceValue = inputSettings;
            so.FindProperty("swipeEvent").objectReferenceValue = swipeEvent;
            so.FindProperty("ghostSwipeEvent").objectReferenceValue = ghostSwipeEvent;
            so.FindProperty("kartStats").objectReferenceValue = kartStats;
            so.FindProperty("ghostKartStats").objectReferenceValue = ghostKartStats;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save the scene.
            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            EditorUtility.DisplayDialog(
                "Prototype Scene Built",
                $"Prototype scene created at:\n{SCENE_PATH}\n\n" +
                "Polished prototype features:\n" +
                "  • 4 obstacles forcing direction variety\n" +
                "  • Kart squash + particle burst on each swipe\n" +
                "  • Fading trail showing your swipe path\n" +
                "  • Synthesized swipe whoosh (pitched by velocity)\n" +
                "  • Solo ghost replay (best run alongside current attempt)\n" +
                "  • Race results with par + best-run comparison\n\n" +
                "Open it and press Play to test the swipe feel.",
                "OK"
            );

            // Auto-open the scene for the user.
            EditorSceneManager.OpenScene(SCENE_PATH);
            Selection.activeGameObject = go;

            // Frame the scene view on the new GameObject (Frame is an instance method).
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(go.transform.position, Vector3.one * 30), false);
            }
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            if (asset == null)
            {
                Debug.LogError($"[PrototypeSceneBuilder] Failed to create instance of {typeof(T).Name}. " +
                               $"If {typeof(T).Name} is a generic type (e.g., GameEventSO<SwipeData>), " +
                               "Unity cannot instantiate it via CreateInstance<T>(). " +
                               "Create a concrete non-generic subclass instead.");
                return null;
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// Forces the InputSettings asset to have the current prototype tuning values,
        /// even if the asset already exists with old values. Run after EnsureAsset.
        /// </summary>
        private static void ForceUpdateInputSettings(QuickChecks.Input.InputSettings settings)
        {
            if (settings == null) return;
            settings.minSwipeVelocity = 1000f;
            settings.maxSwipeDurationMs = 220f;
            settings.minSwipeDistance = 50f;
            settings.velocityToImpulseScale = 0.4f;
            settings.maxImpulseMagnitude = 250f;
            settings.minCameraZoom = 5f;
            settings.maxCameraZoom = 20f;
            settings.pinchSensitivity = 0.15f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[PrototypeSceneBuilder] Force-updated InputSettings with current tuning values.");
        }

        /// <summary>
        /// Forces the KartStats asset to have the current prototype tuning values,
        /// even if the asset already exists with old values.
        /// </summary>
        private static void ForceUpdateKartStats(QuickChecks.Racing.KartStats stats, string kartId, string displayName)
        {
            if (stats == null) return;
            stats.kartId = kartId;
            stats.displayName = displayName;
            stats.impulseMultiplier = 1.1f;
            stats.frictionPerSecond = 0.22f;
            stats.stopThreshold = 8f;
            stats.maxSpeed = 250f;
            stats.boostMultiplier = 1.8f;
            stats.boostDurationSec = 1.5f;
            stats.unlockAfterTrackIndex = -1;
            EditorUtility.SetDirty(stats);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PrototypeSceneBuilder] Force-updated KartStats ({kartId}) with current tuning values.");
        }

        private static QuickChecks.Racing.KartStats EnsureKartStats(string path, string kartId, string displayName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<QuickChecks.Racing.KartStats>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<QuickChecks.Racing.KartStats>();
            asset.kartId = kartId;
            asset.displayName = displayName;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
#endif
