using System;
using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;
using Newtonsoft.Json;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Records the player's best run and replays it as a ghost.
    ///
    /// POSITION-BASED REPLAY: The ghost follows the EXACT recorded path by
    /// setting transform.position from recorded position samples. No physics
    /// needed — the ghost is a kinematic trigger that just follows the path.
    ///
    /// SYNCHRONIZED MODE: When the player swipes, the ghost advances its
    /// position by ~1 second of recorded movement. This creates the
    /// turn-taking feel: player swipes → ghost moves → player swipes → ghost moves.
    /// </summary>
    public class SoloGhostPlayer : MonoBehaviour
    {
        [SerializeField] private GhostRecorder playerRecorder;
        [SerializeField] private Core.GameEventSO<SwipeData> playerSwipeEvent;
        [SerializeField] private string trackId = "prototype_track";
        [SerializeField] private Vector3 ghostSpawnOffset = new Vector3(0, 0, 0);

        private GhostData _bestGhost;
        private GhostPlayer _ghostPlayer;
        private GameObject _ghostKartInstance;

        public GhostData BestGhost => _bestGhost;
        public bool HasBestRun => _bestGhost != null;
        public GhostPlayer GhostPlayer => _ghostPlayer;

        private void Awake()
        {
            LoadBestGhost();
        }

        public void OnRaceStart(long startMs, Vector3 playerSpawn)
        {
            if (_bestGhost == null)
            {
                Debug.Log("[SoloGhost] No best run yet — racing without ghost.");
                return;
            }

            if (_bestGhost.positionSamples == null || _bestGhost.positionSamples.Count == 0)
            {
                Debug.LogWarning("[SoloGhost] Best ghost has 0 position samples — not spawning ghost. " +
                                 "Old ghost data may not have position samples (pre-position-replay era).");
                return;
            }

            // Destroy old ghost if exists
            if (_ghostKartInstance != null) Destroy(_ghostKartInstance);

            // Create ghost kart — simple visual only, NO physics, NO KartController
            Vector3 ghostSpawnPos = playerSpawn + ghostSpawnOffset;
            _ghostKartInstance = CreateGhostKartGameObject(ghostSpawnPos);
            _ghostKartInstance.name = "GhostKart";
            _ghostKartInstance.SetActive(true);

            Debug.Log($"[SoloGhost] Ghost kart spawned at {ghostSpawnPos}. " +
                      $"Position samples: {_bestGhost.positionSamples.Count}, " +
                      $"Swipes: {_bestGhost.swipeCount}.");

            // Add GhostPlayer component
            _ghostPlayer = _ghostKartInstance.AddComponent<GhostPlayer>();
            SetField(_ghostPlayer, "ghostKart", _ghostKartInstance.transform);

            // Wire the ghost into the camera follow controller
            var cameraFollow = FindObjectOfType<QuickChecks.Camera.CameraFollowController>();
            if (cameraFollow != null)
            {
                cameraFollow.SetGhostPlayer(_ghostPlayer);
            }

            // Subscribe to player swipe events for synchronized playback
            if (playerSwipeEvent != null)
            {
                playerSwipeEvent.Register(OnPlayerSwipe);
                Debug.Log("[SoloGhost] Subscribed to playerSwipeEvent — ghost will advance on each player swipe.");
            }

            _ghostPlayer.StartPlayback(_bestGhost, startMs);
            Debug.Log($"[SoloGhost] Ghost playback started. {_bestGhost.positionSamples.Count} position samples. " +
                      "Ghost will advance ~1 second of movement per player swipe.");
        }

        private void OnPlayerSwipe(SwipeData swipe)
        {
            if (_ghostPlayer != null && _ghostPlayer.IsPlaying)
            {
                _ghostPlayer.OnPlayerSwipe();
            }
        }

        public void OnRaceFinish(int swipes, long finishMs)
        {
            // Unsubscribe from player swipe events
            if (playerSwipeEvent != null)
            {
                playerSwipeEvent.Unregister(OnPlayerSwipe);
            }

            // Stop ghost playback
            if (_ghostPlayer != null)
            {
                _ghostPlayer.StopPlayback();
            }

            // Check if this is a new best (fewer swipes, or same swipes + faster time)
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
                    Debug.Log($"[SoloGhost] NEW BEST! {swipes} swipes in {finishMs / 1000f:F2}s. " +
                              $"{_bestGhost.positionSamples.Count} position samples saved.");
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
                    _bestGhost = JsonConvert.DeserializeObject<GhostData>(json);
                    if (_bestGhost != null)
                    {
                        int posCount = _bestGhost.positionSamples?.Count ?? 0;
                        int swipeCount = _bestGhost.swipeEvents?.Count ?? 0;
                        if (posCount == 0 && swipeCount == 0)
                        {
                            Debug.LogWarning("[SoloGhost] Loaded ghost has 0 samples — discarding.");
                            _bestGhost = null;
                        }
                        else
                        {
                            Debug.Log($"[SoloGhost] Loaded best run: {_bestGhost.swipeCount} swipes, " +
                                      $"{posCount} position samples, {_bestGhost.finishTimeMs / 1000f:F2}s.");
                        }
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

        private GameObject CreateGhostKartGameObject(Vector3 position)
        {
            var go = new GameObject("GhostKart");
            go.transform.position = position;
            go.transform.localScale = Vector3.one;

            // Visual sprite — muted gray-blue, semi-transparent
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.482f, 0.541f, 0.541f, 0.6f);
            sr.sprite = CreateGhostSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 1.5f);
            sr.sortingOrder = 8;

            // Trigger collider — ghost doesn't physically interact with anything
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);
            col.isTrigger = true;

            // Kinematic Rigidbody2D — no gravity, no physics forces, just follows transform.position
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Add KartTrail for visual path
            // (No KartController needed — ghost just follows recorded positions)

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
    }
}
