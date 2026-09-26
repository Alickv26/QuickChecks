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
        private const string KART_STATS_PATH = "Assets/_Project/ScriptableObjects/Karts/Kart_Starter.asset";

        [MenuItem("Tools/QuickChecks/Build Prototype Scene")]
        public static void BuildScene()
        {
            // Ensure Scenes folder exists.
            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");

            // Create ScriptableObject assets so the bootstrapper can wire them up persistently.
            var inputSettings = EnsureAsset<QuickChecks.Input.InputSettings>(INPUT_SETTINGS_PATH);
            var swipeEvent = EnsureAsset<QuickChecks.Core.GameEventSO<QuickChecks.Input.SwipeData>>(SWIPE_EVENT_PATH);
            var kartStats = EnsureKartStats(KART_STATS_PATH);

            // Create new empty scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Add PrototypeBootstrapper GameObject.
            var go = new GameObject("PrototypeBootstrapper");
            var bootstrapper = go.AddComponent<PrototypeBootstrapper>();

            // Wire up the [SerializeField] private fields via SerializedObject.
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("inputSettings").objectReferenceValue = inputSettings;
            so.FindProperty("swipeEvent").objectReferenceValue = swipeEvent;
            so.FindProperty("kartStats").objectReferenceValue = kartStats;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save the scene.
            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            EditorUtility.DisplayDialog(
                "Prototype Scene Built",
                $"Prototype scene created at:\n{SCENE_PATH}\n\n" +
                "Open it and press Play to test the swipe feel.\n\n" +
                "Check the console for swipe logs and tuning data.",
                "OK"
            );

            // Auto-open the scene for the user.
            EditorSceneManager.OpenScene(SCENE_PATH);
            Selection.activeGameObject = go;
            SceneView.Frame(new Bounds(go.transform.position, Vector3.one * 30), false);
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static QuickChecks.Racing.KartStats EnsureKartStats(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<QuickChecks.Racing.KartStats>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<QuickChecks.Racing.KartStats>();
            asset.kartId = "kart_starter";
            asset.displayName = "Starter";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
#endif
