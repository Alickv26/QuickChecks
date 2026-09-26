using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Input;
using QuickChecks.Racing;
using QuickChecks.Ghost;
using QuickChecks.Core;
using QuickChecks.Camera;
using QuickChecks.Audio;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// Week 1 prototype (polished): validates swipe feel + audio feedback + ghost replay
    /// + turn-based CPU opponent in a single scene that builds itself at runtime.
    ///
    /// Track layout:
    ///   - Main arena 40x20 with 4 walls
    ///   - 3 obstacles in the middle forcing direction variety (not just straight L→R)
    ///   - Finish line on the right
    ///
    /// Features added in polish pass:
    ///   - SwipeFeedback: kart squash + particle burst on each swipe
    ///   - KartTrail: fading path behind kart (visualizes swipe efficiency)
    ///   - SwipeAudio: synthesized whoosh pitched by swipe velocity
    ///   - SoloGhostPlayer: records + replays best run alongside current attempt
    ///   - Race results screen with par comparison
    /// </summary>
    public class PrototypeBootstrapper : MonoBehaviour
    {
        [Header("Track Config")]
        [SerializeField] private Vector2 trackSize = new Vector2(40, 20);
        [SerializeField] private Vector3 kartSpawn = new Vector3(-15, 0, 0);
        [SerializeField] private float finishLineX = 15f;

        [Header("Obstacles (forces direction variety)")]
        [SerializeField] private Vector2[] obstaclePositions =
        {
            new Vector2(-5, 0),    // Center obstacle — must go around
            new Vector2(0, 5),     // Upper barrier
            new Vector2(0, -5),    // Lower barrier (creates chicane with above)
            new Vector2(5, 0),     // Final center obstacle before finish
        };
        [SerializeField] private Vector2[] obstacleSizes =
        {
            new Vector2(3, 3),
            new Vector2(3, 8),
            new Vector2(3, 8),
            new Vector2(3, 3),
        };

        [Header("Tuning")]
        [SerializeField] private InputSettings inputSettings;
        [SerializeField] private GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private GameEventSO<SwipeData> ghostSwipeEvent; // separate event for ghost kart
        [SerializeField] private KartStats kartStats;
        [SerializeField] private KartStats ghostKartStats;

        [Header("Race Targets")]
        [SerializeField] private int parSwipes = 5; // theoretical min for this track layout

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging = true;

        // Built at runtime
        private KartController _kart;
        private SwipeDetector _swipeDetector;
        private GhostRecorder _ghostRecorder;
        private CameraRig _cameraRig;
        private PrototypeHUD _hud;
        private KartTrail _kartTrail;
        private SwipeFeedback _swipeFeedback;
        private SwipeAudio _swipeAudio;
        private SoloGhostPlayer _soloGhost;

        // Race state
        private long _raceStartMs;
        private int _swipeCount;
        private bool _finished;

        // Public accessors for HUD
        public int SwipeCount => _swipeCount;
        public KartController Kart => _kart;
        public float FinishLineX => finishLineX;
        public bool IsFinished => _finished;
        public int ParSwipes => parSwipes;
        public SoloGhostPlayer SoloGhost => _soloGhost;

        public event Action<int, long> OnRaceFinished;

        private void Awake()
        {
            EnsureAssets();
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
                if (verboseLogging) Debug.Log("[Prototype] Created in-memory InputSettings");
            }
            if (swipeEvent == null)
            {
                swipeEvent = ScriptableObject.CreateInstance<GameEventSO<SwipeData>>();
                if (verboseLogging) Debug.Log("[Prototype] Created in-memory SwipeEvent");
            }
            if (ghostSwipeEvent == null)
            {
                ghostSwipeEvent = ScriptableObject.CreateInstance<GameEventSO<SwipeData>>();
                if (verboseLogging) Debug.Log("[Prototype] Created in-memory GhostSwipeEvent");
            }
            if (kartStats == null)
            {
                kartStats = ScriptableObject.CreateInstance<KartStats>();
                kartStats.kartId = "kart_starter";
                kartStats.displayName = "Starter";
            }
            if (ghostKartStats == null)
            {
                ghostKartStats = ScriptableObject.CreateInstance<KartStats>();
                ghostKartStats.kartId = "kart_ghost";
                ghostKartStats.displayName = "Ghost";
            }
        }

        private void BuildScene()
        {
            // ----- Camera + CameraRig -----
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f); // #0E1414
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            _cameraRig = camGo.AddComponent<CameraRig>();
            SetPrivateField(_cameraRig, "inputSettings", inputSettings);

            // ----- Track walls -----
            var trackGo = new GameObject("Track");
            var wallColor = new Color(0.247f, 0.878f, 0.760f, 0.3f);
            CreateWall(trackGo.transform, "Wall_Top",    new Vector2(0,  trackSize.y / 2), new Vector2(trackSize.x + 2, 1), wallColor);
            CreateWall(trackGo.transform, "Wall_Bottom", new Vector2(0, -trackSize.y / 2), new Vector2(trackSize.x + 2, 1), wallColor);
            CreateWall(trackGo.transform, "Wall_Left",   new Vector2(-trackSize.x / 2, 0), new Vector2(1, trackSize.y), wallColor);
            CreateWall(trackGo.transform, "Wall_Right",  new Vector2( trackSize.x / 2, 0), new Vector2(1, trackSize.y), wallColor);

            // ----- Obstacles (force direction variety) -----
            var obstacleColor = new Color(0.247f, 0.878f, 0.760f, 0.5f);
            for (int i = 0; i < obstaclePositions.Length; i++)
            {
                CreateWall(trackGo.transform, $"Obstacle_{i}", obstaclePositions[i], obstacleSizes[i], obstacleColor);
            }

            // ----- Finish line -----
            var finishGo = new GameObject("FinishLine");
            finishGo.transform.SetParent(trackGo.transform);
            finishGo.transform.position = new Vector3(finishLineX, 0, 0);
            var finishSpriteRenderer = finishGo.AddComponent<SpriteRenderer>();
            finishSpriteRenderer.color = new Color(0.247f, 0.878f, 0.760f, 0.4f);
            finishSpriteRenderer.sprite = CreateSquareSprite();
            finishGo.transform.localScale = new Vector3(0.5f, trackSize.y, 1);
            var finishCollider = finishGo.AddComponent<BoxCollider2D>();
            finishCollider.isTrigger = true;
            finishCollider.size = new Vector2(0.5f, trackSize.y);
            finishGo.tag = "FinishLine";

            // ----- Player kart with all components -----
            var kartGo = new GameObject("PlayerKart");
            kartGo.transform.position = kartSpawn;
            var kartSpriteRenderer = kartGo.AddComponent<SpriteRenderer>();
            kartSpriteRenderer.color = new Color(1f, 0.82f, 0.4f); // #FFD166 yellow
            kartSpriteRenderer.sprite = CreateSquareSprite();
            kartSpriteRenderer.sortingOrder = 10;
            kartGo.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            kartGo.tag = "Kart";
            int kartLayer = LayerMask.NameToLayer("Kart");
            if (kartLayer >= 0) kartGo.layer = kartLayer;

            var kartRb = kartGo.AddComponent<Rigidbody2D>();
            _kart = kartGo.AddComponent<KartController>();
            _swipeDetector = kartGo.AddComponent<SwipeDetector>();
            _ghostRecorder = kartGo.AddComponent<GhostRecorder>();
            _kartTrail = kartGo.AddComponent<KartTrail>();
            _swipeFeedback = kartGo.AddComponent<SwipeFeedback>();
            _swipeAudio = kartGo.AddComponent<SwipeAudio>();
            kartGo.AddComponent<AudioSource>(); // Required by SwipeAudio

            // Wire up [SerializeField] private fields via reflection.
            SetPrivateField(_kart, "stats", kartStats);
            SetPrivateField(_kart, "swipeEvent", swipeEvent);

            SetPrivateField(_swipeDetector, "settings", inputSettings);
            SetPrivateField(_swipeDetector, "swipeEvent", swipeEvent);

            SetPrivateField(_ghostRecorder, "swipeEvent", swipeEvent);

            SetPrivateField(_kartTrail, "kart", _kart);

            SetPrivateField(_swipeFeedback, "swipeEvent", swipeEvent);
            SetPrivateField(_swipeFeedback, "kart", _kart);

            SetPrivateField(_swipeAudio, "swipeEvent", swipeEvent);

            // Camera follows the kart.
            SetPrivateField(_cameraRig, "target", kartGo.transform);

            // ----- Solo ghost player (records + replays best run) -----
            var soloGhostGo = new GameObject("SoloGhostPlayer");
            _soloGhost = soloGhostGo.AddComponent<SoloGhostPlayer>();
            SetPrivateField(_soloGhost, "playerRecorder", _ghostRecorder);
            SetPrivateField(_soloGhost, "playerSwipeEvent", swipeEvent);
            SetPrivateField(_soloGhost, "ghostSwipeEvent", ghostSwipeEvent);
            SetPrivateField(_soloGhost, "trackId", "prototype_track");

            // ----- HUD canvas -----
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(canvasGo.transform, false);

            // Try TMP first; fall back to legacy UI.Text if TMP resources aren't imported.
            Component statusTextComponent;
            try
            {
                statusTextComponent = statusGo.AddComponent<TextMeshProUGUI>();
                Debug.Log("[Prototype] Using TextMeshPro for HUD (recommended).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[Prototype] TextMeshProUGUI failed (likely TMP Essential Resources not imported). " +
                    "Falling back to legacy UI.Text. To enable TMP: Window > TextMeshPro > Import TMP Essential Resources.\n" +
                    "Error: " + ex.Message
                );
                var legacyText = statusGo.AddComponent<Text>();
                legacyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacyText.fontSize = 24;
                legacyText.color = new Color(0.969f, 0.969f, 0.949f);
                legacyText.alignment = TextAnchor.UpperLeft;
                legacyText.raycastTarget = false;
                statusTextComponent = legacyText;
            }

            // Apply common styling (works for both TMP and legacy via duck typing).
            if (statusTextComponent is TMP_Text tmpText)
            {
                tmpText.fontSize = 36;
                tmpText.alignment = TextAlignmentOptions.TopLeft;
                tmpText.color = new Color(0.969f, 0.969f, 0.949f);
            }

            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(0, 1);
            statusRect.pivot = new Vector2(0, 1);
            statusRect.anchoredPosition = new Vector2(40, -40);
            statusRect.sizeDelta = new Vector2(900, 800);

            _hud = canvasGo.AddComponent<PrototypeHUD>();
            SetPrivateField(_hud, "raceStarter", this);
            SetPrivateField(_hud, "kart", _kart);
            // Wire up whichever text component we created (TMP if available, legacy otherwise).
            if (statusTextComponent is TMP_Text tmp)
                SetPrivateField(_hud, "statusTextTMP", tmp);
            else if (statusTextComponent is Text legacy)
                SetPrivateField(_hud, "statusTextLegacy", legacy);

            // ----- Hint text -----
            var hintGo = new GameObject("HintText");
            hintGo.transform.SetParent(canvasGo.transform, false);
            Component hintTextComponent;
            try
            {
                var tmpHint = hintGo.AddComponent<TextMeshProUGUI>();
                tmpHint.fontSize = 28;
                tmpHint.alignment = TextAlignmentOptions.Bottom;
                tmpHint.color = new Color(0.969f, 0.969f, 0.949f, 0.7f);
                tmpHint.text = "FLICK to move the kart. Reach the green line on the right. R = reset. C = clear best run.";
                hintTextComponent = tmpHint;
            }
            catch
            {
                var legacyHint = hintGo.AddComponent<Text>();
                legacyHint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                legacyHint.fontSize = 20;
                legacyHint.alignment = TextAnchor.LowerCenter;
                legacyHint.color = new Color(0.969f, 0.969f, 0.949f, 0.7f);
                legacyHint.text = "FLICK to move the kart. Reach the green line. R = reset. C = clear best.";
                legacyHint.raycastTarget = false;
                hintTextComponent = legacyHint;
            }
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0);
            hintRect.anchorMax = new Vector2(0.5f, 0);
            hintRect.pivot = new Vector2(0.5f, 0);
            hintRect.anchoredPosition = new Vector2(0, 40);
            hintRect.sizeDelta = new Vector2(1000, 100);

            if (verboseLogging)
            {
                Debug.Log($"[Prototype] Scene built. {obstaclePositions.Length} obstacles, " +
                          $"par={parSwipes} swipes, finish at x={finishLineX}");
            }
        }

        private GameObject CreateWall(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.color = color;
            renderer.sprite = CreateSquareSprite();
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            return go;
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
                _cachedSquareSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f),
                    size
                );
                _cachedSquareSprite.name = "PrototypeSquare";
            }
            return _cachedSquareSprite;
        }

        private void StartRace()
        {
            _raceStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _swipeDetector.SetRaceStartTime(_raceStartMs);
            _ghostRecorder.BeginRace("prototype_track", "kart_starter");
            swipeEvent.Register(OnSwipe);
            _finished = false;
            _swipeCount = 0;
            if (_kartTrail != null) _kartTrail.Clear();

            // Start ghost replay (if we have a best run recorded).
            if (_soloGhost != null)
            {
                _soloGhost.OnRaceStart(_raceStartMs, kartSpawn);
            }

            Debug.Log("[Prototype] Race started. Par = " + parSwipes + " swipes. Beat the ghost if there is one!");
        }

        private void OnSwipe(SwipeData swipe)
        {
            _swipeCount++;
            if (verboseLogging)
            {
                Debug.Log($"[Prototype] Swipe #{_swipeCount}: mag={swipe.magnitude:F0}, " +
                          $"dir=({swipe.direction.x:F2}, {swipe.direction.y:F2}), " +
                          $"rawV={swipe.rawVelocityPxS:F0}px/s");
            }
        }

        private void Update()
        {
            if (_kart != null && !_finished && _kart.transform.position.x >= finishLineX)
            {
                FinishRace();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetRace();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                _soloGhost?.ClearBestRun();
                Debug.Log("[Prototype] Best run cleared. Restart to race without ghost.");
            }
        }

        private void FinishRace()
        {
            _finished = true;
            long finishMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartMs;

            // Solo ghost: compare + save if better.
            _soloGhost?.OnRaceFinish(_swipeCount, finishMs);

            swipeEvent.Unregister(OnSwipe);
            OnRaceFinished?.Invoke(_swipeCount, finishMs);

            int bestSwipes = _soloGhost?.BestGhost?.swipeCount ?? _swipeCount;
            long bestTime = _soloGhost?.BestGhost?.finishTimeMs ?? finishMs;

            int delta = _swipeCount - bestSwipes;
            string deltaStr = delta <= 0
                ? $"<color=#3FE0C2>NEW BEST! (-{Math.Abs(delta)} swipes)</color>"
                : $"+{delta} swipes vs best";

            Debug.Log($"[Prototype] Race finished! Swipes: {_swipeCount} (par {parSwipes}), " +
                      $"Time: {finishMs / 1000f:F2}s. {deltaStr}");
        }

        public void ResetRace()
        {
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
            if (_kart != null) _kart.RespawnAt(kartSpawn);
            StartRace();
        }

        private void OnDisable()
        {
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null || string.IsNullOrEmpty(fieldName)) return;
            var type = obj.GetType();
            var field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"[Prototype] Field '{fieldName}' not found on {type.Name}.");
            }
        }
    }
}
