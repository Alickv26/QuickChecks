using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Track;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Player-facing custom track builder. Runs at runtime (NOT in the Unity editor).
    ///
    /// Modes:
    ///   Draw     — tap empty space to add a Bezier control point; drag points to move them
    ///   Wall      — tap to place a wall obstacle; drag placed walls to move; resize via slider
    ///   Gravel    — same as Wall, but gravel
    ///   BlackHole — same, but black hole
    ///   Boost / Shield / Magnet / Slingshot — tap to place a power-up pickup
    ///   Select    — tap placed items to select; drag to move; delete via button
    ///
    /// UI:
    ///   - Top: track name input + Save + Test + Back buttons
    ///   - Bottom: tool palette (mode buttons)
    ///   - Side (when item selected): size slider + delete button
    ///
    /// The track is previewed live via SplineTrackBuilder.Build, rebuilt whenever points
    /// or obstacles change.
    /// </summary>
    public class CustomTrackBuilderController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField trackNameInput;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button testButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button clearAllButton;

        [Header("Tool palette buttons")]
        [SerializeField] private Button drawModeButton;
        [SerializeField] private Button wallModeButton;
        [SerializeField] private Button gravelModeButton;
        [SerializeField] private Button blackHoleModeButton;
        [SerializeField] private Button boostModeButton;
        [SerializeField] private Button shieldModeButton;
        [SerializeField] private Button magnetModeButton;
        [SerializeField] private Button slingshotModeButton;
        [SerializeField] private Button selectModeButton;

        [Header("Selected item panel")]
        [SerializeField] private GameObject selectedItemPanel;
        [SerializeField] private Slider sizeSlider;
        [SerializeField] private Button deleteSelectedItemButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        [Header("Camera")]
        [SerializeField] private UnityEngine.Camera builderCamera;

        [Header("Colors (palette)")]
        [SerializeField] private Color pointColor = new(0.247f, 0.878f, 0.760f);
        [SerializeField] private Color selectedPointColor = new(1f, 0.82f, 0.4f);

        // State
        private enum BuilderMode { Draw, Wall, Gravel, BlackHole, Boost, Shield, Magnet, Slingshot, Select }
        private BuilderMode _mode = BuilderMode.Draw;

        private CustomTrackData _track;
        private GameObject _trackPreviewRoot;
        private readonly List<GameObject> _pointMarkers = new();
        private readonly List<GameObject> _obstacleMarkers = new();
        private readonly List<GameObject> _powerUpMarkers = new();

        // Drag state
        private int _selectedPointIdx = -1;       // -1 = none
        private int _selectedObstacleIdx = -1;
        private int _selectedPowerUpIdx = -1;
        private bool _isDragging = false;
        private const float POINT_SELECT_RADIUS = 1.2f;
        private const float OBstacle_SELECT_RADIUS = 2.5f;

        private void Start()
        {
            // Load existing track if we're editing, otherwise start fresh
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.pendingCustomTrack != null)
            {
                _track = GameFlowManager.Instance.pendingCustomTrack;
                GameFlowManager.Instance.pendingCustomTrack = null;  // consume
                Debug.Log($"[CustomBuilder] Editing existing track: {_track.trackName}");
            }
            else
            {
                _track = new CustomTrackData
                {
                    trackId = "",  // assigned on save
                    trackName = "My Custom Track",
                    authorId = GameFlowManager.Instance?.profile?.playerId ?? "anon",
                    authorName = GameFlowManager.Instance?.profile?.displayName ?? "Player",
                    isClosedLoop = true,
                    trackWidth = 6f,
                    defaultZoom = 16f,
                    splinePoints = new List<CustomTrackData.SerializableVector2>(),
                    obstacles = new List<CustomTrackData.PlacedObstacle>(),
                    powerUps = new List<CustomTrackData.PlacedPowerUp>()
                };
                Debug.Log("[CustomBuilder] New track");
            }

            if (trackNameInput != null)
            {
                trackNameInput.text = _track.trackName;
                trackNameInput.onValueChanged.RemoveAllListeners();
                trackNameInput.onValueChanged.AddListener(val => _track.trackName = val);
            }

            WireButton(saveButton, OnSaveClicked);
            WireButton(testButton, OnTestClicked);
            WireButton(backButton, OnBackClicked);
            WireButton(clearAllButton, OnClearAllClicked);

            WireModeButton(drawModeButton, BuilderMode.Draw);
            WireModeButton(wallModeButton, BuilderMode.Wall);
            WireModeButton(gravelModeButton, BuilderMode.Gravel);
            WireModeButton(blackHoleModeButton, BuilderMode.BlackHole);
            WireModeButton(boostModeButton, BuilderMode.Boost);
            WireModeButton(shieldModeButton, BuilderMode.Shield);
            WireModeButton(magnetModeButton, BuilderMode.Magnet);
            WireModeButton(slingshotModeButton, BuilderMode.Slingshot);
            WireModeButton(selectModeButton, BuilderMode.Select);

            if (sizeSlider != null)
            {
                sizeSlider.minValue = 0.5f;
                sizeSlider.maxValue = 6f;
                sizeSlider.value = 2f;
                sizeSlider.onValueChanged.RemoveAllListeners();
                sizeSlider.onValueChanged.AddListener(OnSizeSliderChanged);
            }
            WireButton(deleteSelectedItemButton, OnDeleteSelectedItemClicked);

            if (selectedItemPanel != null) selectedItemPanel.SetActive(false);

            if (builderCamera == null) builderCamera = UnityEngine.Camera.main;
            if (builderCamera != null)
            {
                builderCamera.orthographic = true;
                builderCamera.orthographicSize = 16f;
                builderCamera.transform.position = new Vector3(0, 0, -10);
                builderCamera.backgroundColor = new Color(0.055f, 0.078f, 0.078f);
            }

            RebuildTrackPreview();
            UpdateModeIndicators();
            UpdateStatus("Tap empty space to add track points. Min 4 points to save.");
        }

        private void WireButton(Button btn, Action onClick)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                onClick?.Invoke();
            });
            if (btn.GetComponent<ButtonHoverFx>() == null) btn.gameObject.AddComponent<ButtonHoverFx>();
        }

        private void WireModeButton(Button btn, BuilderMode mode)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                _mode = mode;
                DeselectAll();
                UpdateModeIndicators();
            });
            if (btn.GetComponent<ButtonHoverFx>() == null) btn.gameObject.AddComponent<ButtonHoverFx>();
        }

        private void UpdateModeIndicators()
        {
            // Visually distinguish active mode button (cyan bg) from inactive (dim bg)
            SetModeButtonColor(drawModeButton, _mode == BuilderMode.Draw);
            SetModeButtonColor(wallModeButton, _mode == BuilderMode.Wall);
            SetModeButtonColor(gravelModeButton, _mode == BuilderMode.Gravel);
            SetModeButtonColor(blackHoleModeButton, _mode == BuilderMode.BlackHole);
            SetModeButtonColor(boostModeButton, _mode == BuilderMode.Boost);
            SetModeButtonColor(shieldModeButton, _mode == BuilderMode.Shield);
            SetModeButtonColor(magnetModeButton, _mode == BuilderMode.Magnet);
            SetModeButtonColor(slingshotModeButton, _mode == BuilderMode.Slingshot);
            SetModeButtonColor(selectModeButton, _mode == BuilderMode.Select);
        }

        private static void SetModeButtonColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            img.color = isActive
                ? new Color(0.247f, 0.878f, 0.760f, 0.9f)   // cyan accent (active)
                : new Color(0.086f, 0.110f, 0.110f, 0.9f);    // dark teal (inactive)
        }

        // =====================================================================
        // Input handling
        // =====================================================================

        private void Update()
        {
            // Pan camera with WASD / arrow keys (for desktop testing)
            // Note: fully-qualify as UnityEngine.Input because our QuickChecks.Input
            // namespace shadows UnityEngine.Input.
            float panX = UnityEngine.Input.GetAxisRaw("Horizontal");
            float panY = UnityEngine.Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(panX) > 0.01f || Mathf.Abs(panY) > 0.01f)
            {
                var pos = builderCamera.transform.position;
                pos.x += panX * 20f * Time.deltaTime;
                pos.y += panY * 20f * Time.deltaTime;
                builderCamera.transform.position = pos;
            }

            // Mouse wheel zoom
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                builderCamera.orthographicSize = Mathf.Clamp(
                    builderCamera.orthographicSize - scroll * 2f, 6f, 30f);
            }

            // Handle tap / click / drag
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                Vector3 worldPos = builderCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                worldPos.z = 0;
                HandlePointerDown(worldPos);
            }
            else if (UnityEngine.Input.GetMouseButton(0) && _isDragging)
            {
                Vector3 worldPos = builderCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                worldPos.z = 0;
                HandlePointerDrag(worldPos);
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0) && _isDragging)
            {
                _isDragging = false;
            }
        }

        private void HandlePointerDown(Vector3 worldPos)
        {
            // First check if we tapped an existing point (in any mode — drag/move is universal)
            int pointIdx = FindNearestPointIndex(worldPos, POINT_SELECT_RADIUS);
            if (pointIdx >= 0)
            {
                _selectedPointIdx = pointIdx;
                _isDragging = true;
                if (_mode == BuilderMode.Select)
                {
                    UpdateStatus($"Point {pointIdx + 1} selected. Drag to move.");
                }
                return;
            }

            // Check obstacles/power-ups (only in Select mode can we select placed items)
            if (_mode == BuilderMode.Select)
            {
                int obsIdx = FindNearestObstacleIndex(worldPos);
                if (obsIdx >= 0)
                {
                    _selectedObstacleIdx = obsIdx;
                    _selectedPowerUpIdx = -1;
                    ShowSelectedItemPanel(isObstacle: true);
                    _isDragging = true;
                    return;
                }
                int puIdx = FindNearestPowerUpIndex(worldPos);
                if (puIdx >= 0)
                {
                    _selectedPowerUpIdx = puIdx;
                    _selectedObstacleIdx = -1;
                    ShowSelectedItemPanel(isObstacle: false);
                    _isDragging = true;
                    return;
                }
            }

            // Otherwise, the tap places something based on the current mode
            switch (_mode)
            {
                case BuilderMode.Draw:
                    AddPoint(worldPos);
                    break;
                case BuilderMode.Wall:
                    PlaceObstacle("Wall", worldPos, new Vector2(2f, 2f));
                    break;
                case BuilderMode.Gravel:
                    PlaceObstacle("Gravel", worldPos, new Vector2(6f, 3f));
                    break;
                case BuilderMode.BlackHole:
                    PlaceObstacle("BlackHole", worldPos, new Vector2(2f, 2f));
                    break;
                case BuilderMode.Boost:
                    PlacePowerUp("Boost", worldPos);
                    break;
                case BuilderMode.Shield:
                    PlacePowerUp("Shield", worldPos);
                    break;
                case BuilderMode.Magnet:
                    PlacePowerUp("Magnet", worldPos);
                    break;
                case BuilderMode.Slingshot:
                    PlacePowerUp("Slingshot", worldPos);
                    break;
            }
        }

        private void HandlePointerDrag(Vector3 worldPos)
        {
            if (_selectedPointIdx >= 0)
            {
                _track.splinePoints[_selectedPointIdx] = CustomTrackData.SerializableVector2.From(worldPos);
                RebuildTrackPreview();
            }
            else if (_selectedObstacleIdx >= 0)
            {
                _track.obstacles[_selectedObstacleIdx].position = CustomTrackData.SerializableVector2.From(worldPos);
                _track.obstacles[_selectedObstacleIdx].t = EstimateTAtPosition(worldPos);
                RebuildTrackPreview();
            }
            else if (_selectedPowerUpIdx >= 0)
            {
                _track.powerUps[_selectedPowerUpIdx].position = CustomTrackData.SerializableVector2.From(worldPos);
                _track.powerUps[_selectedPowerUpIdx].t = EstimateTAtPosition(worldPos);
                RebuildTrackPreview();
            }
        }

        // =====================================================================
        // Track editing
        // =====================================================================

        private void AddPoint(Vector3 pos)
        {
            _track.splinePoints.Add(CustomTrackData.SerializableVector2.From(pos));
            RebuildTrackPreview();
            UpdateStatus($"Point {_track.splinePoints.Count} added at ({pos.x:F1}, {pos.y:F1}). " +
                         $"Need 4+ to save.");
        }

        private void PlaceObstacle(string type, Vector3 pos, Vector2 defaultSize)
        {
            _track.obstacles.Add(new CustomTrackData.PlacedObstacle
            {
                type = type,
                position = CustomTrackData.SerializableVector2.From(pos),
                size = CustomTrackData.SerializableVector2.From(defaultSize),
                rotation = 0f,
                t = EstimateTAtPosition(pos),
                lateralOffset = 0f
            });
            RebuildTrackPreview();
            UpdateStatus($"{type} placed at ({pos.x:F1}, {pos.y:F1}). Switch to Select mode to move/resize.");
        }

        private void PlacePowerUp(string type, Vector3 pos)
        {
            _track.powerUps.Add(new CustomTrackData.PlacedPowerUp
            {
                type = type,
                position = CustomTrackData.SerializableVector2.From(pos),
                t = EstimateTAtPosition(pos),
                lateralOffset = 0f
            });
            RebuildTrackPreview();
            UpdateStatus($"{type} power-up placed at ({pos.x:F1}, {pos.y:F1}).");
        }

        /// <summary>Estimates the spline parameter t (0-1) closest to the given world position.</summary>
        private float EstimateTAtPosition(Vector3 pos)
        {
            if (_track.splinePoints.Count < 2) return 0f;
            // Sample 20 points along the spline, find the closest one
            float bestT = 0f;
            float bestDist = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Vector2 sp = BezierSpline.EvaluateSpline(_track.GetSplinePointsAsVector2(), t, _track.isClosedLoop);
                float d = Vector2.Distance(sp, pos);
                if (d < bestDist) { bestDist = d; bestT = t; }
            }
            return bestT;
        }

        private int FindNearestPointIndex(Vector3 pos, float radius)
        {
            float bestDist = radius;
            int bestIdx = -1;
            for (int i = 0; i < _track.splinePoints.Count; i++)
            {
                var p = _track.splinePoints[i].ToVector2();
                float d = Vector2.Distance(p, pos);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            return bestIdx;
        }

        private int FindNearestObstacleIndex(Vector3 pos)
        {
            float bestDist = OBstacle_SELECT_RADIUS;
            int bestIdx = -1;
            for (int i = 0; i < _track.obstacles.Count; i++)
            {
                var o = _track.obstacles[i];
                float d = Vector2.Distance(o.position.ToVector2(), pos);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            return bestIdx;
        }

        private int FindNearestPowerUpIndex(Vector3 pos)
        {
            float bestDist = OBstacle_SELECT_RADIUS;
            int bestIdx = -1;
            for (int i = 0; i < _track.powerUps.Count; i++)
            {
                var p = _track.powerUps[i];
                float d = Vector2.Distance(p.position.ToVector2(), pos);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            return bestIdx;
        }

        private void DeselectAll()
        {
            _selectedPointIdx = -1;
            _selectedObstacleIdx = -1;
            _selectedPowerUpIdx = -1;
            if (selectedItemPanel != null) selectedItemPanel.SetActive(false);
        }

        private void ShowSelectedItemPanel(bool isObstacle)
        {
            if (selectedItemPanel == null) return;
            selectedItemPanel.SetActive(true);
            if (sizeSlider != null)
            {
                if (isObstacle && _selectedObstacleIdx >= 0)
                {
                    var o = _track.obstacles[_selectedObstacleIdx];
                    sizeSlider.value = Mathf.Max(o.size.x, o.size.y);
                }
                else
                {
                    sizeSlider.value = 1.5f;  // power-ups are always size 1.5
                    sizeSlider.interactable = isObstacle;  // can't resize power-ups
                }
            }
        }

        private void OnSizeSliderChanged(float value)
        {
            if (_selectedObstacleIdx < 0) return;
            var o = _track.obstacles[_selectedObstacleIdx];
            o.size = CustomTrackData.SerializableVector2.From(new Vector2(value, value));
            RebuildTrackPreview();
        }

        private void OnDeleteSelectedItemClicked()
        {
            if (_selectedObstacleIdx >= 0)
            {
                _track.obstacles.RemoveAt(_selectedObstacleIdx);
                _selectedObstacleIdx = -1;
                UpdateStatus("Obstacle deleted.");
            }
            else if (_selectedPowerUpIdx >= 0)
            {
                _track.powerUps.RemoveAt(_selectedPowerUpIdx);
                _selectedPowerUpIdx = -1;
                UpdateStatus("Power-up deleted.");
            }
            else if (_selectedPointIdx >= 0)
            {
                _track.splinePoints.RemoveAt(_selectedPointIdx);
                _selectedPointIdx = -1;
                UpdateStatus("Point deleted.");
            }
            if (selectedItemPanel != null) selectedItemPanel.SetActive(false);
            RebuildTrackPreview();
        }

        // =====================================================================
        // Track preview rendering
        // =====================================================================

        private void RebuildTrackPreview()
        {
            // Clear existing markers
            foreach (var m in _pointMarkers) if (m != null) Destroy(m);
            foreach (var m in _obstacleMarkers) if (m != null) Destroy(m);
            foreach (var m in _powerUpMarkers) if (m != null) Destroy(m);
            _pointMarkers.Clear();
            _obstacleMarkers.Clear();
            _powerUpMarkers.Clear();

            // Destroy old preview root if it exists
            if (_trackPreviewRoot != null) Destroy(_trackPreviewRoot);
            _trackPreviewRoot = new GameObject("TrackPreview");

            // Only build the actual track mesh if we have 4+ points (spline needs control points)
            if (_track.splinePoints.Count >= 4)
            {
                var def = CustomTrackLibrary.ToTrackDefinition(_track);
                var builderGo = new GameObject("SplineBuilder");
                builderGo.transform.SetParent(_trackPreviewRoot.transform, false);
                var splineBuilder = builderGo.AddComponent<SplineTrackBuilder>();
                splineBuilder.Build(def);
            }

            // Draw point markers (small colored squares)
            for (int i = 0; i < _track.splinePoints.Count; i++)
            {
                var p = _track.splinePoints[i].ToVector2();
                var marker = CreateMarker($"Point_{i}", p, 0.5f,
                    i == _selectedPointIdx ? selectedPointColor : pointColor, sortingOrder: 20);
                _pointMarkers.Add(marker);
            }

            // Draw obstacle markers
            for (int i = 0; i < _track.obstacles.Count; i++)
            {
                var o = _track.obstacles[i];
                Color col = o.type switch
                {
                    "Wall" => new Color(0.42f, 0.42f, 0.42f, 0.9f),
                    "Gravel" => new Color(0.6f, 0.5f, 0.3f, 0.7f),
                    "BlackHole" => new Color(0.15f, 0f, 0.2f, 0.95f),
                    _ => Color.white
                };
                var marker = CreateMarker($"Obs_{o.type}_{i}", o.position.ToVector2(),
                    Mathf.Max(o.size.x, o.size.y) * 0.5f, col, sortingOrder: 15);
                _obstacleMarkers.Add(marker);
            }

            // Draw power-up markers
            for (int i = 0; i < _track.powerUps.Count; i++)
            {
                var p = _track.powerUps[i];
                Color col = p.type switch
                {
                    "Boost" => new Color(1f, 0.42f, 0.42f),       // coral
                    "Shield" => new Color(0.31f, 0.80f, 0.77f),   // mint
                    "Magnet" => new Color(0.78f, 0.64f, 1f),       // lavender
                    "Slingshot" => new Color(0.72f, 0.51f, 1f),    // purple
                    _ => Color.white
                };
                var marker = CreateMarker($"PU_{p.type}_{i}", p.position.ToVector2(), 0.7f, col, sortingOrder: 18);
                _powerUpMarkers.Add(marker);
            }
        }

        private GameObject CreateMarker(string name, Vector2 pos, float radius, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sprite = CreateSquareSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(radius * 2, radius * 2);
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private Sprite _cachedSquareSprite;
        private Sprite CreateSquareSprite()
        {
            if (_cachedSquareSprite != null) return _cachedSquareSprite;
            const int size = 16;
            var tex = new Texture2D(size, size);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _cachedSquareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedSquareSprite;
        }

        // =====================================================================
        // Button handlers
        // =====================================================================

        private void OnSaveClicked()
        {
            if (!ValidateTrack(out string error))
            {
                UpdateStatus(error, isError: true);
                return;
            }
            _track.trackName = string.IsNullOrEmpty(trackNameInput?.text) ? "Untitled" : trackNameInput.text;
            _track.RecalculatePar();
            CustomTrackLibrary.Save(_track);
            UpdateStatus($"Track '{_track.trackName}' saved! Par: {_track.parSwipeCount} swipes.");
        }

        private void OnTestClicked()
        {
            if (!ValidateTrack(out string error))
            {
                UpdateStatus(error, isError: true);
                return;
            }
            _track.trackName = string.IsNullOrEmpty(trackNameInput?.text) ? "Untitled" : trackNameInput.text;
            _track.RecalculatePar();
            CustomTrackLibrary.Save(_track);

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.pendingCustomTrack = _track;
                GameFlowManager.Instance.selectedMode = RaceMode.Solo;
                GameFlowManager.Instance.StartRace(
                    CustomTrackLibrary.ToTrackDefinition(_track),
                    GameFlowManager.Instance.selectedKart ?? TrackFactory.GetDefaultKart());
            }
        }

        private void OnBackClicked()
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToCustomTrackSelect();
            }
            else
            {
                SceneManager.LoadScene("CustomTrackSelect");
            }
        }

        private void OnClearAllClicked()
        {
            _track.splinePoints.Clear();
            _track.obstacles.Clear();
            _track.powerUps.Clear();
            DeselectAll();
            RebuildTrackPreview();
            UpdateStatus("Track cleared.");
        }

        private bool ValidateTrack(out string error)
        {
            if (_track.splinePoints.Count < 4)
            {
                error = "Need at least 4 points to save. Tap empty space to add points.";
                return false;
            }
            if (string.IsNullOrEmpty(trackNameInput?.text?.Trim()))
            {
                error = "Track name can't be empty.";
                return false;
            }
            error = "";
            return true;
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = isError
                ? new Color(1f, 0.42f, 0.42f)
                : new Color(0.969f, 0.969f, 0.949f);
        }
    }
}
