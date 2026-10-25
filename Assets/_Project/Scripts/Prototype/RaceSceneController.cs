using System;
using System.Collections.Generic;
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

        // Multiplayer state
        private Core.RaceMode _raceMode = Core.RaceMode.Solo;
        private List<KartController> _allKarts = new();
        private List<SwipeDetector> _allSwipeDetectors = new();
        private List<Core.GameEventSO<QuickChecks.Input.SwipeData>> _perKartSwipeEvents = new();
        private List<Racing.KartGlow> _allKartGlows = new();
        private Racing.TurnManager _turnManager;
        private UI.PassPlayOverlay _passPlayOverlay;
        private UI.TurnBannerController _turnBanner;
        private bool _isMultiplayer = false;
        private int _currentTurnPlayerIndex = 0;
        private bool[] _wasKartMoving;
        private float[] _kartStopDelay;  // countdown before declaring kart stopped (prevents false triggers)
        private float _turnStartDelay = 0f;

        // Per-kart swipe handlers (each knows its own player index)
        private List<Action<QuickChecks.Input.SwipeData>> _perKartSwipeHandlers = new();
        private static readonly Color[] PLAYER_COLORS = {
            new Color(0.247f, 0.878f, 0.760f),  // cyan (player 1)
            new Color(1f, 0.42f, 0.42f),         // red (player 2 / CPU)
            new Color(0.42f, 0.79f, 0.47f),      // green (player 3)
            new Color(0.42f, 0.58f, 1f),          // blue (player 4)
        };

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
            // Phase 2: prefer the kart the player selected in Kart Select over the hardcoded default.
            if (kartStats == null && GameFlowManager.Instance != null
                && GameFlowManager.Instance.selectedKart != null)
            {
                kartStats = GameFlowManager.Instance.selectedKart;
                Debug.Log($"[RaceScene] Using selected kart: {kartStats.displayName} ({kartStats.kartId})");
            }
            if (kartStats == null)
            {
                kartStats = TrackFactory.GetDefaultKart();
                Debug.Log("[RaceScene] No selectedKart — falling back to Starter.");
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
            sr.color = kartStats != null ? kartStats.kartColor : new Color(1f, 0.82f, 0.4f);
            sr.sprite = CreateSquareSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 1.5f);
            sr.sortingOrder = 10;

            var col = kartGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);  // Smaller hitbox (visual is 1.5, hitbox is 0.8 for tight navigation)

            var rb = kartGo.AddComponent<Rigidbody2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;  // Catch fast-moving trigger overlaps (power-ups)
            _kart = kartGo.AddComponent<KartController>();
            _swipeDetector = kartGo.AddComponent<SwipeDetector>();
            _ghostRecorder = kartGo.AddComponent<GhostRecorder>();
            var stopHistory = kartGo.AddComponent<Racing.KartStopHistory>();
            var trail = kartGo.AddComponent<KartTrail>();
            kartGo.AddComponent<SwipeFeedback>();
            kartGo.AddComponent<SwipeAudio>();
            kartGo.AddComponent<AudioSource>();
            var glow = kartGo.AddComponent<Racing.KartGlow>();  // Neon glow for active kart
            glow.SetGlowColor(PLAYER_COLORS[0]);  // Player 1 = cyan
            _allKartGlows.Add(glow);
            var visualFeedback = kartGo.AddComponent<Racing.KartVisualFeedback>();
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

            // NOTE: PowerUpPickupNotifier is wired at the END of BuildHUD() (which runs
            // after BuildKart) via _powerUpNotifier.WatchKart(pus). Don't wire it here —
            // _powerUpNotifier is null at this point because BuildHUD hasn't run yet.

            // Wire feedback + audio (was missing — no particles/whoosh in Race scene)
            var feedback = kartGo.GetComponent<SwipeFeedback>();
            if (feedback != null) SetField(feedback, "swipeEvent", swipeEvent);
            if (feedback != null) SetField(feedback, "kart", _kart);
            var audioComp = kartGo.GetComponent<SwipeAudio>();
            if (audioComp != null) SetField(audioComp, "swipeEvent", swipeEvent);

            // Wire checkpoint tracker — route to correct handler based on mode
            if (_trackData != null)
            {
                var cpArray = _trackData.checkpoints.ToArray();
                var finishCol = _trackData.finishLine?.GetComponent<Collider2D>();
                ct.SetCheckpoints(cpArray, finishCol, _trackDef.lapCount);
                // Issue #2: In multiplayer, route to TurnManager.NotifyPlayerFinished, not solo FinishRace
                ct.OnRaceComplete += (total) =>
                {
                    if (_finished) return;
                    if (_isMultiplayer && _turnManager != null)
                        _turnManager.NotifyPlayerFinished(0);
                    else
                        FinishRace();
                };
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

            // ----- Back button (top-right, below RESET) -----
            var backBtnGo = new GameObject("BackButton");
            backBtnGo.transform.SetParent(canvasGo.transform, false);
            var backBtn = backBtnGo.AddComponent<Button>();
            var backBtnImg = backBtnGo.AddComponent<Image>();
            backBtnImg.color = new Color(0.15f, 0.2f, 0.2f, 0.9f);  // dark
            backBtnImg.raycastTarget = true;
            backBtn.targetGraphic = backBtnImg;
            var backBtnRect = backBtnGo.GetComponent<RectTransform>();
            backBtnRect.anchorMin = new Vector2(1, 1);
            backBtnRect.anchorMax = new Vector2(1, 1);
            backBtnRect.pivot = new Vector2(1, 1);
            backBtnRect.anchoredPosition = new Vector2(-40, -160);  // Below reset button
            backBtnRect.sizeDelta = new Vector2(250, 80);

            var backLabelGo = new GameObject("Label");
            backLabelGo.transform.SetParent(backBtnGo.transform, false);
            try
            {
                var backLabel = backLabelGo.AddComponent<TextMeshProUGUI>();
                backLabel.text = "← TRACK SELECT";
                backLabel.fontSize = 30;
                backLabel.alignment = TextAlignmentOptions.Center;
                backLabel.color = Color.white;
                backLabel.raycastTarget = false;
            }
            catch
            {
                var backLabel = backLabelGo.AddComponent<Text>();
                backLabel.text = "← TRACK SELECT";
                backLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                backLabel.fontSize = 22;
                backLabel.alignment = TextAnchor.MiddleCenter;
                backLabel.color = Color.white;
                backLabel.raycastTarget = false;
            }
            var backLabelRect = backLabelGo.GetComponent<RectTransform>();
            backLabelRect.anchorMin = Vector2.zero;
            backLabelRect.anchorMax = Vector2.one;
            backLabelRect.pivot = new Vector2(0.5f, 0.5f);
            backLabelRect.offsetMin = Vector2.zero;
            backLabelRect.offsetMax = Vector2.zero;

            backBtn.onClick.AddListener(() =>
            {
                Debug.Log("[RaceScene] Back button clicked — going to track select.");
                if (Core.GameFlowManager.Instance != null)
                {
                    Core.GameFlowManager.Instance.GoToTrackSelect();
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("TrackSelect");
                }
            });
            Debug.Log("[Prototype] Back button created at top-right (below RESET).");

            // ----- Turn banner (top-center, slides in/out) -----
            var bannerGo = new GameObject("TurnBanner");
            bannerGo.transform.SetParent(canvasGo.transform, false);
            var bannerImg = bannerGo.AddComponent<Image>();
            bannerImg.color = Color.clear;
            bannerImg.raycastTarget = false;
            var bannerRect = bannerGo.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1);
            bannerRect.anchorMax = new Vector2(0.5f, 1);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.anchoredPosition = new Vector2(0, 100);
            bannerRect.sizeDelta = new Vector2(600, 80);

            var bannerLabelGo = new GameObject("BannerText");
            bannerLabelGo.transform.SetParent(bannerGo.transform, false);
            TMP_Text bannerLabel = null;
            try
            {
                bannerLabel = bannerLabelGo.AddComponent<TextMeshProUGUI>();
                bannerLabel.text = "";
                bannerLabel.fontSize = 42;
                bannerLabel.alignment = TextAlignmentOptions.Center;
                bannerLabel.color = Color.white;
                bannerLabel.raycastTarget = false;
            }
            catch
            {
                var legacy = bannerLabelGo.AddComponent<Text>();
                legacy.text = "";
                legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacy.fontSize = 32;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.color = Color.white;
                legacy.raycastTarget = false;
                bannerLabel = (TMP_Text)(object)legacy;
            }
            var blRect = bannerLabelGo.GetComponent<RectTransform>();
            blRect.anchorMin = Vector2.zero;
            blRect.anchorMax = Vector2.one;
            blRect.pivot = new Vector2(0.5f, 0.5f);
            blRect.offsetMin = Vector2.zero;
            blRect.offsetMax = Vector2.zero;

            _turnBanner = bannerGo.AddComponent<UI.TurnBannerController>();
            SetField(_turnBanner, "bannerText", bannerLabel);
            SetField(_turnBanner, "bannerBackground", bannerImg);

            // ----- Power-up pickup notifier (top-center toast, hidden by default) -----
            BuildPowerUpNotifier(canvasGo.transform);
        }

        /// <summary>Builds the in-race power-up pickup notifier toast (hidden by default).</summary>
        private void BuildPowerUpNotifier(Transform canvasTransform)
        {
            var panelGo = new GameObject("PowerUpNotifier");
            panelGo.transform.SetParent(canvasTransform, false);

            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.055f, 0.078f, 0.078f, 0.92f);  // dark teal
            panelImg.raycastTarget = false;
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.85f);
            panelRect.anchorMax = new Vector2(0.5f, 0.85f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 130);

            // Accent bar (left side — colored to match the power-up's effectColor)
            var accentGo = new GameObject("AccentBar");
            accentGo.transform.SetParent(panelGo.transform, false);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = new Color(0.247f, 0.878f, 0.760f, 1f);  // default cyan; updated per pickup
            accentImg.raycastTarget = false;
            var accentRect = accentGo.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.offsetMin = new Vector2(0, 0);
            accentRect.offsetMax = new Vector2(8, 0);

            // Power-up name (large, off-white)
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(panelGo.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = "";
            nameText.fontSize = 38;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = new Color(0.969f, 0.969f, 0.949f);
            nameText.raycastTarget = false;
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.1f, 0.55f);
            nameRect.anchorMax = new Vector2(0.95f, 0.95f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.offsetMin = Vector2.zero; nameRect.offsetMax = Vector2.zero;

            // Effect description (smaller, dim cyan)
            var descGo = new GameObject("DescriptionText");
            descGo.transform.SetParent(panelGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = "";
            descText.fontSize = 24;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = new Color(0.247f, 0.878f, 0.760f, 0.85f);
            descText.enableWordWrapping = true;
            descText.raycastTarget = false;
            var descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.05f);
            descRect.anchorMax = new Vector2(0.95f, 0.50f);
            descRect.pivot = new Vector2(0.5f, 0.5f);
            descRect.offsetMin = Vector2.zero; descRect.offsetMax = Vector2.zero;

            panelGo.SetActive(false);  // hidden by default — shown on pickup

            _powerUpNotifier = panelGo.AddComponent<UI.PowerUpPickupNotifier>();
            SetField(_powerUpNotifier, "panelRoot", panelGo);
            SetField(_powerUpNotifier, "accentBar", accentImg);
            SetField(_powerUpNotifier, "nameText", nameText);
            SetField(_powerUpNotifier, "descriptionText", descText);

            // NOW wire the notifier to the player's kart PowerUpSystem.
            // This must happen HERE (end of BuildHUD) because BuildKart runs BEFORE BuildHUD —
            // the notifier didn't exist when BuildKart tried to wire it.
            if (_kart != null)
            {
                var pus = _kart.GetComponent<PowerUpSystem>();
                if (pus != null && _powerUpNotifier != null)
                {
                    _powerUpNotifier.WatchKart(pus);
                    Debug.Log("[RaceScene] PowerUpPickupNotifier wired to player kart's PowerUpSystem.");
                }
            }
        }

        private UI.PowerUpPickupNotifier _powerUpNotifier;

        private void BuildServices()
        {
            // Resolve race mode from GameFlowManager
            if (GameFlowManager.Instance != null)
            {
                _raceMode = GameFlowManager.Instance.selectedMode;
            }
            _isMultiplayer = !_raceMode.IsSolo();
            _allKarts.Add(_kart);

            if (_isMultiplayer)
            {
                SetupMultiplayer();
            }
            else
            {
                // Solo mode: setup ghost
                var ghostGo = new GameObject("SoloGhostPlayer");
                _soloGhost = ghostGo.AddComponent<SoloGhostPlayer>();
                SetField(_soloGhost, "playerRecorder", _ghostRecorder);
                SetField(_soloGhost, "trackId", _trackDef.trackId);
                SetField(_soloGhost, "playerKart", _kart);

                // Phase 2: if GameFlowManager has a pending external ghost (e.g. downloaded
                // from the leaderboard via the 'Replay' button), load it now so it overrides
                // the player's local best for this race.
                if (GameFlowManager.Instance != null &&
                    GameFlowManager.Instance.pendingExternalGhost != null)
                {
                    var extGhost = GameFlowManager.Instance.pendingExternalGhost;
                    string extTrackId = GameFlowManager.Instance.pendingExternalTrackId;
                    if (extTrackId == _trackDef.trackId)
                    {
                        _soloGhost.LoadExternalGhost(extGhost);
                        Debug.Log($"[RaceScene] Loaded external ghost for track {_trackDef.trackId}. " +
                                  "Will replay against the player this race.");
                    }
                    else
                    {
                        Debug.LogWarning($"[RaceScene] External ghost trackId mismatch — " +
                                          $"ghost is for '{extTrackId}' but racing '{_trackDef.trackId}'. " +
                                          "Ignoring external ghost.");
                    }
                }
            }

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

        private void SetupMultiplayer()
        {
            int playerCount = _raceMode.PlayerCount();
            Debug.Log($"[RaceScene] Multiplayer mode: {_raceMode.DisplayName()} ({playerCount} players)");

            // Disable CameraFollowController in multiplayer — HandleTurnStart controls camera
            if (_cameraFollowController != null)
            {
                _cameraFollowController.enabled = false;
                Debug.Log("[RaceScene] CameraFollowController disabled (multiplayer uses HandleTurnStart for camera).");
            }

            // CRITICAL: Create a separate SwipeEvent for EACH kart so they don't share input.
            // Each kart gets its own handler that KNOWS its player index.
            var player0Event = ScriptableObject.CreateInstance<Core.SwipeEvent>();
            _perKartSwipeEvents.Add(player0Event);
            SetField(_swipeDetector, "swipeEvent", player0Event);
            _kart.SetSwipeEvent(player0Event);

            // Create a per-kart handler for player 0
            int p0Index = 0;  // capture for closure
            var handler0 = new Action<QuickChecks.Input.SwipeData>(swipe => OnKartSwipe(p0Index, swipe));
            _perKartSwipeHandlers.Add(handler0);
            player0Event.Register(handler0);
            _allSwipeDetectors.Add(_swipeDetector);

            // Spawn additional karts (player 0 is already created as _kart)
            for (int i = 1; i < playerCount; i++)
            {
                SpawnAdditionalKart(i);
            }

            // Init wasKartMoving + kartStopDelay arrays
            _wasKartMoving = new bool[_allKarts.Count];
            _kartStopDelay = new float[_allKarts.Count];

            // Create TurnManager
            var tmGo = new GameObject("[TurnManager]");
            _turnManager = tmGo.AddComponent<Racing.TurnManager>();

            // Build player states for TurnManager
            var playerStates = new List<Racing.TurnManager.PlayerState>();
            for (int i = 0; i < _allKarts.Count; i++)
            {
                playerStates.Add(new Racing.TurnManager.PlayerState
                {
                    playerName = i == 0 ? "Player 1" : (_raceMode.IsVsCpu() ? "CPU" : $"Player {i + 1}"),
                    kart = _allKarts[i],
                    isCPU = _raceMode.IsVsCpu() && i > 0,
                    isFinished = false,
                    swipeCount = 0,
                    finishTimeMs = 0,
                    checkpointIndex = 0
                });
                // CPUPlayer is now added inside SpawnAdditionalKart (with per-kart event)
            }

            // Disable ghost recorder in multiplayer
            if (_ghostRecorder != null) _ghostRecorder.enabled = false;

            // Subscribe to turn events BEFORE starting the race
            _turnManager.OnTurnStart += HandleTurnStart;
            _turnManager.OnTurnEnd += HandleTurnEnd;
            // OnPlayerFinished fires when EACH kart crosses finish line
            _turnManager.OnPlayerFinished += (playerIndex, finishTimeMs) =>
            {
                if (!_finished)
                {
                    FinishRaceMultiplayer(playerIndex);
                }
            };

            // NOW start the race (OnTurnStart(0) fires, HandleTurnStart is listening)
            _turnManager.StartRace(playerStates, _trackDef, ConvertToTurnManagerRaceMode(_raceMode));

            // Pass & Play overlay
            if (_raceMode.IsPassPlay())
            {
                var overlayGo = new GameObject("PassPlayOverlay");
                _passPlayOverlay = overlayGo.AddComponent<UI.PassPlayOverlay>();
                var ppmGo = new GameObject("[PassPlayManager]");
                var ppm = ppmGo.AddComponent<Multiplayer.PassPlayManager>();
                SetField(ppm, "turnManager", _turnManager);
                SetField(ppm, "passOverlay", _passPlayOverlay);
            }

            // Initial turn: player 0 (TurnManager calls OnTurnStart for index 0)
            // Disable all SwipeDetectors except player 0
            SetActiveSwipeDetector(0);

            Debug.Log($"[RaceScene] Multiplayer setup complete: {playerCount} karts, TurnManager active.");

            // Add turn flow debugger for diagnostics
            var debuggerGo = new GameObject("[TurnFlowDebugger]");
            var debugger = debuggerGo.AddComponent<TurnFlowDebugger>();
            debugger.Initialize(_turnManager, _allKarts, _allSwipeDetectors);

            // Make all karts ignore collision with each other (they pass through, like ghosts)
            // Karts still collide with walls but not with each other.
            for (int i = 0; i < _allKarts.Count; i++)
            {
                var colA = _allKarts[i].GetComponent<Collider2D>();
                if (colA == null) continue;
                for (int j = i + 1; j < _allKarts.Count; j++)
                {
                    var colB = _allKarts[j].GetComponent<Collider2D>();
                    if (colB != null)
                    {
                        UnityEngine.Physics2D.IgnoreCollision(colA, colB, true);
                    }
                }
            }
            Debug.Log("[RaceScene] All karts set to ignore collision with each other.");
        }

        private void HandleTurnStart(int playerIndex)
        {
            _currentTurnPlayerIndex = playerIndex;
            string playerName = _turnManager.Players[playerIndex].playerName;
            Debug.Log($"[RaceScene] Turn started for player {playerIndex} ({playerName}).");

            // Enable the active player's SwipeDetector — BUT ONLY for human players.
            // CPU players use CPUPlayer.TakeTurn() which fires the swipe event directly.
            // If we enable the CPU's SwipeDetector, the human can swipe during the CPU's turn
            // and accidentally move the CPU's kart.
            bool isHuman = !_turnManager.Players[playerIndex].isCPU;
            SetActiveSwipeDetector(isHuman ? playerIndex : -1);

            // Camera follows the active kart
            if (_cameraRig != null && _allKarts.Count > playerIndex)
            {
                _cameraRig.target = _allKarts[playerIndex].transform;
            }

            // Update kart glow — only active kart glows
            for (int i = 0; i < _allKartGlows.Count; i++)
            {
                if (_allKartGlows[i] != null)
                {
                    _allKartGlows[i].SetActive(i == playerIndex);
                }
            }

            // Show turn banner
            if (_turnBanner != null)
            {
                _turnBanner.ShowTurnBanner(playerIndex, playerName, _isMultiplayer);
            }

            // Deactivate trail wall
            var trail = _allKarts[playerIndex]?.GetComponent<KartTrail>();
            if (trail != null && trail.IsTrailWallActive)
            {
                trail.DeactivateTrailWall();
            }
        }

        private void HandleTurnEnd(int playerIndex, int swipeCount)
        {
            Debug.Log($"[RaceScene] Turn ended for player {playerIndex} ({swipeCount} swipes total).");

            // Deactivate all kart glows
            foreach (var glow in _allKartGlows)
            {
                glow?.SetActive(false);
            }

            SetActiveSwipeDetector(-1);
        }

        private void SetActiveSwipeDetector(int activeIndex)
        {
            for (int i = 0; i < _allSwipeDetectors.Count; i++)
            {
                if (_allSwipeDetectors[i] != null)
                {
                    _allSwipeDetectors[i].enabled = (i == activeIndex);
                }
            }
        }

        private void SpawnAdditionalKart(int playerIndex)
        {
            // Spawn at same position as player (slightly offset)
            Vector3 spawnPos = _runtimeKartSpawn + new Vector3(0, playerIndex * 2f, 0);

            var kartGo = new GameObject($"Kart_{playerIndex}");
            kartGo.transform.position = spawnPos;
            kartGo.transform.localScale = Vector3.one;

            var sr = kartGo.AddComponent<SpriteRenderer>();
            // Different colors for different players
            Color[] kartColors = {
                new Color(1f, 0.82f, 0.4f),    // yellow (player 1 — already spawned)
                new Color(1f, 0.42f, 0.42f),    // red (player 2 / CPU)
                new Color(0.42f, 0.79f, 0.47f), // green (player 3)
                new Color(0.42f, 0.58f, 1f),    // blue (player 4)
            };
            sr.color = kartColors[playerIndex % kartColors.Length];
            sr.sprite = CreateSquareSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 1.5f);
            sr.sortingOrder = 10;

            var col = kartGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);  // Smaller hitbox (visual is 1.5, hitbox is 0.8 for tight navigation)

            var rb = kartGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var kart = kartGo.AddComponent<KartController>();

            // CRITICAL: Each kart gets its OWN SwipeEvent so swipes don't leak to other karts.
            var kartSwipeEvent = ScriptableObject.CreateInstance<Core.SwipeEvent>();
            _perKartSwipeEvents.Add(kartSwipeEvent);
            kart.SetSwipeEvent(kartSwipeEvent);

            // Per-player kart stats: in P&P mode, each player may have picked a different kart.
            // Fall back to the shared kartStats if no per-player selection exists.
            KartStats statsForThisKart = kartStats;
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.pendingKartSelections != null &&
                playerIndex < GameFlowManager.Instance.pendingKartSelections.Count)
            {
                var playerKart = GameFlowManager.Instance.pendingKartSelections[playerIndex];
                if (playerKart != null)
                {
                    statsForThisKart = playerKart;
                    Debug.Log($"[RaceScene] Using player {playerIndex}'s kart: {playerKart.displayName}");
                }
            }
            SetField(kart, "stats", statsForThisKart);

            // Also tint the kart's SpriteRenderer to the player's kart color
            var kartSr = kartGo.GetComponent<SpriteRenderer>();
            if (kartSr != null && statsForThisKart != null)
            {
                kartSr.color = statsForThisKart.kartColor;
            }

            // Add SwipeDetector (disabled by default — TurnManager enables it per turn)
            var sd = kartGo.AddComponent<SwipeDetector>();
            SetField(sd, "settings", inputSettings);
            SetField(sd, "swipeEvent", kartSwipeEvent);
            sd.enabled = false;
            _allSwipeDetectors.Add(sd);

            // Wire CPUPlayer to use this kart's event (not the shared one)
            if (_raceMode.IsVsCpu() || _raceMode.IsOnline())
            {
                var cpu = kartGo.AddComponent<Racing.CPUPlayer>();
                SetField(cpu, "swipeEvent", kartSwipeEvent);  // CPU raises THIS kart's event
                SetField(cpu, "kart", kart);

                var difficulty = _raceMode switch
                {
                    Core.RaceMode.VsCpuEasy => Racing.CPUPlayer.Difficulty.Easy,
                    Core.RaceMode.VsCpuMedium => Racing.CPUPlayer.Difficulty.Medium,
                    Core.RaceMode.VsCpuHard => Racing.CPUPlayer.Difficulty.Hard,
                    Core.RaceMode.Online => Racing.CPUPlayer.Difficulty.Medium,
                    _ => Racing.CPUPlayer.Difficulty.Medium
                };
                cpu.SetDifficulty(difficulty);
            }

            // Register this kart's event with a PER-KART handler (knows its own index)
            int kartIndex = playerIndex;  // capture for closure
            var handler = new Action<QuickChecks.Input.SwipeData>(swipe => OnKartSwipe(kartIndex, swipe));
            _perKartSwipeHandlers.Add(handler);
            kartSwipeEvent.Register(handler);

            // Stop-position history (for BlackHole rewind feature)
            kartGo.AddComponent<Racing.KartStopHistory>();

            // Add trail
            var trail = kartGo.AddComponent<KartTrail>();
            SetField(trail, "kart", kart);

            // Add neon glow for this kart (color matches player index)
            var glow = kartGo.AddComponent<Racing.KartGlow>();
            glow.SetGlowColor(PLAYER_COLORS[playerIndex % PLAYER_COLORS.Length]);
            _allKartGlows.Add(glow);

            // Add visual feedback (power-up tint + wall/gravel/black hole flash)
            kartGo.AddComponent<Racing.KartVisualFeedback>();

            _allKarts.Add(kart);

            // Add PowerUpSystem so this kart can collect power-ups (issue #9)
            var pus = kartGo.AddComponent<PowerUpSystem>();
            SetField(pus, "kart", kart);

            // Add CheckpointTracker for each kart + wire to TurnManager (issue #1)
            var ct = kartGo.AddComponent<CheckpointTracker>();
            if (_trackData != null)
            {
                var cpArray = _trackData.checkpoints.ToArray();
                var finishCol = _trackData.finishLine?.GetComponent<Collider2D>();
                ct.SetCheckpoints(cpArray, finishCol, _trackDef.lapCount);
                // Wire OnRaceComplete → TurnManager.NotifyPlayerFinished for this kart index
                int finishKartIndex = playerIndex;  // capture for closure
                ct.OnRaceComplete += (total) =>
                {
                    if (_turnManager != null)
                    {
                        _turnManager.NotifyPlayerFinished(finishKartIndex);
                    }
                };
            }

            Debug.Log($"[RaceScene] Spawned kart {playerIndex} at {spawnPos} (color: {sr.color}).");
        }

        private Racing.TurnManager.RaceMode ConvertToTurnManagerRaceMode(Core.RaceMode mode)
        {
            return mode switch
            {
                Core.RaceMode.VsCpuEasy => Racing.TurnManager.RaceMode.VsCpuEasy,
                Core.RaceMode.VsCpuMedium => Racing.TurnManager.RaceMode.VsCpuMedium,
                Core.RaceMode.VsCpuHard => Racing.TurnManager.RaceMode.VsCpuHard,
                Core.RaceMode.PassPlay2 => Racing.TurnManager.RaceMode.PassAndPlay,
                Core.RaceMode.PassPlay3 => Racing.TurnManager.RaceMode.PassAndPlay,
                Core.RaceMode.PassPlay4 => Racing.TurnManager.RaceMode.PassAndPlay,
                Core.RaceMode.Online => Racing.TurnManager.RaceMode.Online,
                _ => Racing.TurnManager.RaceMode.Solo
            };
        }

        private void FinishRaceMultiplayer(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _turnManager.Players.Count)
            {
                Debug.LogWarning("[RaceScene] Invalid player index in FinishRaceMultiplayer.");
                return;
            }

            var finisher = _turnManager.Players[playerIndex];
            Debug.Log($"[RaceScene] {finisher.playerName} finished! ({finisher.swipeCount} swipes)");

            // Show winner banner for this finisher
            if (_turnBanner != null)
            {
                _turnBanner.ShowTurnBanner(playerIndex, $"{finisher.playerName} FINISHES!", true);
            }

            // Count remaining players (not finished)
            int remainingCount = 0;
            foreach (var p in _turnManager.Players)
            {
                if (!p.isFinished) remainingCount++;
            }

            if (remainingCount == 0)
            {
                // Everyone finished — show results.
                ShowMultiplayerResults();
            }
            else if (_raceMode.IsVsCpu() || _raceMode.IsOnline())
            {
                // Vs CPU / Online: when the human finishes, the race ends immediately (per TurnManager rule).
                // The opponent stays as `isFinished=false` (didn't hit swipe cap, just got beaten) — we don't
                // mark them as DNF. RaceResultBuilder distinguishes finished / DNF / unfinished states.
                ShowMultiplayerResults();
            }
            else if (remainingCount == 1)
            {
                // Pass & Play: only one player left racing — auto-end.
                // Mark that last player as DNF (they can't catch up / chose not to finish).
                foreach (var p in _turnManager.Players)
                {
                    if (!p.isFinished)
                    {
                        p.isFinished = true;
                        p.finishTimeMs = -1;
                        Debug.Log($"[RaceScene] {p.playerName} classified as DNF (auto-end, last remaining).");
                    }
                }
                ShowMultiplayerResults();
            }
            else
            {
                // Multiple players remaining (P&P only) — show Continue / End Game prompt.
                ShowContinuePrompt();
            }
        }

        private void ShowContinuePrompt()
        {
            _finished = true;  // Pause normal race flow until player chooses

            // Build results so far
            string results = "RACE RESULTS SO FAR\n\n";
            var sorted = new List<Racing.TurnManager.PlayerState>(_turnManager.Players);
            sorted.Sort((a, b) =>
            {
                if (a.isFinished && !b.isFinished) return -1;
                if (!a.isFinished && b.isFinished) return 1;
                if (a.isFinished && b.isFinished)
                {
                    if (a.finishTimeMs < 0 && b.finishTimeMs >= 0) return 1;
                    if (b.finishTimeMs < 0 && a.finishTimeMs >= 0) return -1;
                    if (a.swipeCount != b.swipeCount) return a.swipeCount.CompareTo(b.swipeCount);
                    return a.finishTimeMs.CompareTo(b.finishTimeMs);
                }
                return 0;
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                if (p.isFinished && p.finishTimeMs > 0)
                    results += $"{i + 1}. {p.playerName}: {p.swipeCount} swipes\n";
                else if (p.isFinished)
                    results += $"{i + 1}. {p.playerName}: DNF\n";
                else
                    results += $"   {p.playerName}: still racing\n";
            }

            Debug.Log($"[RaceScene] === RACE RESULTS SO FAR ===\n{results}");
            Debug.Log("[RaceScene] Showing Continue / End Game prompt.");

            // Create Continue + End Game buttons dynamically
            CreateContinueEndButtons(results);
        }

        private void CreateContinueEndButtons(string resultsText)
        {
            // Find canvas
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // Create results panel
            var panelGo = new GameObject("MP_ResultsPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.08f, 0.08f, 0.95f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(700, 500);

            // Results text
            var textGo = new GameObject("ResultsText");
            textGo.transform.SetParent(panelGo.transform, false);
            TMP_Text resultsTMP = null;
            try
            {
                resultsTMP = textGo.AddComponent<TextMeshProUGUI>();
                resultsTMP.text = resultsText + "\nContinue racing or end the game?";
                resultsTMP.fontSize = 32;
                resultsTMP.alignment = TextAlignmentOptions.Center;
                resultsTMP.color = Color.white;
            }
            catch
            {
                var legacy = textGo.AddComponent<Text>();
                legacy.text = resultsText + "\nContinue racing or end the game?";
                legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacy.fontSize = 24;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.color = Color.white;
            }
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.7f);
            textRect.anchorMax = new Vector2(0.5f, 0.7f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(650, 300);

            // Continue button
            var continueGo = new GameObject("ContinueButton");
            continueGo.transform.SetParent(panelGo.transform, false);
            var continueBtn = continueGo.AddComponent<Button>();
            var continueImg = continueGo.AddComponent<Image>();
            continueImg.color = new Color(0.247f, 0.878f, 0.760f, 0.9f);
            continueImg.raycastTarget = true;
            continueBtn.targetGraphic = continueImg;
            var contRect = continueGo.GetComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.3f, 0.15f);
            contRect.anchorMax = new Vector2(0.3f, 0.15f);
            contRect.pivot = new Vector2(0.5f, 0.5f);
            contRect.sizeDelta = new Vector2(250, 80);

            var contLabelGo = new GameObject("Label");
            contLabelGo.transform.SetParent(continueGo.transform, false);
            TMP_Text contLabel = null;
            try
            {
                contLabel = contLabelGo.AddComponent<TextMeshProUGUI>();
                contLabel.text = "CONTINUE";
                contLabel.fontSize = 32;
                contLabel.alignment = TextAlignmentOptions.Center;
                contLabel.color = new Color(0.05f, 0.08f, 0.08f);
                contLabel.raycastTarget = false;
            }
            catch
            {
                var legacy = contLabelGo.AddComponent<Text>();
                legacy.text = "CONTINUE";
                legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacy.fontSize = 24;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.color = new Color(0.05f, 0.08f, 0.08f);
                legacy.raycastTarget = false;
                contLabel = (TMP_Text)(object)legacy;
            }
            var clRect = contLabelGo.GetComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero; clRect.anchorMax = Vector2.one;
            clRect.pivot = new Vector2(0.5f, 0.5f);
            clRect.offsetMin = Vector2.zero; clRect.offsetMax = Vector2.zero;

            continueBtn.onClick.AddListener(() =>
            {
                Destroy(panelGo);
                _finished = false;  // Resume race
                // Winner is already marked as finished — TurnManager will skip them
                Debug.Log("[RaceScene] Continue selected — remaining players continue racing.");
            });

            // End Game button
            var endGo = new GameObject("EndGameButton");
            endGo.transform.SetParent(panelGo.transform, false);
            var endBtn = endGo.AddComponent<Button>();
            var endImg = endGo.AddComponent<Image>();
            endImg.color = new Color(1f, 0.42f, 0.42f, 0.9f);
            endImg.raycastTarget = true;
            endBtn.targetGraphic = endImg;
            var endRect = endGo.GetComponent<RectTransform>();
            endRect.anchorMin = new Vector2(0.7f, 0.15f);
            endRect.anchorMax = new Vector2(0.7f, 0.15f);
            endRect.pivot = new Vector2(0.5f, 0.5f);
            endRect.sizeDelta = new Vector2(250, 80);

            var endLabelGo = new GameObject("Label");
            endLabelGo.transform.SetParent(endGo.transform, false);
            TMP_Text endLabel = null;
            try
            {
                endLabel = endLabelGo.AddComponent<TextMeshProUGUI>();
                endLabel.text = "END GAME";
                endLabel.fontSize = 32;
                endLabel.alignment = TextAlignmentOptions.Center;
                endLabel.color = Color.white;
                endLabel.raycastTarget = false;
            }
            catch
            {
                var legacy = endLabelGo.AddComponent<Text>();
                legacy.text = "END GAME";
                legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacy.fontSize = 24;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.color = Color.white;
                legacy.raycastTarget = false;
                endLabel = (TMP_Text)(object)legacy;
            }
            var elRect = endLabelGo.GetComponent<RectTransform>();
            elRect.anchorMin = Vector2.zero; elRect.anchorMax = Vector2.one;
            elRect.pivot = new Vector2(0.5f, 0.5f);
            elRect.offsetMin = Vector2.zero; elRect.offsetMax = Vector2.zero;

            endBtn.onClick.AddListener(() =>
            {
                Destroy(panelGo);
                // Classify all remaining as DNF
                foreach (var p in _turnManager.Players)
                {
                    if (!p.isFinished)
                    {
                        p.isFinished = true;
                        p.finishTimeMs = -1;
                        Debug.Log($"[RaceScene] {p.playerName} classified as DNF (End Game).");
                    }
                }
                ShowMultiplayerResults();
            });
        }

        private void ShowMultiplayerResults()
        {
            _finished = true;

            // Phase 2: build the structured payload and route through the Results scene.
            // The in-scene text-dump + auto-reset is replaced by a real scene transition so the
            // neon results UI can render properly for every mode.
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var results = RaceResultBuilder.Build(
                mode: _raceMode,
                track: _trackDef,
                raceStartMs: _raceStartMs,
                raceEndMs: nowMs,
                turnManager: _turnManager,
                soloGhost: null,
                isNewBest: false,
                prevBestSwipes: 0,
                prevBestTimeMs: 0);

            // Log a readable summary (replaces the old Debug.Log text dump).
            Debug.Log($"[RaceScene] === FINAL RESULTS ===\n{FormatResultsSummary(results)}");

            // SFX
            if (QuickChecks.Audio.SfxPlayer.Instance != null)
            {
                if (results.humanDnf || (!results.humanWon && results.IsMultiplayer))
                    QuickChecks.Audio.SfxPlayer.Instance.PlayRaceFailed();
                else
                    QuickChecks.Audio.SfxPlayer.Instance.PlayRaceComplete();
            }

            // Transition to the Results scene after a short delay so the winner banner has time to show.
            // If a winner banner is currently displayed, give it ~2s before transitioning.
            _autoResetTimer = 2.5f;
            _pendingResultsPayload = results;
        }

        /// <summary>Holds the results payload between ShowMultiplayerResults() and the actual scene transition.</summary>
        private RaceResultData _pendingResultsPayload;

        private static string FormatResultsSummary(RaceResultData r)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Mode: {r.mode.DisplayName()}  Track: {r.trackDisplayName}  Par: {r.parSwipes}");
            foreach (var p in r.players)
            {
                string outcome = p.isDnf ? "DNF" :
                                 p.rank > 0 ? $"#{p.rank}" : "—";
                sb.AppendLine($"  {outcome}  {p.playerName}  swipes={p.SwipesDisplay}  time={p.TimeDisplay}" +
                              $"{(p.isHuman ? "  [YOU]" : "")}");
            }
            if (r.winnerIndex >= 0 && r.winnerIndex < r.players.Count)
                sb.AppendLine($"Winner: {r.players[r.winnerIndex].playerName}");
            return sb.ToString();
        }

        private void StartRace()
        {
            _raceStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _finished = false;
            _swipeCount = 0;

            // Play race music
            if (QuickChecks.Audio.MusicPlayer.Instance != null)
            {
                QuickChecks.Audio.MusicPlayer.Instance.PlayRaceMusic();
            }

            if (!_isMultiplayer)
            {
                // Solo: use shared swipeEvent
                _swipeDetector.SetRaceStartTime(_raceStartMs);
                swipeEvent.Register(OnSwipe);

                // Record ghost + spawn ghost replay
                _ghostRecorder.BeginRace(_trackDef.trackId, "kart_starter");
                Vector3 spawn = _runtimeKartSpawn != Vector3.zero ? _runtimeKartSpawn : Vector3.zero;
                _soloGhost?.OnRaceStart(_raceStartMs, spawn);
                Debug.Log($"[RaceScene] Solo race started on track '{_trackDef.trackId}'. Par = {_trackDef.parSwipeCount} swipes.");
            }
            else
            {
                // Multiplayer: OnSwipe is already registered on per-kart events in SetupMultiplayer.
                // TurnManager.OnTurnStart was already called for player 0 in SetupMultiplayer.
                Debug.Log($"[RaceScene] Multiplayer race started: {_raceMode.DisplayName()}. Par = {_trackDef.parSwipeCount} swipes.");
            }
        }

        /// <summary>
        /// Per-kart swipe handler. Each kart has its own handler that knows its player index.
        /// This prevents the race condition where OnSwipe used _currentTurnPlayerIndex
        /// which might not have been updated yet.
        /// </summary>
        private void OnKartSwipe(int playerIndex, QuickChecks.Input.SwipeData swipe)
        {
            _swipeCount++;

            if (_isMultiplayer && _turnManager != null)
            {
                Debug.Log($"[RaceScene] OnKartSwipe: player={playerIndex}, currentTurn={_currentTurnPlayerIndex}, " +
                          $"phase={_turnManager.Phase}, mag={swipe.magnitude:F0}");

                // Only register the swipe if it's actually this player's turn
                if (playerIndex == _turnManager.CurrentPlayerIndex)
                {
                    _turnManager.RegisterSwipe(playerIndex);

                    // Mark this kart as "was moving"
                    if (_wasKartMoving != null && playerIndex < _wasKartMoving.Length)
                    {
                        _wasKartMoving[playerIndex] = true;
                        if (_kartStopDelay != null) _kartStopDelay[playerIndex] = 0.3f;
                    }

                    // Notify the kart's stop-history that a swipe fired (for BlackHole rewind)
                    var kartForHistory = _allKarts[playerIndex];
                    if (kartForHistory != null)
                    {
                        kartForHistory.GetComponent<Racing.KartStopHistory>()?.NotifySwipe();
                    }

                    // Check trail wall pending
                    var kart = _allKarts[playerIndex];
                    if (kart != null && kart.ConsumeTrailWallPending())
                    {
                        var trail = kart.GetComponent<KartTrail>();
                        if (trail != null)
                        {
                            trail.ActivateTrailWall();
                            Debug.Log($"[RaceScene] Trail wall activated for player {playerIndex}.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[RaceScene] Swipe from player {playerIndex} but it's player {_turnManager.CurrentPlayerIndex}'s turn — IGNORED.");
                }
            }
        }

        private void OnSwipe(SwipeData swipe)
        {
            // Solo mode only
            _swipeCount++;
            // Notify the stop-history so it knows the next stop should be recorded (for BlackHole rewind)
            _kart?.GetComponent<Racing.KartStopHistory>()?.NotifySwipe();
        }

        private void Update()
        {
            // Auto-reset countdown
            if (_autoResetTimer > 0f)
            {
                _autoResetTimer -= Time.deltaTime;
                if (_autoResetTimer <= 0f)
                {
                    _autoResetTimer = -1f;

                    // Phase 2: if we have a pending multiplayer results payload, transition to Results scene.
                    if (_pendingResultsPayload != null)
                    {
                        var payload = _pendingResultsPayload;
                        _pendingResultsPayload = null;
                        if (GameFlowManager.Instance != null)
                        {
                            GameFlowManager.Instance.GoToResults(payload);
                        }
                        else
                        {
                            UnityEngine.SceneManagement.SceneManager.LoadScene("Results");
                        }
                        return;
                    }

                    if (_isMultiplayer)
                    {
                        // Multiplayer in-scene reset (only used if no payload was set — e.g., explicit Reset button)
                        ResetRace();
                    }
                    else
                    {
                        TransitionToResults();
                    }
                    return;
                }
            }

            // Multiplayer: detect when the CURRENT player's kart stops moving
            if (_isMultiplayer && _turnManager != null && _wasKartMoving != null)
            {
                int currentIdx = _turnManager.CurrentPlayerIndex;
                if (currentIdx >= 0 && currentIdx < _allKarts.Count && _allKarts[currentIdx] != null)
                {
                    // Only check for stopped if the kart was actually moving at some point.
                    // _wasKartMoving is set to true in OnKartSwipe when a swipe fires.
                    // We also set _kartStopDelay to 0.3s there — this delay gives physics
                    // time to apply the impulse before we check if the kart stopped.
                    if (_wasKartMoving[currentIdx])
                    {
                        // Check if the kart is currently stopped (velocity below stopThreshold)
                        bool isStopped = _allKarts[currentIdx].IsStopped;

                        if (!isStopped)
                        {
                            // Kart is still moving — reset the stop delay
                            if (_kartStopDelay != null && currentIdx < _kartStopDelay.Length)
                                _kartStopDelay[currentIdx] = 0.3f;
                        }
                        else
                        {
                            // Kart appears stopped — count down the delay
                            // This prevents false triggers from the frame gap between
                            // swipe registration and physics impulse application.
                            if (_kartStopDelay != null && currentIdx < _kartStopDelay.Length && _kartStopDelay[currentIdx] > 0f)
                            {
                                _kartStopDelay[currentIdx] -= Time.deltaTime;
                            }
                            else
                            {
                                // Confirmed stopped for 0.3s — end turn
                                Debug.Log($"[RaceScene] Kart {currentIdx} stopped — notifying TurnManager. " +
                                          $"Phase={_turnManager.Phase}, vel={_allKarts[currentIdx].Velocity.magnitude:F1}");
                                _wasKartMoving[currentIdx] = false;
                                _turnManager.NotifyKartStopped(currentIdx);
                            }
                        }
                    }
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
                // Only unlock the next track if this is a real (non-custom) track — custom
                // tracks have trackIndex == -1 and shouldn't affect progression.
                if (isNewBest && _trackDef.trackIndex >= 0)
                {
                    GameFlowManager.Instance.profile?.UnlockNextTrack(_trackDef.trackIndex);
                }
            }

            // Phase 2 Supabase: auto-upload new best to global leaderboard (Solo only).
            // We upload every new best (Decision: auto-upload). Failed uploads are non-fatal —
            // the player still gets their local result + ghost. The ghost JSON is also uploaded
            // so other players can replay it from the leaderboard.
            //
            // Skip the upload if this race was started with an external ghost (e.g. a
            // leaderboard replay) — we don't want to overwrite the player's local best with
            // a worse run, and we definitely don't want to upload someone else's ghost back
            // to the leaderboard.
            bool wasExternalGhost = GameFlowManager.Instance?.pendingExternalGhost != null;
            if (_raceMode.IsSolo() && isNewBest && !wasExternalGhost &&
                !string.IsNullOrEmpty(_trackDef.trackId))
            {
                UploadToLeaderboard(finishMs);
            }

            // Clear the external ghost after the race — future races use the player's
            // local best again (unless the player taps 'Replay' on another leaderboard row).
            if (wasExternalGhost && GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.pendingExternalGhost = null;
                GameFlowManager.Instance.pendingExternalTrackId = null;
                Debug.Log("[RaceScene] External ghost cleared after race.");
            }

            // Phase 2: build the full results payload and route through the new path.
            // We synthesise a 1-entry TurnManager-style snapshot so RaceResultBuilder stays single-source-of-truth.
            // Auto-transition runs after the short display delay (lets the kart settle before scene change).
            _autoResetTimer = resultsDisplayDuration;
            Debug.Log($"[RaceScene] Race finished! Swipes: {_swipeCount}, Time: {finishMs / 1000f:F2}s. " +
                      $"Auto-transition to results in {resultsDisplayDuration}s.");

            // Play race-complete SFX
            if (QuickChecks.Audio.SfxPlayer.Instance != null)
            {
                QuickChecks.Audio.SfxPlayer.Instance.PlayRaceComplete();
            }

            // Juice: slow-motion on finish + celebration burst + haptic
            if (Racing.JuiceManager.Instance != null)
            {
                Racing.JuiceManager.Instance.SlowMo(0.3f, 0.5f);  // 30% speed, restore over 0.5s
                Racing.JuiceManager.Instance.HapticFinish();
                Racing.JuiceManager.Instance.Burst(_kart.transform.position,
                    new Color(0.247f, 0.878f, 0.760f), 16, 5f, 0.5f);
                Racing.JuiceManager.Instance.Shake(0.3f, 0.2f);
            }
        }

        /// <summary>
        /// Uploads the player's new best race result + ghost to the Supabase leaderboard.
        /// Fire-and-forget: any failure is logged but does not block the Results scene transition.
        /// </summary>
        private void UploadToLeaderboard(long finishMs)
        {
            var leaderboard = Multiplayer.SupabaseLeaderboard.Instance;
            if (leaderboard == null)
            {
                Debug.LogWarning("[RaceScene] SupabaseLeaderboard not initialised — skipping upload.");
                return;
            }

            string kartId = kartStats != null ? kartStats.kartId : "kart_starter";

            // Serialize the ghost data to JSON for upload (so other players can replay top runs).
            string ghostJson = null;
            if (_ghostRecorder != null && _ghostRecorder.CurrentData != null)
            {
                try
                {
                    ghostJson = Newtonsoft.Json.JsonConvert.SerializeObject(_ghostRecorder.CurrentData);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[RaceScene] Failed to serialize ghost: {ex.Message}");
                }
            }

            Debug.Log($"[RaceScene] Uploading new best to Supabase: track={_trackDef.trackId}, " +
                      $"swipes={_swipeCount}, time={finishMs / 1000f:F2}s");
            leaderboard.UploadRaceResult(
                trackId: _trackDef.trackId,
                kartId: kartId,
                swipeCount: _swipeCount,
                finishTimeMs: finishMs,
                isDnf: false,
                ghostJson: ghostJson);
        }

        private void TransitionToResults()
        {
            int bestSwipes = _soloGhost?.BestGhost?.swipeCount ?? _swipeCount;
            long bestTime = _soloGhost?.BestGhost?.finishTimeMs ?? 0;

            if (GameFlowManager.Instance != null)
            {
                bool isNewBest = _swipeCount < bestSwipes ||
                    (_swipeCount == bestSwipes && (_soloGhost?.BestGhost?.finishTimeMs ?? long.MaxValue) < bestTime);

                // Phase 2: build a full RaceResultData payload and route through the unified path.
                // For Solo we synthesise a 1-entry TurnManager-style snapshot via RaceResultBuilder,
                // passing the ghost info (isNewBest, prevBest) directly.
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var soloPlayerStates = new List<Racing.TurnManager.PlayerState>
                {
                    new Racing.TurnManager.PlayerState
                    {
                        playerName = "You",
                        kart = _kart,
                        isCPU = false,
                        isFinished = true,
                        swipeCount = _swipeCount,
                        finishTimeMs = nowMs - _raceStartMs,
                        checkpointIndex = 0
                    }
                };

                // Build a transient TurnManager-like snapshot without spawning a real component.
                // RaceResultBuilder.Build() reads from TurnManager.Players; we synthesise a fake.
                var fakeTm = new GameObject("[SoloResultsTurnManager]");
                var tm = fakeTm.AddComponent<Racing.TurnManager>();
                tm.SetPlayersForResults(soloPlayerStates);

                var results = RaceResultBuilder.Build(
                    mode: _raceMode,
                    track: _trackDef,
                    raceStartMs: _raceStartMs,
                    raceEndMs: nowMs,
                    turnManager: tm,
                    soloGhost: _soloGhost,
                    isNewBest: isNewBest,
                    prevBestSwipes: bestSwipes,
                    prevBestTimeMs: bestTime);

                Destroy(fakeTm);

                GameFlowManager.Instance.GoToResults(results);
            }
            else
            {
                ResetRace();
            }
        }

        public void ResetRace()
        {
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
            _finished = false;
            _autoResetTimer = -1f;
            _swipeCount = 0;

            if (_isMultiplayer)
            {
                // Reset ALL karts to spawn positions + clear state
                for (int i = 0; i < _allKarts.Count; i++)
                {
                    if (_allKarts[i] == null) continue;

                    // Each kart respawns at the same spawn point
                    _allKarts[i].RespawnAt(_runtimeKartSpawn);

                    // Reset checkpoint tracker
                    var ct = _allKarts[i].GetComponent<CheckpointTracker>();
                    ct?.Reset();

                    // Clear power-ups
                    var pus = _allKarts[i].GetComponent<PowerUpSystem>();
                    pus?.Clear();

                    // Reset stop-position history (for BlackHole rewind feature)
                    var stopHistory = _allKarts[i].GetComponent<Racing.KartStopHistory>();
                    stopHistory?.Clear();

                    // Reset wasKartMoving
                    if (_wasKartMoving != null && i < _wasKartMoving.Length)
                        _wasKartMoving[i] = false;
                    if (_kartStopDelay != null && i < _kartStopDelay.Length)
                        _kartStopDelay[i] = 0f;
                }

                // Reset TurnManager state
                if (_turnManager != null && _trackDef != null)
                {
                    foreach (var p in _turnManager.Players)
                    {
                        p.isFinished = false;
                        p.swipeCount = 0;
                        p.finishTimeMs = 0;
                        p.checkpointIndex = 0;
                    }
                    _turnManager.StartRace(
                        new List<Racing.TurnManager.PlayerState>(_turnManager.Players),
                        _trackDef, ConvertToTurnManagerRaceMode(_raceMode));
                    Debug.Log("[RaceScene] TurnManager restarted after reset.");
                }
            }
            else
            {
                if (_kart != null) _kart.RespawnAt(_runtimeKartSpawn);
                // Reset solo kart's stop-position history
                _kart?.GetComponent<Racing.KartStopHistory>()?.Clear();
            }

            // Reset checkpoint visuals
            if (_trackData?.checkpoints != null)
                foreach (var cp in _trackData.checkpoints) cp?.ResetVisual();

            // Reset zoom
            if (_cameraRig != null && _trackDef != null)
                _cameraRig.ResetToDefault(_trackDef.defaultZoom);

            StartRace();
            Debug.Log("[RaceScene] Race reset complete.");
        }

        private void OnDisable()
        {
            // Cleanup per-kart SwipeEvent subscriptions
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
            for (int i = 0; i < _perKartSwipeEvents.Count; i++)
            {
                if (_perKartSwipeEvents[i] != null && i < _perKartSwipeHandlers.Count && _perKartSwipeHandlers[i] != null)
                {
                    _perKartSwipeEvents[i].Unregister(_perKartSwipeHandlers[i]);
                }
                if (_perKartSwipeEvents[i] != null) Destroy(_perKartSwipeEvents[i]);
            }
            _perKartSwipeEvents.Clear();
            _perKartSwipeHandlers.Clear();
            // Unsubscribe from TurnManager events
            if (_turnManager != null)
            {
                _turnManager.OnTurnStart -= HandleTurnStart;
                _turnManager.OnTurnEnd -= HandleTurnEnd;
            }
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
