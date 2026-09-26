using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Per-kart tunable stats. Each kart prefab references one of these.
    /// Designers can create new KartStats assets via the menu and balance
    /// without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Kart Stats", fileName = "NewKartStats")]
    public class KartStats : ScriptableObject
    {
        [Header("Identity")]
        public string kartId;
        public string displayName;
        public Sprite previewSprite;

        [Header("Movement")]
        [Tooltip("Multiplier on incoming swipe impulse. Higher = more responsive to swipes.")]
        [Range(0.5f, 2f)] public float impulseMultiplier = 1.1f;

        [Tooltip("Velocity lost per second when coasting. 0.22 = 22% velocity lost per second.")]
        [Range(0f, 0.5f)] public float frictionPerSecond = 0.22f;

        [Tooltip("Minimum speed (units/s) below which kart is considered stopped. Higher = cleaner stop.")]
        public float stopThreshold = 8f;

        [Tooltip("Maximum speed cap, regardless of impulse or boost.")]
        public float maxSpeed = 1500f;

        [Header("Boost")]
        [Tooltip("Multiplier applied to velocity during a boost power-up.")]
        public float boostMultiplier = 1.8f;

        [Tooltip("Duration of boost power-up in seconds.")]
        public float boostDurationSec = 1.5f;

        [Header("Unlock Requirement")]
        [Tooltip("Track index (0-based) that must be completed to unlock this kart. -1 = unlocked from start.")]
        public int unlockAfterTrackIndex = -1;
    }
}
