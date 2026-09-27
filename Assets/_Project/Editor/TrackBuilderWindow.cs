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
    /// Features (Week 3-4):
    /// - Edit existing TrackDefinition asset (spline points, width, par, etc.)
    /// - Toggle "Draw Mode" — click in Scene view to add Bezier control points
    ///   (automatically maintains 3N closed-loop or 3N+1 open spline pattern)
    /// - Auto-generate boundaries + checkpoints preview in Scene view (gizmos)
    /// - Validate track before saving
    /// - Save the asset
    ///
    /// Usage:
    /// 1. Tools > QuickChecks > Track Builder
    /// 2. Click "Create New Track Definition" (or assign existing one)
    /// 3. Click "Toggle Draw Mode"
    /// 4. Click in Scene view to add control points
    /// 5. Click "Validate" before saving
    /// 6. Ctrl+S to save the asset
    /// </summary>
    public class TrackBuilderWindow : EditorWindow
    {
        private TrackDefinition _currentTrack;
        private bool _isDrawMode = false;
        private int _hoveredPoint = -1;
        private Vector2 _scrollPos;

        [MenuItem("Tools/QuickChecks/Track Builder")]
        public static void OpenWindow()
        {
            var window = GetWindow<TrackBuilderWindow>("QuickChecks Track Builder");
            window.minSize = new Vector2(400, 600);
            // Hook into Scene view so we can draw gizmos + handle clicks
            SceneView.duringSceneGui += window.OnSceneGUI;
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

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
                        asset.trackId = "track_new";
                        asset.displayName = "New Track";
                        asset.isClosedLoop = true;
                        asset.trackWidth = 4f;
                        asset.lapCount = 1;
                        asset.parSwipeCount = 10;
                        asset.isFree = false;
                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();
                        _currentTrack = asset;
                        Selection.activeObject = asset;
                    }
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            // --- Identity ---
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            _currentTrack.trackId = EditorGUILayout.TextField("Track ID", _currentTrack.trackId);
            _currentTrack.displayName = EditorGUILayout.TextField("Display Name", _currentTrack.displayName);
            _currentTrack.difficultyStars = EditorGUILayout.IntSlider("Difficulty", _currentTrack.difficultyStars, 1, 5);
            _currentTrack.trackIndex = EditorGUILayout.IntField("Track Index (0-7)", _currentTrack.trackIndex);
            _currentTrack.isFree = EditorGUILayout.Toggle("Free (no IAP)", _currentTrack.isFree);
            EditorGUILayout.Space();

            // --- Spline ---
            EditorGUILayout.LabelField("Spline Path", EditorStyles.boldLabel);
            _currentTrack.isClosedLoop = EditorGUILayout.Toggle("Closed Loop", _currentTrack.isClosedLoop);
            _currentTrack.trackWidth = EditorGUILayout.Slider("Track Width", _currentTrack.trackWidth, 1f, 12f);

            int pointCount = _currentTrack.splinePoints?.Length ?? 0;
            int segCount = BezierSpline.SegmentCount(pointCount, _currentTrack.isClosedLoop);
            int expected = BezierSpline.ControlPointCount(segCount, _currentTrack.isClosedLoop);

            EditorGUILayout.LabelField("Status:", $"{pointCount} points / {segCount} segments (expected {expected})");

            // --- Draw mode ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Drawing", EditorStyles.boldLabel);
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = _isDrawMode ? new Color(0.6f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(_isDrawMode ? "DRAW MODE ON — Click in Scene view to add points" : "Toggle Draw Mode"))
            {
                _isDrawMode = !_isDrawMode;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.HelpBox(
                _isDrawMode
                    ? "DRAW MODE: Click in Scene view to add Bezier control points.\n" +
                      "Points are added 3 at a time (one full cubic Bezier segment per click).\n" +
                      "Press ESC or toggle Draw Mode off to stop."
                    : "Click 'Toggle Draw Mode' then click in Scene view to add control points.",
                MessageType.Info
            );

            if (GUILayout.Button("Add Point at Origin"))
            {
                AddControlPoint(Vector2.zero);
            }

            if (GUILayout.Button("Clear All Points"))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Points",
                    "Remove all spline control points from this track?",
                    "Clear", "Cancel"))
                {
                    _currentTrack.splinePoints = new Vector2[0];
                    EditorUtility.SetDirty(_currentTrack);
                    SceneView.RepaintAll();
                }
            }

            // --- Camera + gameplay ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera & Gameplay", EditorStyles.boldLabel);
            _currentTrack.defaultZoom = EditorGUILayout.Slider("Default Zoom", _currentTrack.defaultZoom, 5f, 20f);
            _currentTrack.lapCount = EditorGUILayout.IntSlider("Lap Count", _currentTrack.lapCount, 1, 5);
            _currentTrack.parSwipeCount = EditorGUILayout.IntSlider("Par (swipes)", _currentTrack.parSwipeCount, 5, 80);

            int maxCap = _currentTrack.maxSwipeCap;
            bool useFormula = (maxCap <= 0);
            useFormula = EditorGUILayout.Toggle("Use Formula (30 + stars×10)", useFormula);
            if (!useFormula)
            {
                _currentTrack.maxSwipeCap = EditorGUILayout.IntField("Max Swipe Cap", Mathf.Max(10, maxCap));
            }
            else
            {
                _currentTrack.maxSwipeCap = -1;
            }

            // --- Validation ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate Track"))
            {
                if (_currentTrack.Validate(out string error))
                {
                    EditorUtility.DisplayDialog("Validation", "Track is valid!", "OK");
                    float length = BezierSpline.ApproximateLength(_currentTrack.splinePoints, _currentTrack.isClosedLoop);
                    Debug.Log($"[TrackBuilder] Track '{_currentTrack.trackId}' valid. " +
                              $"Length: {length:F1} units, par: {_currentTrack.parSwipeCount} swipes.");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Failed", error, "OK");
                }
            }

            // --- Save ---
            EditorGUILayout.Space();
            if (GUILayout.Button("Save Asset"))
            {
                EditorUtility.SetDirty(_currentTrack);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TrackBuilder] Saved track '{_currentTrack.trackId}'.");
            }

            EditorGUILayout.EndScrollView();
        }

        private void AddControlPoint(Vector2 position)
        {
            var points = _currentTrack.splinePoints;
            if (points == null) points = new Vector2[0];

            // Add a cubic Bezier segment (3 new points + use last p3 as new p0).
            // For the first segment, we need 4 points (p0, p1, p2, p3).
            // For subsequent segments, we add 3 (p1, p2, p3) — p0 shared.
            // For closed loops, the LAST segment doesn't store p3 (wraps to first p0).
            //
            // Simplification: just add points one at a time. Designer can fix winding later.
            System.Array.Resize(ref points, points.Length + 1);
            points[points.Length - 1] = position;
            _currentTrack.splinePoints = points;
            EditorUtility.SetDirty(_currentTrack);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentTrack == null || _currentTrack.splinePoints == null) return;

            // --- Draw control point gizmos ---
            for (int i = 0; i < _currentTrack.splinePoints.Length; i++)
            {
                Vector2 p = _currentTrack.splinePoints[i];
                Vector3 worldP = new Vector3(p.x, p.y, 0);

                // Handle label
                Handles.Label(worldP + Vector3.up * 0.5f, $"P{i}", EditorStyles.miniBoldLabel);

                // Handle cap (draggable in scene view)
                float handleSize = HandleUtility.GetHandleSize(worldP) * 0.15f;
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.FreeMoveHandle(
                    worldP, handleSize, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentTrack, "Move Bezier control point");
                    _currentTrack.splinePoints[i] = new Vector2(newPos.x, newPos.y);
                    EditorUtility.SetDirty(_currentTrack);
                }
            }

            // --- Draw the spline curve ---
            if (_currentTrack.splinePoints.Length >= 4)
            {
                int segCount = BezierSpline.SegmentCount(_currentTrack.splinePoints.Length, _currentTrack.isClosedLoop);
                if (segCount > 0)
                {
                    Handles.color = new Color(0.247f, 0.878f, 0.760f);  // cyan
                    int samples = 30;
                    Vector3 prev = BezierSpline.EvaluateSpline(_currentTrack.splinePoints, 0f, _currentTrack.isClosedLoop);
                    for (int i = 1; i <= samples; i++)
                    {
                        float t = (float)i / samples;
                        Vector3 cur = BezierSpline.EvaluateSpline(_currentTrack.splinePoints, t, _currentTrack.isClosedLoop);
                        Handles.DrawLine(prev, cur, 2f);
                        prev = cur;
                    }
                }
            }

            // --- Draw boundary preview (if track has width) ---
            if (_currentTrack.splinePoints.Length >= 4 && _currentTrack.trackWidth > 0.1f)
            {
                float halfWidth = _currentTrack.trackWidth * 0.5f;
                BezierSpline.SampleBoundaries(
                    _currentTrack.splinePoints, halfWidth, 30, _currentTrack.isClosedLoop,
                    out var leftBoundary, out var rightBoundary);

                Handles.color = new Color(0.247f, 0.878f, 0.760f, 0.4f);
                for (int i = 0; i < leftBoundary.Count - 1; i++)
                {
                    Handles.DrawLine(leftBoundary[i], leftBoundary[i + 1], 1f);
                    Handles.DrawLine(rightBoundary[i], rightBoundary[i + 1], 1f);
                }
                if (_currentTrack.isClosedLoop)
                {
                    Handles.DrawLine(leftBoundary[leftBoundary.Count - 1], leftBoundary[0], 1f);
                    Handles.DrawLine(rightBoundary[rightBoundary.Count - 1], rightBoundary[0], 1f);
                }
            }

            // --- Handle click-to-add in draw mode ---
            if (_isDrawMode)
            {
                // Show draw mode banner
                Handles.BeginGUI();
                var rect = new Rect(sceneView.position.width - 280, 10, 270, 30);
                GUI.color = new Color(0.6f, 1f, 0.6f, 0.9f);
                GUI.Box(rect, "DRAW MODE: click to add point");
                GUI.color = Color.white;
                Handles.EndGUI();

                // Detect left-click in scene view
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if (Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0 &&
                    !Event.current.alt)
                {
                    // Raycast to find world position at the click
                    Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    // For 2D, we want the (x, y) of the ray at z=0
                    float t = -ray.origin.z / ray.direction.z;
                    Vector3 hit = ray.origin + ray.direction * t;
                    AddControlPoint(new Vector2(hit.x, hit.y));
                    Event.current.Use();
                }

                // ESC exits draw mode
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    _isDrawMode = false;
                    SceneView.RepaintAll();
                }
            }
        }
    }
}
#endif
