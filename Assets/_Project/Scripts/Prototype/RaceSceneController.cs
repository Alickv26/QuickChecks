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
            var glow = kartGo.AddComponent<Racing.KartGlow>();  // Neon glow for active kart
            glow.SetGlowColor(PLAYER_COLORS[0]);  // Player 1 = cyan
            _allKartGlows.Add(glow);
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
        }

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

            // Start TurnManager
            _turnManager.StartRace(playerStates, _trackDef, ConvertToTurnManagerRaceMode(_raceMode));

            // Subscribe to turn events
            _turnManager.OnTurnStart += HandleTurnStart;
            _turnManager.OnTurnEnd += HandleTurnEnd;
            _turnManager.OnRaceFinished += (winnerIndex) =>
            {
                if (!_finished) FinishRaceMultiplayer(winnerIndex);
            };

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

            // Enable the active player's SwipeDetector
            SetActiveSwipeDetector(playerIndex);

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
            col.size = new Vector2(1.5f, 1.5f);

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
            SetField(kart, "stats", kartStats);

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

            // Add trail
            var trail = kartGo.AddComponent<KartTrail>();
            SetField(trail, "kart", kart);

            // Add neon glow for this kart (color matches player index)
            var glow = kartGo.AddComponent<Racing.KartGlow>();
            glow.SetGlowColor(PLAYER_COLORS[playerIndex % PLAYER_COLORS.Length]);
            _allKartGlows.Add(glow);

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

        private void FinishRaceMultiplayer(int winnerIndex)
        {
            _finished = true;
            long finishMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartMs;

            // Guard against all-DNF (winnerIndex = -1)
            if (winnerIndex < 0 || winnerIndex >= _turnManager.Players.Count)
            {
                Debug.LogWarning("[RaceScene] Multiplayer race ended — no winner (all DNF).");
                _autoResetTimer = 3f;
                return;
            }

            var winner = _turnManager.Players[winnerIndex];
            Debug.Log($"[RaceScene] Multiplayer race finished! Winner: {winner.playerName} " +
                      $"({winner.swipeCount} swipes, {finishMs / 1000f:F2}s)");

            // Record results in player profile (issue #23)
            if (GameFlowManager.Instance != null)
            {
                for (int i = 0; i < _turnManager.Players.Count; i++)
                {
                    var p = _turnManager.Players[i];
                    if (!p.isFinished || p.finishTimeMs < 0) continue;
                    GameFlowManager.Instance.OnTrackCompleted(_trackDef.trackId, p.swipeCount, p.finishTimeMs);
                }
            }

            _autoResetTimer = 3f;
        }

        private void StartRace()
        {
            _raceStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _finished = false;
            _swipeCount = 0;

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
                    if (_isMultiplayer)
                    {
                        // Multiplayer: just reset (no results scene for now)
                        ResetRace();
                    }
                    else
                    {
                        TransitionToResults();
                    }
                    return;
                }
            }

            // Multiplayer: detect when karts stop moving → notify TurnManager
            if (_isMultiplayer && _turnManager != null && _wasKartMoving != null)
            {
                // Only check the CURRENT player's kart — ignore other karts
                int currentIdx = _turnManager.CurrentPlayerIndex;
                if (currentIdx >= 0 && currentIdx < _allKarts.Count && _allKarts[currentIdx] != null)
                {
                    bool isMoving = _allKarts[currentIdx].Velocity.magnitude > 10f;

                    if (isMoving)
                    {
                        _wasKartMoving[currentIdx] = true;
                        if (_kartStopDelay != null && currentIdx < _kartStopDelay.Length)
                            _kartStopDelay[currentIdx] = 0.3f;
                    }
                    else if (_wasKartMoving[currentIdx])
                    {
                        if (_kartStopDelay != null && currentIdx < _kartStopDelay.Length && _kartStopDelay[currentIdx] > 0f)
                        {
                            _kartStopDelay[currentIdx] -= Time.deltaTime;
                        }
                        else
                        {
                            Debug.Log($"[RaceScene] Kart {currentIdx} (current player) stopped — notifying TurnManager. " +
                                      $"Phase={_turnManager.Phase}");
                            _wasKartMoving[currentIdx] = false;
                            _turnManager.NotifyKartStopped(currentIdx);
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

            if (_isMultiplayer)
            {
                // Reset ALL karts + their checkpoint trackers + power-up systems
                foreach (var k in _allKarts)
                {
                    if (k != null) k.RespawnAt(_runtimeKartSpawn);
                    k?.GetComponent<CheckpointTracker>()?.Reset();
                    k?.GetComponent<PowerUpSystem>()?.Clear();
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
                }
            }
            else
            {
                if (_kart != null) _kart.RespawnAt(_runtimeKartSpawn);
            }
            // Reset checkpoint visuals
            if (_trackData?.checkpoints != null)
                foreach (var cp in _trackData.checkpoints) cp?.ResetVisual();
            if (_cameraRig != null && _trackDef != null)
                _cameraRig.ResetToDefault(_trackDef.defaultZoom);
            StartRace();
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
