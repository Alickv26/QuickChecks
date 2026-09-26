using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using QuickChecks.Input;
using QuickChecks.Racing;
using QuickChecks.Ghost;
using QuickChecks.Core;
using QuickChecks.Camera;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// Week 1 prototype: validates swipe feel before committing to full track system.
    /// This script builds the entire scene at runtime — no manual scene setup needed.
    ///
    /// Scene contents (built in Awake):
    ///   - Camera with CameraRig
    ///   - Rectangular track with 4 walls + finish line trigger
    ///   - Player kart (yellow square) with SwipeDetector, KartController, GhostRecorder
    ///   - HUD canvas with TextMeshPro status text
    ///
    /// Goal: swipe the kart from left to right across the finish line.
    ///       Press R to reset. Watch the swipe count — fewer is better.
    ///
    /// Validation criteria (see PROTOTYPE_README.md):
    ///   1. Does each swipe feel like a "billiards shot" or "golf stroke"?
    ///   2. Is the swipe velocity threshold right (no false rejections / no false accepts)?
    ///   3. Is the friction curve satisfying (coast + decelerate + stop)?
    ///   4. Does pinch-zoom feel natural on mobile?
    /// </summary>
    public class PrototypeBootstrapper : MonoBehaviour
    {
        [Header("Track Config")]
        [SerializeField] private Vector2 trackSize = new Vector2(40, 20);
        [SerializeField] private Vector3 kartSpawn = new Vector3(-15, 0, 0);
        [SerializeField] private float finishLineX = 15f;

        [Header("Tuning (overrides defaults if set)")]
        [SerializeField] private InputSettings inputSettings;
        [SerializeField] private GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private KartStats kartStats;

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging = true;

        // Built at runtime
        private KartController _kart;
        private SwipeDetector _swipeDetector;
        private GhostRecorder _ghostRecorder;
        private CameraRig _cameraRig;
        private PrototypeHUD _hud;

        // Race state
        private long _raceStartMs;
        private int _swipeCount;
        private bool _finished;

        // Public accessors for HUD
        public int SwipeCount => _swipeCount;
        public KartController Kart => _kart;
        public float FinishLineX => finishLineX;
        public bool IsFinished => _finished;

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
            // If ScriptableObjects aren't assigned, create them in-memory.
            // (Editor script will save them as .asset files for persistence.)
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
            if (kartStats == null)
            {
                kartStats = ScriptableObject.CreateInstance<KartStats>();
                kartStats.kartId = "kart_starter";
                kartStats.displayName = "Starter";
                if (verboseLogging) Debug.Log("[Prototype] Created in-memory KartStats");
            }
        }

        private void BuildScene()
        {
            // ----- Camera + CameraRig -----
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f); // #0E1414
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            _cameraRig = camGo.AddComponent<CameraRig>();
            SetPrivateField(_cameraRig, "inputSettings", inputSettings);

            // ----- Track walls -----
            var trackGo = new GameObject("Track");
            var wallColor = new Color(0.247f, 0.878f, 0.760f, 0.3f); // cyan, 30% alpha
            CreateWall(trackGo.transform, "Wall_Top",    new Vector2(0,  trackSize.y / 2), new Vector2(trackSize.x + 2, 1), wallColor);
            CreateWall(trackGo.transform, "Wall_Bottom", new Vector2(0, -trackSize.y / 2), new Vector2(trackSize.x + 2, 1), wallColor);
            CreateWall(trackGo.transform, "Wall_Left",   new Vector2(-trackSize.x / 2, 0), new Vector2(1, trackSize.y), wallColor);
            CreateWall(trackGo.transform, "Wall_Right",  new Vector2( trackSize.x / 2, 0), new Vector2(1, trackSize.y), wallColor);

            // ----- Finish line (visual + trigger) -----
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

            // ----- Player kart -----
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

            // Wire up private [SerializeField] fields via reflection.
            SetPrivateField(_kart, "stats", kartStats);
            SetPrivateField(_kart, "swipeEvent", swipeEvent);

            SetPrivateField(_swipeDetector, "settings", inputSettings);
            SetPrivateField(_swipeDetector, "swipeEvent", swipeEvent);

            SetPrivateField(_ghostRecorder, "swipeEvent", swipeEvent);

            SetPrivateField(_cameraRig, "target", kartGo.transform);

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
            TMP_Text statusText;
            try
            {
                statusText = statusGo.AddComponent<TextMeshProUGUI>();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[Prototype] Failed to add TextMeshProUGUI. " +
                    "Import TMP essentials: Window > TextMeshPro > Import TMP Essential Resources. " +
                    "Falling back to legacy UI.Text.\n" + ex.Message
                );
                statusText = (TMP_Text)(object)statusGo.AddComponent<UnityEngine.UI.Text>();
            }
            statusText.fontSize = 36;
            statusText.alignment = TextAlignmentOptions.TopLeft;
            statusText.color = new Color(0.969f, 0.969f, 0.949f); // #F7F7F2

            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(0, 1);
            statusRect.pivot = new Vector2(0, 1);
            statusRect.anchoredPosition = new Vector2(40, -40);
            statusRect.sizeDelta = new Vector2(900, 600);

            _hud = canvasGo.AddComponent<PrototypeHUD>();
            SetPrivateField(_hud, "raceStarter", this);
            SetPrivateField(_hud, "kart", _kart);
            SetPrivateField(_hud, "statusText", statusText);

            // ----- Hint text (bottom) -----
            var hintGo = new GameObject("HintText");
            hintGo.transform.SetParent(canvasGo.transform, false);
            TMP_Text hintText = hintGo.AddComponent<TextMeshProUGUI>();
            hintText.fontSize = 28;
            hintText.alignment = TextAlignmentOptions.Bottom;
            hintText.color = new Color(0.969f, 0.969f, 0.949f, 0.7f);
            hintText.text = "FLICK to move the kart. Reach the green line on the right. Press R to reset.";
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0);
            hintRect.anchorMax = new Vector2(0.5f, 0);
            hintRect.pivot = new Vector2(0.5f, 0);
            hintRect.anchoredPosition = new Vector2(0, 40);
            hintRect.sizeDelta = new Vector2(1000, 100);

            if (verboseLogging)
            {
                Debug.Log("[Prototype] Scene built. Kart at " + kartSpawn +
                          ", finish line at x=" + finishLineX +
                          ", track size " + trackSize);
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
            // Sprite is 1x1 unit; scale GameObject to match desired size.
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
            Debug.Log("[Prototype] Race started. Swipe the kart to the finish line on the right. Fewer swipes = better!");
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
        }

        private void FinishRace()
        {
            _finished = true;
            long finishMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartMs;
            var ghost = _ghostRecorder.EndRace(finishMs);
            swipeEvent.Unregister(OnSwipe);
            OnRaceFinished?.Invoke(_swipeCount, finishMs);
            Debug.Log($"[Prototype] Race finished! Swipes: {_swipeCount}, Time: {finishMs / 1000f:F2}s");
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

        /// <summary>
        /// Reflection helper to set [SerializeField] private fields.
        /// Used because the existing scripts use private fields with [SerializeField]
        /// for cleanliness; we don't want to break that just for the prototype.
        /// </summary>
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
                Debug.LogWarning($"[Prototype] Field '{fieldName}' not found on {type.Name}. " +
                                  "Wire it manually in the Inspector after the prototype loads.");
            }
        }
    }
}
