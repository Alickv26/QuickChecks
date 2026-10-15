using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Input;
using QuickChecks.Racing;
using QuickChecks.Ghost;
using QuickChecks.Core;
using QuickChecks.Audio;
using QuickChecks.Track;
using QuickChecks.Camera;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// Race scene controller. Replaces PrototypeBootstrapper for the full game flow.
    ///
    /// Reads the selected track from GameFlowManager.Instance.selectedTrack,
    /// builds the scene (camera, track, kart, HUD, ghost), runs the race,
    /// then transitions to Results scene on completion.
    ///
    /// If no GameFlowManager exists (e.g., running the Prototype scene directly),
    /// falls back to the oval test track (same as PrototypeBootstrapper).
    /// </summary>
    public class RaceSceneController : MonoBehaviour, Core.IRaceStarter
    {
        [Header("Fallback (if no GameFlowManager)")]
        [SerializeField] private InputSettings inputSettings;
        [SerializeField] private GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private GameEventSO<SwipeData> ghostSwipeEvent;
        [SerializeField] private KartStats kartStats;

        [Header("Auto-transition")]
        [Tooltip("Seconds to wait on results screen before auto-returning to track select.")]
        [SerializeField] private float resultsDisplayDuration = 2f;

        private Track.TrackDefinition _trackDef;
        private Track.TrackRuntimeData _trackData;
        private KartController _kart;
        private SwipeDetector _swipeDetector;
        private GhostRecorder _ghostRecorder;
        private CameraRig _cameraRig;
        private Camera.CameraFollowController _cameraFollowController;
        private PrototypeHUD _hud;
        private SoloGhostPlayer _soloGhost;

        private long _raceStartMs;
        private int _swipeCount;
        private bool _finished;
        private float _autoResetTimer = -1f;
        private Vector3 _runtimeKartSpawn;

        public int SwipeCount => _swipeCount;
        public KartController Kart => _kart;
        public int ParSwipes => _trackDef != null ? _trackDef.parSwipeCount : 8;
        public SoloGhostPlayer SoloGhost => _soloGhost;
        public bool IsFinished => _finished;

        public event Action<int, long> OnRaceFinished;
        public event Action<int, long, bool> OnGhostUpdated;

        private void Awake()
        {
            EnsureAssets();
            ResolveTrack();
            BuildScene();
        }

        private void Start()
        {
            StartRace();
        }

        private void EnsureAssets()
        {
            if (inputSettings == null)
            {
                inputSettings = ScriptableObject.CreateInstance<InputSettings>();
            }
            if (swipeEvent == null)
            {
                swipeEvent = ScriptableObject.CreateInstance<SwipeEvent>();
            }
            if (ghostSwipeEvent == null)
            {
                ghostSwipeEvent = ScriptableObject.CreateInstance<GhostSwipeEvent>();
            }
            if (kartStats == null)
            {
                kartStats = TrackFactory.GetDefaultKart();
            }
        }

        private void ResolveTrack()
        {
            // Try to get the selected track from GameFlowManager
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.selectedTrack != null)
            {
                _trackDef = GameFlowManager.Instance.selectedTrack;
                Debug.Log($"[RaceScene] Using track from GameFlowManager: {_trackDef.trackId} ({_trackDef.displayName})");
            }
            else
            {
                // Fallback: use the oval test track (same as PrototypeBootstrapper)
                _trackDef = CreateFallbackTrack();
                Debug.Log("[RaceScene] No GameFlowManager — using fallback oval test track.");
            }
        }

        private void BuildScene()
        {
            // Same as PrototypeBootstrapper.BuildScene but using _trackDef
            BuildCamera();
            BuildTrack();
            BuildKart();
            BuildHUD();
            BuildServices();
        }

        private void BuildCamera()
        {
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = _trackDef.defaultZoom;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";

            var existingListeners = FindObjectsOfType<AudioListener>();
            if (existingListeners.Length == 0) camGo.AddComponent<AudioListener>();

            _cameraRig = camGo.AddComponent<CameraRig>();
            SetField(_cameraRig, "inputSettings", inputSettings);
            camGo.AddComponent<QuickChecks.Input.PinchZoomInput>();
            _cameraFollowController = camGo.AddComponent<Camera.CameraFollowController>();
            SetField(_cameraFollowController, "cameraRig", _cameraRig);
        }

        private void BuildTrack()
        {
            var trackGo = new GameObject("Track");
            var splineBuilder = trackGo.AddComponent<SplineTrackBuilder>();
            _trackData = splineBuilder.Build(_trackDef);

            Vector2 spawnPos = BezierSpline.EvaluateSpline(_trackDef.splinePoints, 0f, _trackDef.isClosedLoop);
            Vector2 spawnTangent = BezierSpline.EvaluateSplineTangent(_trackDef.splinePoints, 0f, _trackDef.isClosedLoop);
            _runtimeKartSpawn = spawnPos + spawnTangent * 1f;
        }

        private void BuildKart()
        {
            var kartGo = new GameObject("PlayerKart");
            kartGo.transform.position = _runtimeKartSpawn;
            kartGo.transform.localScale = Vector3.one;

            var sr = kartGo.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.82f, 0.4f);
            sr.sprite = CreateSquareSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 1.5f);
            sr.sortingOrder = 10;

            var col = kartGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);

            var rb = kartGo.AddComponent<Rigidbody2D>();
            _kart = kartGo.AddComponent<KartController>();
            _swipeDetector = kartGo.AddComponent<SwipeDetector>();
            _ghostRecorder = kartGo.AddComponent<GhostRecorder>();
            var trail = kartGo.AddComponent<KartTrail>();
            kartGo.AddComponent<SwipeFeedback>();
            kartGo.AddComponent<SwipeAudio>();
            kartGo.AddComponent<AudioSource>();
            var pus = kartGo.AddComponent<PowerUpSystem>();
            var ct = kartGo.AddComponent<CheckpointTracker>();

            // Wire fields
            _kart.SetSwipeEvent(swipeEvent);
            SetField(_kart, "stats", kartStats);  // CRITICAL: wire stats or kart won't move
            SetField(_swipeDetector, "settings", inputSettings);
            SetField(_swipeDetector, "swipeEvent", swipeEvent);
            SetField(_ghostRecorder, "swipeEvent", swipeEvent);
            SetField(_ghostRecorder, "kartTransform", kartGo.transform);
            SetField(_ghostRecorder, "kartController", _kart);
            SetField(trail, "kart", _kart);  // Wire trail so it follows the kart
            SetField(pus, "kart", _kart);
            SetField(_cameraRig, "target", kartGo.transform);

            // Wire checkpoint tracker
            if (_trackData != null)
            {
                var cpArray = _trackData.checkpoints.ToArray();
                var finishCol = _trackData.finishLine?.GetComponent<Collider2D>();
                ct.SetCheckpoints(cpArray, finishCol, _trackDef.lapCount);
                ct.OnRaceComplete += (total) => { if (!_finished) FinishRace(); };
            }

            // Camera follow
            if (_cameraFollowController != null) _cameraFollowController.SetPlayerKart(_kart);
        }

        private void BuildHUD()
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Status text (top-left)
            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(canvasGo.transform, false);
            TMP_Text statusText;
            try { statusText = statusGo.AddComponent<TextMeshProUGUI>(); }
            catch { statusText = (TMP_Text)(object)statusGo.AddComponent<Text>(); }
            statusText.fontSize = 36;
            statusText.alignment = TextAlignmentOptions.TopLeft;
            statusText.color = new Color(0.969f, 0.969f, 0.949f);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(0, 1);
            statusRect.pivot = new Vector2(0, 1);
            statusRect.anchoredPosition = new Vector2(40, -40);
            statusRect.sizeDelta = new Vector2(900, 800);

            // Reset button (top-right)
            var resetGo = new GameObject("ResetButton");
            resetGo.transform.SetParent(canvasGo.transform, false);
            var resetBtn = resetGo.AddComponent<Button>();
            var resetImg = resetGo.AddComponent<Image>();
            resetImg.color = new Color(0.247f, 0.878f, 0.760f, 0.9f);
            resetImg.raycastTarget = true;
            resetBtn.targetGraphic = resetImg;
            var resetRect = resetGo.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(1, 1);
            resetRect.anchorMax = new Vector2(1, 1);
            resetRect.pivot = new Vector2(1, 1);
            resetRect.anchoredPosition = new Vector2(-40, -40);
            resetRect.sizeDelta = new Vector2(250, 100);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(resetGo.transform, false);
            try
            {
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "RESET";
                label.fontSize = 42;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.055f, 0.078f, 0.078f);
                label.raycastTarget = false;
            }
            catch
            {
                var label = labelGo.AddComponent<Text>();
                label.text = "RESET";
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 32;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = new Color(0.055f, 0.078f, 0.078f);
                label.raycastTarget = false;
            }
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Wire HUD
            _hud = canvasGo.AddComponent<PrototypeHUD>();
            SetField(_hud, "raceStarter", this);
            SetField(_hud, "kart", _kart);
            if (statusText is TMP_Text tmp) SetField(_hud, "statusTextTMP", tmp);
            SetField(_hud, "resetButton", resetBtn);
        }

        private void BuildServices()
        {
            // Solo ghost
            var ghostGo = new GameObject("SoloGhostPlayer");
            _soloGhost = ghostGo.AddComponent<SoloGhostPlayer>();
            SetField(_soloGhost, "playerRecorder", _ghostRecorder);
            SetField(_soloGhost, "playerSwipeEvent", swipeEvent);
            SetField(_soloGhost, "ghostSwipeEvent", ghostSwipeEvent);
            SetField(_soloGhost, "trackId", _trackDef.trackId);
            SetField(_soloGhost, "playerKart", _kart);

            // Entitlement + IAP (singletons)
            if (Monetization.EntitlementManager.Instance == null)
            {
                var emGo = new GameObject("[EntitlementManager]");
                emGo.AddComponent<Monetization.EntitlementManager>();
            }
            if (Monetization.IAPService.Instance == null)
            {
                var iapGo = new GameObject("[IAPService]");
                var iap = iapGo.AddComponent<Monetization.IAPService>();
                SetField(iap, "useStubMode", true);
            }
        }

        private void StartRace()
        {
            _raceStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _swipeDetector.SetRaceStartTime(_raceStartMs);
            _ghostRecorder.BeginRace(_trackDef.trackId, "kart_starter");
            swipeEvent.Register(OnSwipe);
            _finished = false;
            _swipeCount = 0;

            Vector3 spawn = _runtimeKartSpawn != Vector3.zero ? _runtimeKartSpawn : Vector3.zero;
            _soloGhost?.OnRaceStart(_raceStartMs, spawn);

            Debug.Log($"[RaceScene] Race started on track '{_trackDef.trackId}'. Par = {_trackDef.parSwipeCount} swipes.");
        }

        private void OnSwipe(SwipeData swipe)
        {
            _swipeCount++;
        }

        private void Update()
        {
            if (_autoResetTimer > 0f)
            {
                _autoResetTimer -= Time.deltaTime;
                if (_autoResetTimer <= 0f)
                {
                    _autoResetTimer = -1f;
                    TransitionToResults();
                    return;
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) { _autoResetTimer = -1f; ResetRace(); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.C)) { _soloGhost?.ClearBestRun(); }
        }

        private void FinishRace()
        {
            _finished = true;
            long finishMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartMs;

            int prevBest = _soloGhost?.BestGhost?.swipeCount ?? int.MaxValue;
            bool isNewBest = _swipeCount < prevBest ||
                (_swipeCount == prevBest && finishMs < (_soloGhost?.BestGhost?.finishTimeMs ?? long.MaxValue));

            _soloGhost?.OnRaceFinish(_swipeCount, finishMs);
            swipeEvent.Unregister(OnSwipe);
            OnRaceFinished?.Invoke(_swipeCount, finishMs);
            OnGhostUpdated?.Invoke(_swipeCount, finishMs, isNewBest);

            // Record in player profile
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnTrackCompleted(_trackDef.trackId, _swipeCount, finishMs);
                if (isNewBest) GameFlowManager.Instance.profile?.UnlockNextTrack(_trackDef.trackIndex);
            }

            _autoResetTimer = resultsDisplayDuration;
            Debug.Log($"[RaceScene] Race finished! Swipes: {_swipeCount}, Time: {finishMs / 1000f:F2}s. " +
                      $"Auto-transition to results in {resultsDisplayDuration}s.");
        }

        private void TransitionToResults()
        {
            int bestSwipes = _soloGhost?.BestGhost?.swipeCount ?? _swipeCount;
            long bestTime = _soloGhost?.BestGhost?.finishTimeMs ?? 0;

            if (GameFlowManager.Instance != null)
            {
                bool isNewBest = _swipeCount < bestSwipes ||
                    (_swipeCount == bestSwipes && (_soloGhost?.BestGhost?.finishTimeMs ?? long.MaxValue) < bestTime);
                GameFlowManager.Instance.GoToResults(_swipeCount,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartMs,
                    isNewBest, bestSwipes, bestTime);
            }
            else
            {
                ResetRace();
            }
        }

        public void ResetRace()
        {
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
            if (_kart != null) _kart.RespawnAt(_runtimeKartSpawn);
            if (_trackData?.checkpoints != null)
                foreach (var cp in _trackData.checkpoints) cp?.ResetVisual();
            if (_cameraRig != null && _trackDef != null)
                _cameraRig.ResetToDefault(_trackDef.defaultZoom);
            StartRace();
        }

        // --- Fallback track (same oval as PrototypeBootstrapper) ---

        private Track.TrackDefinition CreateFallbackTrack()
        {
            return TrackFactory.GenerateTrack1_FirstLap();
        }

        private Sprite _cachedSquareSprite;
        private Sprite CreateSquareSprite()
        {
            if (_cachedSquareSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size);
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _cachedSquareSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return _cachedSquareSprite;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            if (obj == null || string.IsNullOrEmpty(fieldName)) return;
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
