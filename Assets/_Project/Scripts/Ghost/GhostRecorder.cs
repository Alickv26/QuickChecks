using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Records every valid swipe during a race into a GhostData object.
    /// Attached to the player kart. On race finish, hands off the GhostData
    /// to GhostRepository for upload to Supabase.
    /// </summary>
    public class GhostRecorder : MonoBehaviour
    {
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private string trackId;
        [SerializeField] private string kartId;

        private GhostData _data;
        private long _raceStartUnixMs;
        private bool _isRecording;

        public GhostData CurrentData => _data;

        public void BeginRace(string track, string kart)
        {
            trackId = track;
            kartId = kart;
            _data = new GhostData
            {
                trackId = track,
                kartId = kart,
                swipeEvents = new List<GhostData.SwipeEventEntry>(64)
            };
            _raceStartUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _isRecording = true;
        }

        public GhostData EndRace(long finishTimeMs)
        {
            _isRecording = false;
            if (_data == null) return null;
            _data.finishTimeMs = finishTimeMs;
            return _data;
        }

        private void OnEnable()
        {
            if (swipeEvent != null) swipeEvent.Register(OnSwipe);
        }

        private void OnDisable()
        {
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
        }

        private void OnSwipe(SwipeData swipe)
        {
            if (!_isRecording || _data == null) return;

            _data.swipeEvents.Add(new GhostData.SwipeEventEntry
            {
                t = swipe.timestampMs,
                x = swipe.screenPosition.x,
                y = swipe.screenPosition.y,
                dx = swipe.direction.x,
                dy = swipe.direction.y,
                mag = swipe.magnitude
            });
        }

        public void RecordPowerUpCollected(string powerUpId)
        {
            if (_data == null) return;
            _data.powerUpsCollected.Add(powerUpId);
        }
    }
}
