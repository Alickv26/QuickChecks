#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using QuickChecks.Track;

namespace QuickChecks.Editor
{
    /// <summary>
    /// Editor tool for designers to build tracks visually in the Scene view.
    /// Accessible via Tools > QuickChecks > Track Builder.
    ///
    /// Features (planned for week 3-4):
    /// - Click in scene to add Bezier control points
    /// - Adjust track width via slider
    /// - Auto-generate boundary colliders
    /// - Auto-place checkpoints at regular intervals
    /// - Drag-drop power-up spawn points
    /// - Per-segment camera zoom hints
    /// - In-place playtest (skip main menu)
    ///
    /// v0 stub: just opens the window with a placeholder UI.
    /// </summary>
    public class TrackBuilderWindow : EditorWindow
    {
        private TrackDefinition _currentTrack;
        // _isDrawing reserved for week 3-4 scene-view click-to-add feature
        // private bool _isDrawing = false;

        [MenuItem("Tools/QuickChecks/Track Builder")]
        public static void OpenWindow()
        {
            var window = GetWindow<TrackBuilderWindow>("QuickChecks Track Builder");
            window.minSize = new Vector2(400, 600);
        }

        private void OnGUI()
        {
            GUILayout.Label("QuickChecks Track Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _currentTrack = (TrackDefinition)EditorGUILayout.ObjectField(
                "Track Definition",
                _currentTrack,
                typeof(TrackDefinition),
                false
            );

            if (_currentTrack == null)
            {
                EditorGUILayout.HelpBox(
                    "Create or select a TrackDefinition asset to start building.",
                    MessageType.Info
                );
                if (GUILayout.Button("Create New Track Definition"))
                {
                    var path = EditorUtility.SaveFilePanelInProject(
                        "New Track Definition",
                        "NewTrack",
                        "asset",
                        "Save the new track definition asset"
                    );
                    if (!string.IsNullOrEmpty(path))
                    {
                        var asset = ScriptableObject.CreateInstance<TrackDefinition>();
                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();
                        _currentTrack = asset;
                        Selection.activeObject = asset;
                    }
                }
                return;
            }

            // Track metadata
            _currentTrack.trackId = EditorGUILayout.TextField("Track ID", _currentTrack.trackId);
            _currentTrack.displayName = EditorGUILayout.TextField("Display Name", _currentTrack.displayName);
            _currentTrack.difficultyStars = EditorGUILayout.IntSlider("Difficulty", _currentTrack.difficultyStars, 1, 5);
            _currentTrack.trackWidth = EditorGUILayout.Slider("Track Width", _currentTrack.trackWidth, 1f, 12f);
            _currentTrack.lapCount = EditorGUILayout.IntSlider("Lap Count", _currentTrack.lapCount, 1, 5);

            EditorGUILayout.Space();

            // Spline points (v0: list display, v1: scene-view click to add)
            GUILayout.Label("Spline Points: " + (_currentTrack.splinePoints?.Length ?? 0));
            if (GUILayout.Button("Add Point at Scene Center"))
            {
                var points = _currentTrack.splinePoints;
                System.Array.Resize(ref points, (points?.Length ?? 0) + 1);
                points[points.Length - 1] = new Vector2(
                    SceneView.lastActiveSceneView.camera.transform.position.x,
                    SceneView.lastActiveSceneView.camera.transform.position.y
                );
                _currentTrack.splinePoints = points;
                EditorUtility.SetDirty(_currentTrack);
            }

            if (GUILayout.Button("Clear All Points"))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Points",
                    "Remove all spline points from this track?",
                    "Clear", "Cancel"))
                {
                    _currentTrack.splinePoints = new Vector2[0];
                    EditorUtility.SetDirty(_currentTrack);
                }
            }

            EditorGUILayout.Space();

            // Validation
            if (GUILayout.Button("Validate Track"))
            {
                if (_currentTrack.Validate(out string error))
                {
                    EditorUtility.DisplayDialog("Validation", "Track is valid!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Failed", error, "OK");
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Week 3-4 roadmap: scene-view click to add points, " +
                "auto-generate boundaries, drag-drop power-up spawns, " +
                "per-segment zoom hints, in-place playtest.",
                MessageType.Info
            );
        }
    }
}
#endif
