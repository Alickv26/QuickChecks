using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using QuickChecks.Input;
using Newtonsoft.Json;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// In solo practice mode, records the player's best run for the current track
    /// and replays it as a ghost kart alongside the player's next attempt.
    ///
    /// Storage: PlayerPrefs JSON (simple, persistent across sessions for prototype).
    /// For v1 production, swap to Supabase for cross-device sync.
    ///
    /// Flow:
    ///   1. Player finishes a race -> GhostRecorder produces GhostData
    ///   2. This component compares to stored best, persists if better (fewer swipes)
    ///   3. Next race, this component spawns a ghost kart and replays best-run swipes
    ///   4. Ghost kart uses a separate SwipeEventSO so it doesn't drive the player's kart
    /// </summary>
    public class SoloGhostPlayer : MonoBehaviour
    {
        [SerializeField] private GhostRecorder playerRecorder;
        [SerializeField] private Core.GameEventSO<SwipeData> playerSwipeEvent;
        [SerializeField] private Core.GameEventSO<SwipeData> ghostSwipeEvent; // separate event for ghost kart
        [SerializeField] private string trackId = "prototype_track";
        [SerializeField] private GameObject ghostKartPrefab;
        [Tooltip("Offset from player's spawn position where the ghost kart appears. " +
                 "Default (0, 0, 0) = same position as player. The ghost uses a separate swipeEvent " +
                 "so it drives its own kart independently — no collision with player since ghost " +
                 "has isTrigger collider. Keeping ghost at same spawn point ensures it's ON the track.")]
        [SerializeField] private Vector3 ghostSpawnOffset = new Vector3(0, 0, 0);

        private GhostData _bestGhost;
        private GhostPlayer _ghostPlayer;
        private GameObject _ghostKartInstance;
        private long _raceStartMs;

        public GhostData BestGhost => _bestGhost;
        public bool HasBestRun => _bestGhost != null;

        private void Awake()
        {
            LoadBestGhost();
        }

        public void OnRaceStart(long startMs, Vector3 playerSpawn)
        {
            _raceStartMs = startMs;

            if (_bestGhost == null)
            {
                Debug.Log("[SoloGhost] No best run yet — racing without ghost.");
                return;
            }

            if (_bestGhost.swipeEvents == null || _bestGhost.swipeEvents.Count == 0)
            {
                Debug.LogWarning("[SoloGhost] Best ghost has 0 swipe events — not spawning ghost. " +
                                 "Saved data may be corrupt (JsonUtility can't serialize List<struct>).");
                return;
            }

            // Create ghost kart at runtime (no prefab needed).
            // Same structure as the player kart in PrototypeBootstrapper.
            if (_ghostKartInstance != null) Destroy(_ghostKartInstance);
            Vector3 ghostSpawnPos = playerSpawn + ghostSpawnOffset;
            _ghostKartInstance = CreateGhostKartGameObject(ghostSpawnPos);
            _ghostKartInstance.name = "GhostKart";
            _ghostKartInstance.SetActive(true);  // Ensure it's active

            Debug.Log($"[SoloGhost] Ghost kart spawned at {ghostSpawnPos} " +
                      $"(player spawn: {playerSpawn}, offset: {ghostSpawnOffset}). " +
                      $"Active: {_ghostKartInstance.activeInHierarchy}, " +
                      $"SpriteRenderer enabled: {_ghostKartInstance.GetComponent<SpriteRenderer>()?.enabled}");

            // Add GhostPlayer component if not already present.
            _ghostPlayer = _ghostKartInstance.GetComponent<GhostPlayer>();
            if (_ghostPlayer == null)
            {
                _ghostPlayer = _ghostKartInstance.AddComponent<GhostPlayer>();
            }

            // Wire ghost player's swipe event (output) to its kart.
            var ghostKart = _ghostKartInstance.GetComponent<Racing.KartController>();
            if (ghostKart != null)
            {
                // Use the public SetSwipeEvent method instead of reflection.
                // This immediately subscribes to the ghostSwipeEvent, avoiding the
                // OnEnable/Start race condition where the field is set after OnEnable fires.
                ghostKart.SetSwipeEvent(ghostSwipeEvent);
                Debug.Log($"[SoloGhost] Ghost kart's swipeEvent set via SetSwipeEvent() — should now receive ghost swipes.");
            }
            else
            {
                Debug.LogError("[SoloGhost] GhostKart has no KartController component — can't wire swipe event!");
            }

            // Wire ghost player's playerSwipeEvent (input — listens to player's swipes for
            // synchronized follow-along mode). This is what makes the ghost swipe 1:1 after the player.
            SetField(_ghostPlayer, "playerSwipeEvent", playerSwipeEvent);
            SetField(_ghostPlayer, "ghostSwipeEvent", ghostSwipeEvent);

            _ghostPlayer.StartPlayback(_bestGhost, startMs);
            Debug.Log($"[SoloGhost] Replaying best run: {_bestGhost.swipeCount} swipes, " +
                      $"{_bestGhost.finishTimeMs / 1000f:F2}s, {_bestGhost.swipeEvents.Count} events. " +
                      "Ghost will swipe 0.3s after each of your swipes (synchronized mode).");
        }

        /// <summary>
        /// Reflection helper to set private [SerializeField] fields.
        /// Used because the GhostPlayer's swipeEvent fields are private with [SerializeField]
        /// for cleanliness — we wire them at runtime via reflection instead of making them public.
        /// </summary>
        private static void SetField(object obj, string fieldName, object value)
        {
            if (obj == null || string.IsNullOrEmpty(fieldName)) return;
            var type = obj.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"[SoloGhostPlayer] Field '{fieldName}' not found on {type.Name}.");
            }
        }

        private GameObject CreateGhostKartGameObject(Vector3 position)
        {
            var go = new GameObject("GhostKart");
            go.transform.position = position;
            go.transform.localScale = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.482f, 0.541f, 0.541f, 0.5f);  // muted gray-blue, 50% alpha
            sr.sprite = CreateGhostSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 1.5f);
            sr.sortingOrder = 8;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);
            col.isTrigger = true;  // Ghost doesn't collide with walls (passes through)

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            // DYNAMIC (not Kinematic) so _rb.velocity = currentVelocity actually moves the kart.
            // Kinematic bodies ignore velocity — they only move via MovePosition().
            // Since KartController.FixedUpdate sets _rb.velocity, we need Dynamic.
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var kart = go.AddComponent<Racing.KartController>();
            // Ghost KartStats: same as player but slightly different friction for visibility
            var stats = ScriptableObject.CreateInstance<Racing.KartStats>();
            stats.kartId = "kart_ghost";
            stats.displayName = "Ghost";
            stats.impulseMultiplier = 1.1f;
            stats.frictionPerSecond = 0.22f;
            stats.stopThreshold = 8f;
            stats.maxSpeed = 250f;

            // Wire stats via reflection
            var type = kart.GetType();
            var statsField = type.GetField("stats",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            statsField?.SetValue(kart, stats);

            // Add a KartTrail to the ghost so the player can see the ghost's path.
            // The trail is colored differently (faded gray-blue) to distinguish from player's trail.
            var trail = go.AddComponent<Racing.KartTrail>();
            SetField(trail, "kart", kart);

            // Disable player input on the ghost kart (no SwipeDetector — ghost is driven by GhostPlayer)
            return go;
        }

        private Sprite _cachedGhostSprite;
        private Sprite CreateGhostSprite()
        {
            if (_cachedGhostSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size);
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _cachedGhostSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return _cachedGhostSprite;
        }

        public void OnRaceFinish(int swipes, long finishMs)
        {
            // Compare to best. Lower swipes wins; tiebreaker = faster time.
            bool isBetter = _bestGhost == null
                || swipes < _bestGhost.swipeCount
                || (swipes == _bestGhost.swipeCount && finishMs < _bestGhost.finishTimeMs);

            if (isBetter)
            {
                var newBest = playerRecorder.EndRace(finishMs);
                if (newBest != null)
                {
                    _bestGhost = newBest;
                    SaveBestGhost();
                    Debug.Log($"[SoloGhost] NEW BEST! {swipes} swipes in {finishMs / 1000f:F2}s" +
                              (swipes < (_bestGhost?.swipeCount ?? int.MaxValue) ? " 🏆" : ""));
                }
            }
            else
            {
                Debug.Log($"[SoloGhost] Not better than best ({_bestGhost.swipeCount} swipes).");
            }
        }

        public void ClearBestRun()
        {
            _bestGhost = null;
            PlayerPrefs.DeleteKey(GetPrefsKey());
            PlayerPrefs.Save();
            Debug.Log("[SoloGhost] Best run cleared.");
        }

        private string GetPrefsKey() => $"QuickChecks_BestGhost_{trackId}";

        private void LoadBestGhost()
        {
            string json = PlayerPrefs.GetString(GetPrefsKey(), "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    // Use Newtonsoft.Json (handles List<struct> properly, unlike JsonUtility).
                    _bestGhost = JsonConvert.DeserializeObject<GhostData>(json);
                    if (_bestGhost != null && (_bestGhost.swipeEvents == null || _bestGhost.swipeEvents.Count == 0))
                    {
                        Debug.LogWarning("[SoloGhost] Loaded ghost had no swipe events — discarding.");
                        _bestGhost = null;
                    }
                    else if (_bestGhost != null)
                    {
                        Debug.Log($"[SoloGhost] Loaded best run: {_bestGhost.swipeCount} swipes, " +
                                  $"{_bestGhost.swipeEvents.Count} events, {_bestGhost.finishTimeMs / 1000f:F2}s.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SoloGhost] Failed to load best ghost: {ex.Message}");
                    _bestGhost = null;
                }
            }
        }

        private void SaveBestGhost()
        {
            if (_bestGhost == null) return;
            string json = JsonConvert.SerializeObject(_bestGhost);
            PlayerPrefs.SetString(GetPrefsKey(), json);
            PlayerPrefs.Save();
        }
    }
}
