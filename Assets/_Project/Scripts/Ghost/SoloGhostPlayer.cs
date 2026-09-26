using System;
using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

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
        [SerializeField] private Vector3 ghostSpawnOffset = new Vector3(0, 2, 0);

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

            if (_bestGhost == null || ghostKartPrefab == null)
            {
                Debug.Log("[SoloGhost] No best run yet — racing without ghost.");
                return;
            }

            // Spawn ghost kart at the same position as the player (offset slightly).
            if (_ghostKartInstance != null) Destroy(_ghostKartInstance);
            _ghostKartInstance = Instantiate(ghostKartPrefab, playerSpawn + ghostSpawnOffset, Quaternion.identity);
            _ghostKartInstance.name = "GhostKart";

            // Dim the ghost kart's color so it doesn't confuse with player.
            var renderer = _ghostKartInstance.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                var c = renderer.color;
                c.a = 0.5f;
                renderer.color = c;
            }

            // Add GhostPlayer component if not already present.
            _ghostPlayer = _ghostKartInstance.GetComponent<GhostPlayer>();
            if (_ghostPlayer == null)
            {
                _ghostPlayer = _ghostKartInstance.AddComponent<GhostPlayer>();
            }

            // Wire ghost player's swipe event to its kart.
            var ghostKart = _ghostKartInstance.GetComponent<Racing.KartController>();
            if (ghostKart != null)
            {
                // Use reflection to set the private swipeEvent field on the ghost kart.
                var type = ghostKart.GetType();
                var field = type.GetField("swipeEvent",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(ghostKart, ghostSwipeEvent);
            }

            _ghostPlayer.StartPlayback(_bestGhost, startMs);
            Debug.Log($"[SoloGhost] Replaying best run: {_bestGhost.swipeCount} swipes, {_bestGhost.finishTimeMs / 1000f:F2}s");
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
                    _bestGhost = JsonUtility.FromJson<GhostData>(json);
                    // JsonUtility doesn't deserialize List<SwipeEventEntry> from raw struct JSON.
                    // For prototype, store swipeEvents count + replay from a sub-string.
                    // If swipeEvents count is 0, the save is corrupt — wipe it.
                    if (_bestGhost != null && (_bestGhost.swipeEvents == null || _bestGhost.swipeEvents.Count == 0))
                    {
                        Debug.LogWarning("[SoloGhost] Loaded ghost had no swipe events — discarding.");
                        _bestGhost = null;
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
            string json = JsonUtility.ToJson(_bestGhost);
            PlayerPrefs.SetString(GetPrefsKey(), json);
            PlayerPrefs.Save();
        }
    }
}
