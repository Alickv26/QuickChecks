using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Library of all tracks in the game. References the 8 handcrafted
    /// TrackDefinition assets. Used by track-select UI and the daily track generator.
    ///
    /// Tracks are ordered by difficulty (trackIndex 0 = easiest, 7 = hardest).
    /// First 3 tracks have isFree=true (Decision 16: freemium gating).
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Track Library", fileName = "TrackLibrary")]
    public class TrackLibrary : ScriptableObject
    {
        [Tooltip("All tracks in the game, ordered by difficulty. " +
                 "Index 0 = Track 1 (easiest), Index 7 = Track 8 (hardest).")]
        public TrackDefinition[] tracks = new TrackDefinition[8];

        /// <summary>
        /// Returns the track at the given 0-based index, or null if out of range.
        /// </summary>
        public TrackDefinition GetTrack(int index)
        {
            if (tracks == null || index < 0 || index >= tracks.Length) return null;
            return tracks[index];
        }

        /// <summary>
        /// Returns the first track that has the given trackId, or null if not found.
        /// </summary>
        public TrackDefinition FindById(string trackId)
        {
            if (tracks == null || string.IsNullOrEmpty(trackId)) return null;
            foreach (var t in tracks)
            {
                if (t != null && t.trackId == trackId) return t;
            }
            return null;
        }

        /// <summary>
        /// Returns true if the track is unlocked for the given player progress
        /// (trackIndex of the highest track they've completed).
        /// </summary>
        public bool IsUnlocked(TrackDefinition track, int highestUnlockedTrackIndex)
        {
            if (track == null) return false;
            return track.trackIndex <= highestUnlockedTrackIndex;
        }

        /// <summary>
        /// Returns true if the track is free (available without IAP).
        /// Decision 16: tracks 1-3 are free, 4-8 require IAP unlock.
        /// </summary>
        public bool IsFree(TrackDefinition track)
        {
            return track != null && track.isFree;
        }
    }
}
