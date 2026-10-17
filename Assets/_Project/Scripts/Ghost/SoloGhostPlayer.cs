using System;
using UnityEngine;
using QuickChecks.Input;
using Newtonsoft.Json;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Records the player's best run and replays it as a ghost.
    ///
    /// TURN-TAKING LOGIC:
    /// 1. Player swipes → player moves
    /// 2. Player stops (velocity < threshold) → trigger ghost advance
    /// 3. Ghost advances ~1 second of recorded path (smooth interpolation)
    /// 4. Ghost stops → wait for next player swipe
    ///
    /// The ghost is triggered by the PLAYER STOPPING, not by swiping.
    /// This ensures the camera can follow the player during their move,
    /// then switch to the ghost for its move, in proper turn-taking sequence.
    /// </summary>
    public class SoloGhostPlayer : MonoBehaviour
    {
        [SerializeField] private GhostRecorder playerRecorder;
        [SerializeField] private string trackId = "prototype_track";
        [SerializeField] private Vector3 ghostSpawnOffset = new Vector3(0, 0, 0);
        [SerializeField] private Racing.KartController playerKart;

        [Tooltip("Velocity below which the kart is considered 'stopped'.")]
        [SerializeField] private float stoppedThreshold = 10f;

        [Tooltip("Delay (seconds) after player stops before ghost starts moving. " +
                 "Gives camera time to pan from player to ghost.")]
        [SerializeField] private float ghostStartDelay = 0.5f;

        private GhostData _bestGhost;
        private GhostPlayer _ghostPlayer;
        private GameObject _ghostKartInstance;
        private bool _playerWasMoving = false;
        private float _ghostStartTimer = 0f;
        private bool _waitingToStartGhost = false;

        public GhostData BestGhost => _bestGhost;
        public bool HasBestRun => _bestGhost != null;
        public GhostPlayer GhostPlayer => _ghostPlayer;

        private void Awake()
        {
            // Don't load ghost here — trackId is not set yet (set by RaceSceneController via reflection
            // AFTER Awake). LoadBestGhost() will be called from OnRaceStart() instead.
        }

        public void OnRaceStart(long startMs, Vector3 playerSpawn)
        {
            // Load best ghost NOW (trackId has been set by RaceSceneController before calling OnRaceStart)
            if (_bestGhost == null)
            {
                LoadBestGhost();
            }

            // Diagnostic logging
            Debug.Log($"[SoloGhost] OnRaceStart called. " +
                      $"_bestGhost null? {_bestGhost == null}. " +
                      $"playerKart null? {playerKart == null}. " +
                      $"playerRecorder null? {playerRecorder == null}.");

            if (_bestGhost == null)
            {
                Debug.Log("[SoloGhost] No best run yet — racing without ghost. " +
                          "Complete a lap to record your first ghost.");
                return;
            }

            int posCount = _bestGhost.positionSamples?.Count ?? 0;
            int stopCount = _bestGhost.stopPositions?.Count ?? 0;
            Debug.Log($"[SoloGhost] Best ghost loaded: {_bestGhost.swipeCount} swipes, " +
                      $"{posCount} position samples, {stopCount} stop positions, " +
                      $"{_bestGhost.finishTimeMs / 1000f:F2}s.");

            if (posCount == 0)
            {
                Debug.LogWarning("[SoloGhost] Best ghost has 0 position samples — not spawning. " +
                                 "Press C to clear old data, then complete a new lap.");
                return;
            }

            if (stopCount == 0)
            {
                Debug.LogWarning("[SoloGhost] Best ghost has 0 stop positions — not spawning. " +
                                 "Press C to clear old data, then complete a new lap.");
                return;
            }

            // Destroy old ghost if exists
            if (_ghostKartInstance != null) Destroy(_ghostKartInstance);

            // Create ghost kart — simple visual only, NO physics
            Vector3 ghostSpawnPos = playerSpawn + ghostSpawnOffset;
            _ghostKartInstance = CreateGhostKartGameObject(ghostSpawnPos);
            _ghostKartInstance.name = "GhostKart";
            _ghostKartInstance.SetActive(true);

            // Add GhostPlayer component
            _ghostPlayer = _ghostKartInstance.AddComponent<GhostPlayer>();
            SetField(_ghostPlayer, "ghostKart", _ghostKartInstance.transform);

            // Wire the ghost into the camera follow controller
            var cameraFollow = FindObjectOfType<QuickChecks.Camera.CameraFollowController>();
            if (cameraFollow != null)
            {
                cameraFollow.SetGhostPlayer(_ghostPlayer);
            }

            _ghostPlayer.StartPlayback(_bestGhost, startMs);

            // Reset turn-taking state
            _playerWasMoving = false;
            _waitingToStartGhost = false;
            _ghostStartTimer = 0f;

            Debug.Log($"[SoloGhost] Ghost spawned with {_bestGhost.positionSamples.Count} position samples. " +
                      "Ghost will advance when you STOP moving (turn-taking mode).");
        }

        private void Update()
        {
            if (_ghostPlayer == null || playerKart == null) return;
            if (!_ghostPlayer.IsPlaying) return;

            // Detect player movement state transitions
            bool playerMoving = playerKart.Velocity.magnitude > stoppedThreshold;

            // Player was moving but now stopped → trigger ghost advance after delay
            if (_playerWasMoving && !playerMoving && !_waitingToStartGhost && !_ghostPlayer.IsMoving)
            {
                _waitingToStartGhost = true;
                _ghostStartTimer = ghostStartDelay;
                Debug.Log($"[SoloGhost] Player stopped. Ghost will start in {ghostStartDelay}s (camera pan time).");
            }

            // Player started moving again → cancel pending ghost advance
            if (playerMoving && _waitingToStartGhost)
            {
                _waitingToStartGhost = false;
                Debug.Log("[SoloGhost] Player moved again — cancelled pending ghost advance.");
            }

            _playerWasMoving = playerMoving;

            // Countdown to ghost start
            if (_waitingToStartGhost)
            {
                _ghostStartTimer -= Time.deltaTime;
                if (_ghostStartTimer <= 0f)
                {
                    _waitingToStartGhost = false;
                    _ghostPlayer.AdvanceGhost();
                }
            }
        }

        public void OnRaceFinish(int swipes, long finishMs)
        {
            Debug.Log($"[SoloGhost] OnRaceFinish: swipes={swipes}, time={finishMs}ms, " +
                      $"playerRecorder null? {playerRecorder == null}, " +
                      $"_bestGhost null? {_bestGhost == null}.");

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
                    int posCount = _bestGhost.positionSamples?.Count ?? 0;
                    int stopCount = _bestGhost.stopPositions?.Count ?? 0;
                    Debug.Log($"[SoloGhost] NEW BEST! {swipes} swipes in {finishMs / 1000f:F2}s. " +
                              $"Saved with {posCount} position samples, {stopCount} stop positions.");
                }
                else
                {
                    Debug.LogError("[SoloGhost] playerRecorder.EndRace returned null — ghost NOT saved!");
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

        /// <summary>
        /// Loads an external ghost (e.g. downloaded from the leaderboard) and overrides
        /// the player's local best ghost for the next race. The external ghost is NOT
        /// saved to PlayerPrefs — the player's own best run is preserved.
        ///
        /// Called by RaceSceneController when the player taps "Replay" on a leaderboard
        /// row. The race then plays back this ghost instead of (or alongside) the player's
        /// local best.
        /// </summary>
        public void LoadExternalGhost(GhostData externalGhost)
        {
            if (externalGhost == null)
            {
                Debug.LogWarning("[SoloGhost] LoadExternalGhost called with null — ignoring.");
                return;
            }

            int posCount = externalGhost.positionSamples?.Count ?? 0;
            int stopCount = externalGhost.stopPositions?.Count ?? 0;
            if (posCount == 0 || stopCount == 0)
            {
                Debug.LogWarning($"[SoloGhost] External ghost has {posCount} position samples + " +
                                  $"{stopCount} stop positions — not enough to replay. Ignoring.");
                return;
            }

            _bestGhost = externalGhost;
            Debug.Log($"[SoloGhost] External ghost loaded: {externalGhost.swipeCount} swipes, " +
                      $"{posCount} position samples, {stopCount} stop positions, " +
                      $"{externalGhost.finishTimeMs / 1000f:F2}s. Will replay on next race start.");
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
                        if (posCount == 0)
                        {
                            Debug.LogWarning("[SoloGhost] Loaded ghost has 0 position samples — discarding.");
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

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.482f, 0.541f, 0.541f, 0.6f);
            sr.sprite = CreateGhostSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.5f, 1.5f);
            sr.sortingOrder = 8;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);
            col.isTrigger = true;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

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
        }
    }
}
