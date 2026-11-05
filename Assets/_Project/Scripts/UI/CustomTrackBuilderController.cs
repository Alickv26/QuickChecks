using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    /// Mobile support:
    ///   - Single-finger drag on empty world space = place point / obstacle (depends on mode)
    ///   - Single-finger drag on existing point/obstacle = move it
    ///   - Two-finger pinch = zoom
    ///   - Two-finger drag = pan camera
    ///   - UI elements (buttons, inputs) block world taps — EventSystem check
    /// </summary>
    public class CustomTrackBuilderController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField trackNameInput;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button testButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button clearAllButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;

        [Header("Tool palette buttons")]
        [SerializeField] private Button drawModeButton;
        [SerializeField] private Button freeDrawModeButton;
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
        [SerializeField] private TMP_Text modeIndicatorText;

        [Header("Camera")]
        [SerializeField] private UnityEngine.Camera builderCamera;

        [Header("Colors (palette)")]
        [SerializeField] private Color pointColor = new(0.247f, 0.878f, 0.760f);
        [SerializeField] private Color selectedPointColor = new(1f, 0.82f, 0.4f);
        [SerializeField] private Color pointNumberColor = new(0.055f, 0.078f, 0.078f);

        // State
        private enum BuilderMode { Draw, FreeDraw, Wall, Gravel, BlackHole, Boost, Shield, Magnet, Slingshot, Select }
        private BuilderMode _mode = BuilderMode.Draw;

        private CustomTrackData _track;
        private GameObject _trackPreviewRoot;
        private readonly List<GameObject> _pointMarkers = new();
        private readonly List<GameObject> _obstacleMarkers = new();
        private readonly List<GameObject> _powerUpMarkers = new();

        // Drag state
        private int _selectedPointIdx = -1;
        private int _selectedObstacleIdx = -1;
        private int _selectedPowerUpIdx = -1;
        private bool _isDragging = false;
        private const float POINT_SELECT_RADIUS = 1.5f;
        private const float OBSTACLE_SELECT_RADIUS = 3.0f;

        // Lazy undo snapshot: when the user taps an existing point/obstacle/power-up,
        // we set this flag instead of pushing a snapshot immediately. The snapshot is
        // only pushed on the FIRST actual drag movement — so a tap without drag doesn't
        // create a no-op undo entry.
        private bool _dragSnapshotPending = false;

        // Touch state for pinch-zoom + pan
        private bool _isPinching = false;
        private float _lastPinchDistance = 0f;
        private bool _isTwoFingerPanning = false;
        private Vector2 _lastTwoFingerCenter = Vector2.zero;

        // Drag threshold — prevents accidental placement when the player is trying to pan
        private bool _hasMovedSinceDown = false;
        private Vector3 _pointerDownWorldPos;
        private const float DRAG_THRESHOLD = 0.5f;

        // Free-draw state — player drags to trace a continuous line; we sample points
        private bool _isFreeDrawing = false;
        private float _lastFreeDrawSampleDist = 0f;
        private const float FREE_DRAW_SAMPLE_INTERVAL = 3.0f;  // sample a point every 3 world units of drag

        // --- Undo / Redo ---
        // We snapshot the track (deep copy of splinePoints/obstacles/powerUps) at every
        // mutating action. Undo pops the current state onto the redo stack, then restores
        // the previous state. Redo does the inverse. Clears redo stack on new mutation.
        private readonly Stack<CustomTrackData> _undoStack = new();
        private readonly Stack<CustomTrackData> _redoStack = new();
        private const int MAX_UNDO_DEPTH = 50;

        /// <summary>Snapshot the current track state BEFORE a mutation. Call at the start of every
        /// mutating action (add/remove/move/etc). Does nothing if the snapshot is identical to
        /// the top of the undo stack (avoids spamming stack with no-op snapshots during drags).</summary>
        private void PushUndoSnapshot()
        {
            if (_track == null) return;
            _undoStack.Push(CloneTrack(_track));
            if (_undoStack.Count > MAX_UNDO_DEPTH) _undoStack.TrimExcess();
            // New mutation invalidates the redo stack
            _redoStack.Clear();
            UpdateUndoRedoInteractable();
        }

        private void OnUndoClicked()
        {
            if (_undoStack.Count == 0) return;
            // Push current state to redo stack so we can re-apply
            _redoStack.Push(CloneTrack(_track));
            _track = _undoStack.Pop();
            DeselectAll();
            RebuildTrackPreview();
            UpdateStatus($"Undo ({_undoStack.Count} left in history).");
            UpdateUndoRedoInteractable();
        }

        private void OnRedoClicked()
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push(CloneTrack(_track));
            _track = _redoStack.Pop();
            DeselectAll();
            RebuildTrackPreview();
            UpdateStatus($"Redo ({_redoStack.Count} left in redo).");
            UpdateUndoRedoInteractable();
        }

        private void UpdateUndoRedoInteractable()
        {
            if (undoButton != null) undoButton.interactable = _undoStack.Count > 0;
            if (redoButton != null) redoButton.interactable = _redoStack.Count > 0;
        }

        /// <summary>Deep-clone a CustomTrackData (spline points + obstacles + power-ups + metadata).</summary>
        private static CustomTrackData CloneTrack(CustomTrackData src)
        {
            return new CustomTrackData
            {
                trackId = src.trackId,
                trackName = src.trackName,
                authorId = src.authorId,
                authorName = src.authorName,
                isClosedLoop = src.isClosedLoop,
                trackWidth = src.trackWidth,
                defaultZoom = src.defaultZoom,
                parSwipeCount = src.parSwipeCount,
                splinePoints = new List<CustomTrackData.SerializableVector2>(src.splinePoints),
                obstacles = new List<CustomTrackData.PlacedObstacle>(src.obstacles),
                powerUps = new List<CustomTrackData.PlacedPowerUp>(src.powerUps)
            };
        }

        private void Start()
        {
            // Load existing track if we're editing, otherwise start fresh
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.pendingCustomTrack != null)
            {
                _track = GameFlowManager.Instance.pendingCustomTrack;
                GameFlowManager.Instance.pendingCustomTrack = null;
                Debug.Log($"[CustomBuilder] Editing existing track: {_track.trackName}");
            }
            else
            {
                _track = new CustomTrackData
                {
                    trackId = "",
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
            WireButton(undoButton, OnUndoClicked);
            WireButton(redoButton, OnRedoClicked);
            UpdateUndoRedoInteractable();

            WireModeButton(drawModeButton, BuilderMode.Draw);
            WireModeButton(freeDrawModeButton, BuilderMode.FreeDraw);
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
            UpdateStatus("DRAW: tap to place points. FREE DRAW: drag to trace. Need 6+ to build.");
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
                // Refresh the preview so point markers show/hide correctly when switching modes
                // (e.g., Draw mode hides points when track is built; Select mode shows them).
                RebuildTrackPreview();
            });
            if (btn.GetComponent<ButtonHoverFx>() == null) btn.gameObject.AddComponent<ButtonHoverFx>();
        }

        private void UpdateModeIndicators()
        {
            SetModeButtonColor(drawModeButton, _mode == BuilderMode.Draw);
            SetModeButtonColor(freeDrawModeButton, _mode == BuilderMode.FreeDraw);
            SetModeButtonColor(wallModeButton, _mode == BuilderMode.Wall);
            SetModeButtonColor(gravelModeButton, _mode == BuilderMode.Gravel);
            SetModeButtonColor(blackHoleModeButton, _mode == BuilderMode.BlackHole);
            SetModeButtonColor(boostModeButton, _mode == BuilderMode.Boost);
            SetModeButtonColor(shieldModeButton, _mode == BuilderMode.Shield);
            SetModeButtonColor(magnetModeButton, _mode == BuilderMode.Magnet);
            SetModeButtonColor(slingshotModeButton, _mode == BuilderMode.Slingshot);
            SetModeButtonColor(selectModeButton, _mode == BuilderMode.Select);

            // Show current mode in the status text
            if (modeIndicatorText != null)
            {
                string modeName = _mode switch
                {
                    BuilderMode.Draw => "DRAW (tap points)",
                    BuilderMode.FreeDraw => "FREE DRAW (drag to trace)",
                    BuilderMode.Wall => "WALL",
                    BuilderMode.Gravel => "GRAVEL",
                    BuilderMode.BlackHole => "BLACK HOLE",
                    BuilderMode.Boost => "BOOST",
                    BuilderMode.Shield => "SHIELD",
                    BuilderMode.Magnet => "MAGNET",
                    BuilderMode.Slingshot => "SLINGSHOT",
                    BuilderMode.Select => "SELECT",
                    _ => "DRAW"
                };
                modeIndicatorText.text = $"Mode: {modeName}";
                modeIndicatorText.color = Phase2UiPalette.AccentCyan;
            }
        }

        private static void SetModeButtonColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            img.color = isActive
                ? new Color(0.247f, 0.878f, 0.760f, 0.9f)
                : new Color(0.086f, 0.110f, 0.110f, 0.9f);

            // Also change the label color for contrast
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = isActive ? new Color(0.055f, 0.078f, 0.078f) : Phase2UiPalette.TextPrimary;
            }
        }

        // =====================================================================
        // Input handling — supports both mouse (desktop) + touch (mobile)
        // =====================================================================

        private void Update()
        {
            // Desktop: pan with WASD / arrow keys
            float panX = UnityEngine.Input.GetAxisRaw("Horizontal");
            float panY = UnityEngine.Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(panX) > 0.01f || Mathf.Abs(panY) > 0.01f)
            {
                var pos = builderCamera.transform.position;
                pos.x += panX * 20f * Time.deltaTime;
                pos.y += panY * 20f * Time.deltaTime;
                builderCamera.transform.position = pos;
            }

            // Desktop: mouse wheel zoom
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                builderCamera.orthographicSize = Mathf.Clamp(
                    builderCamera.orthographicSize - scroll * 2f, 6f, 30f);
            }

            // Touch handling (mobile)
            if (UnityEngine.Input.touchCount == 2)
            {
                HandleTwoFingerGesture();
                return;  // two-finger gestures suppress single-finger world taps
            }
            else
            {
                _isPinching = false;
                _isTwoFingerPanning = false;
            }

            // Single-finger / mouse tap handling
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                // Check if pointer is over a UI element — if so, don't place/drag in the world
                if (IsPointerOverUI()) return;

                Vector3 worldPos = builderCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                worldPos.z = 0;
                _pointerDownWorldPos = worldPos;
                _hasMovedSinceDown = false;

                // Free-draw mode: start the free-draw gesture (don't place a point yet —
                // we sample points as the drag continues)
                if (_mode == BuilderMode.FreeDraw)
                {
                    _isFreeDrawing = true;
                    _lastFreeDrawSampleDist = 0f;
                    // Place the first point — this pushes one undo snapshot for the entire gesture.
                    // Subsequent sampled points (in the drag handler) use pushSnapshot=false.
                    AddPoint(worldPos, pushSnapshot: true);
                    return;
                }

                HandlePointerDown(worldPos);
            }
            else if (UnityEngine.Input.GetMouseButton(0) && (_isDragging || _isFreeDrawing))
            {
                if (IsPointerOverUI()) return;

                Vector3 worldPos = builderCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                worldPos.z = 0;

                // Check if the pointer moved enough to be a drag (not a tap)
                float dragDist = Vector3.Distance(_pointerDownWorldPos, worldPos);
                if (dragDist > DRAG_THRESHOLD)
                {
                    _hasMovedSinceDown = true;
                }

                // Free-draw mode: sample points along the drag path at regular intervals
                if (_isFreeDrawing)
                {
                    // Check distance from the last sampled point
                    if (_track.splinePoints.Count > 0)
                    {
                        var lastPoint = _track.splinePoints[_track.splinePoints.Count - 1].ToVector2();
                        float distFromLast = Vector2.Distance(lastPoint, worldPos);
                        if (distFromLast >= FREE_DRAW_SAMPLE_INTERVAL)
                        {
                            // Don't push snapshot — the whole free-draw gesture is one undo entry.
                            // The snapshot was already pushed when the first point was placed.
                            AddPoint(worldPos, pushSnapshot: false);
                        }
                    }
                    return;
                }

                HandlePointerDrag(worldPos);
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0) && (_isDragging || _isFreeDrawing))
            {
                _isDragging = false;
                _isFreeDrawing = false;
                _hasMovedSinceDown = false;
                // Clear the pending snapshot flag — if the user released without dragging,
                // no snapshot was pushed (no no-op undo entry).
                _dragSnapshotPending = false;
            }
        }

        /// <summary>
        /// Two-finger gesture: pinch to zoom + two-finger drag to pan.
        /// </summary>
        private void HandleTwoFingerGesture()
        {
            var t0 = UnityEngine.Input.GetTouch(0);
            var t1 = UnityEngine.Input.GetTouch(1);

            // Pinch zoom
            float currentDist = Vector2.Distance(t0.position, t1.position);
            if (_isPinching)
            {
                float zoomDelta = (_lastPinchDistance - currentDist) * 0.02f;
                builderCamera.orthographicSize = Mathf.Clamp(
                    builderCamera.orthographicSize + zoomDelta, 6f, 30f);
            }
            _lastPinchDistance = currentDist;
            _isPinching = true;

            // Two-finger pan (drag both fingers in the same direction)
            Vector2 currentCenter = (t0.position + t1.position) / 2f;
            if (_isTwoFingerPanning)
            {
                Vector2 delta = currentCenter - _lastTwoFingerCenter;
                // Convert screen delta to world delta (invert because camera moves opposite to drag)
                float worldUnitsPerScreenPixel = builderCamera.orthographicSize * 2f / Screen.height;
                var camPos = builderCamera.transform.position;
                camPos.x -= delta.x * worldUnitsPerScreenPixel;
                camPos.y -= delta.y * worldUnitsPerScreenPixel;
                builderCamera.transform.position = camPos;
            }
            _lastTwoFingerCenter = currentCenter;
            _isTwoFingerPanning = true;
        }

        /// <summary>True if the pointer is over any UI element (buttons, inputs, etc.)</summary>
        /// <remarks>
        /// Uses EventSystem.current.RaycastAll with a PointerEventData positioned at the mouse
        /// position. This is more reliable than IsPointerOverGameObject for touch input because:
        ///   - IsPointerOverGameObject(fingerId) only works reliably during OnPointerDown/Up callbacks
        ///     or when the touch is in Began phase. For Moved/Stationary/Ended it may return false
        ///     even when the finger IS over a UI element.
        ///   - RaycastAll works for any phase because it directly raycasts against all
        ///     GraphicRaycasters in the scene at the given screen position.
        /// Unity's mouse emulation for touches keeps Input.mousePosition synced with the active
        /// touch position, so this works for both mouse and touch.
        /// </remarks>
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = UnityEngine.Input.mousePosition
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        private void HandlePointerDown(Vector3 worldPos)
        {
            // First check if we tapped an existing point (in any mode — drag/move is universal)
            int pointIdx = FindNearestPointIndex(worldPos, POINT_SELECT_RADIUS);
            if (pointIdx >= 0)
            {
                // Don't push snapshot yet — defer until the drag actually moves.
                // This prevents no-op undo entries when the user taps a point without dragging.
                _dragSnapshotPending = true;
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
                    _dragSnapshotPending = true;
                    _selectedObstacleIdx = obsIdx;
                    _selectedPowerUpIdx = -1;
                    _obstacleResizeHasSnapshot = false;
                    ShowSelectedItemPanel(isObstacle: true);
                    _isDragging = true;
                    return;
                }
                int puIdx = FindNearestPowerUpIndex(worldPos);
                if (puIdx >= 0)
                {
                    _dragSnapshotPending = true;
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
            // Lazy snapshot: push the undo snapshot on the FIRST actual drag movement.
            // If the user just tapped without dragging, no snapshot is pushed (no no-op undo).
            if (_dragSnapshotPending)
            {
                PushUndoSnapshot();
                _dragSnapshotPending = false;
            }

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

        private void AddPoint(Vector3 pos, bool pushSnapshot = true)
        {
            if (pushSnapshot) PushUndoSnapshot();
            _track.splinePoints.Add(CustomTrackData.SerializableVector2.From(pos));
            RebuildTrackPreview();
            int remaining = 6 - _track.splinePoints.Count;
            if (remaining > 0)
                UpdateStatus($"Point {_track.splinePoints.Count} added. Need {remaining} more to build track.");
            else
                UpdateStatus($"Point {_track.splinePoints.Count} added. Track is ready — test it!");
        }

        private void PlaceObstacle(string type, Vector3 pos, Vector2 defaultSize)
        {
            PushUndoSnapshot();
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
            UpdateStatus($"{type} placed. Switch to Select mode to move/resize.");
        }

        private void PlacePowerUp(string type, Vector3 pos)
        {
            PushUndoSnapshot();
            _track.powerUps.Add(new CustomTrackData.PlacedPowerUp
            {
                type = type,
                position = CustomTrackData.SerializableVector2.From(pos),
                t = EstimateTAtPosition(pos),
                lateralOffset = 0f
            });
            RebuildTrackPreview();
            UpdateStatus($"{type} power-up placed.");
        }

        /// <summary>Estimates the spline parameter t (0-1) closest to the given world position.</summary>
        private float EstimateTAtPosition(Vector3 pos)
        {
            if (_track.splinePoints.Count < 2) return 0f;
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
            float bestDist = OBSTACLE_SELECT_RADIUS;
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
            float bestDist = OBSTACLE_SELECT_RADIUS;
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
            _obstacleResizeHasSnapshot = false;
            _dragSnapshotPending = false;
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
                    sizeSlider.interactable = true;
                }
                else
                {
                    sizeSlider.value = 1.5f;
                    sizeSlider.interactable = false;  // can't resize power-ups
                }
            }
        }

        private void OnSizeSliderChanged(float value)
        {
            if (_selectedObstacleIdx < 0) return;
            // Only snapshot once per drag — would otherwise flood the undo stack.
            // We snapshot on first change after selection; subsequent slider changes
            // in the same gesture share one undo entry.
            if (!_obstacleResizeHasSnapshot)
            {
                PushUndoSnapshot();
                _obstacleResizeHasSnapshot = true;
            }
            var o = _track.obstacles[_selectedObstacleIdx];
            o.size = CustomTrackData.SerializableVector2.From(new Vector2(value, value));
            RebuildTrackPreview();
        }
        private bool _obstacleResizeHasSnapshot = false;

        private void OnDeleteSelectedItemClicked()
        {
            if (_selectedObstacleIdx >= 0)
            {
                PushUndoSnapshot();
                _track.obstacles.RemoveAt(_selectedObstacleIdx);
                _selectedObstacleIdx = -1;
                UpdateStatus("Obstacle deleted.");
            }
            else if (_selectedPowerUpIdx >= 0)
            {
                PushUndoSnapshot();
                _track.powerUps.RemoveAt(_selectedPowerUpIdx);
                _selectedPowerUpIdx = -1;
                UpdateStatus("Power-up deleted.");
            }
            else if (_selectedPointIdx >= 0)
            {
                PushUndoSnapshot();
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

            bool hasBuiltTrack = _track.splinePoints.Count >= 6;
            bool trackMeshIsVisible = false;

            // Only build the actual track mesh if we have enough points for a closed loop (6 = 3*2 segments)
            if (hasBuiltTrack)
            {
                var def = CustomTrackLibrary.ToTrackDefinition(_track);
                var builderGo = new GameObject("SplineBuilder");
                builderGo.transform.SetParent(_trackPreviewRoot.transform, false);
                var splineBuilder = builderGo.AddComponent<SplineTrackBuilder>();
                splineBuilder.Build(def);
                trackMeshIsVisible = true;
            }

            // Decide whether to show point markers:
            //   - Always show if track isn't yet built (count < 6) — no mesh to see anyway
            //   - In Select mode, always show (so user can grab and drag points)
            //   - In Draw / FreeDraw: ALWAYS show with numbers (the user wants to see the order
            //     they placed points, even when the track mesh is rendered — so they can plan
            //     where to add the next point or which existing point to drag).
            //   - In Wall / Gravel / BlackHole / Boost / Shield / Magnet / Slingshot: hide
            //     markers once the track is built (focus is on placing items, not editing points).
            //     Exception: keep the currently-dragged point visible.
            bool isDrawLike = _mode == BuilderMode.Draw || _mode == BuilderMode.FreeDraw;
            bool showPoints = !hasBuiltTrack
                              || _mode == BuilderMode.Select
                              || isDrawLike
                              || _selectedPointIdx >= 0;

            if (showPoints)
            {
                for (int i = 0; i < _track.splinePoints.Count; i++)
                {
                    // In Draw/FreeDraw/Select: show all points (with numbers).
                    // In other modes: only the dragged point is shown — others cover the track.
                    if (!isDrawLike && _mode != BuilderMode.Select
                        && trackMeshIsVisible && i != _selectedPointIdx) continue;

                    var p = _track.splinePoints[i].ToVector2();
                    bool isSelected = i == _selectedPointIdx;
                    Color markerColor = isSelected ? selectedPointColor : pointColor;
                    var marker = CreateMarker($"Point_{i}", p, 0.5f, markerColor, sortingOrder: 20);

                    // Add a number label child for Draw / FreeDraw modes (1-based, human-friendly).
                    // We also render numbers in Select mode so the user can see which point is which
                    // while selecting — useful when planning edits.
                    if (isDrawLike || _mode == BuilderMode.Select)
                    {
                        CreateNumberLabel(marker.transform, i + 1);
                    }
                    _pointMarkers.Add(marker);
                }
            }

            // Draw obstacle markers (always visible)
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

            // Draw power-up markers (always visible)
            for (int i = 0; i < _track.powerUps.Count; i++)
            {
                var p = _track.powerUps[i];
                Color col = p.type switch
                {
                    "Boost" => new Color(1f, 0.42f, 0.42f),
                    "Shield" => new Color(0.31f, 0.80f, 0.77f),
                    "Magnet" => new Color(0.78f, 0.64f, 1f),
                    "Slingshot" => new Color(0.72f, 0.51f, 1f),
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

        /// <summary>
        /// Creates a world-space TextMeshPro label showing the given number, anchored to a parent
        /// marker. The label renders above the marker (sortingOrder = 22) so it's visible
        /// even when the track mesh is behind it.
        /// </summary>
        private void CreateNumberLabel(Transform parent, int number)
        {
            var labelGo = new GameObject($"PointNumber_{number}");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = Vector3.zero;
            labelGo.transform.localScale = Vector3.one;
            try
            {
                var label = labelGo.AddComponent<TextMeshPro>();
                label.text = number.ToString();
                label.fontSize = 1.5f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = pointNumberColor;
                label.raycastTarget = false;
                label.enableWordWrapping = false;
                // Render above the marker (sprite sortingOrder was 20, this is 22)
                label.sortingOrder = 22;
                // Auto-size so the number fits inside the 1-unit marker
                label.autoSizeTextContainer = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CustomBuilder] Failed to create TextMeshPro number label: {ex.Message}");
                Destroy(labelGo);
            }
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
                // Stash the track so when the player exits the test race via Back, the builder
                // re-opens with this exact track (with full undo history if the builder survives
                // the scene load — note: undo stacks DON'T survive, only the saved track data does).
                GameFlowManager.Instance.pendingCustomTrack = _track;
                // Set the flag so RaceSceneController's Back button routes back to this builder
                // instead of going to TrackSelect.
                GameFlowManager.Instance.lastRaceCameFromCustomBuilder = true;
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
            if (_track.splinePoints.Count == 0 && _track.obstacles.Count == 0 && _track.powerUps.Count == 0)
            {
                UpdateStatus("Nothing to clear.");
                return;
            }
            PushUndoSnapshot();
            _track.splinePoints.Clear();
            _track.obstacles.Clear();
            _track.powerUps.Clear();
            DeselectAll();
            RebuildTrackPreview();
            UpdateStatus("Track cleared. Tap empty space to add points.");
        }

        private bool ValidateTrack(out string error)
        {
            if (_track.splinePoints.Count < 6)
            {
                error = $"Need at least 6 points to build a track. You have {_track.splinePoints.Count}.";
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
