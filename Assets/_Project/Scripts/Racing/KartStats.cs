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

        [Tooltip("Short archetype label shown on kart select cards. e.g. 'Balanced', 'Glass cannon'.")]
        public string archetypeLabel = "Balanced";

        [Tooltip("One-line description shown under the name on the kart select card.")]
        [TextArea(2, 4)]
        public string description = "A balanced all-rounder kart.";

        [Tooltip("Whether this kart is free or requires IAP. Free karts are available to all players; " +
                 "paid karts require the $4.99 premium unlock (Decision 16).")]
        public bool isFree = true;

        [Tooltip("Display color used in neon UI (kart select card accent, in-race glow). " +
                 "Leave default for white; usually overridden per kart in the asset.")]
        public Color kartColor = new Color(1f, 0.82f, 0.4f);  // default warm yellow

        [Header("Movement")]
        [Tooltip("Multiplier on incoming swipe impulse. Higher = more responsive to swipes.")]
        [Range(0.5f, 2f)] public float impulseMultiplier = 1.1f;

        [Tooltip("Velocity lost per second when coasting. 0.22 = 22% velocity lost per second.")]
        [Range(0f, 0.5f)] public float frictionPerSecond = 0.22f;

        [Tooltip("Minimum speed (units/s) below which kart is considered stopped. Higher = cleaner stop.")]
        public float stopThreshold = 8f;

        [Tooltip("Maximum speed cap, regardless of impulse or boost. " +
                 "Match this to InputSettings.maxImpulseMagnitude for predictable behavior.")]
        public float maxSpeed = 250f;

        [Header("Boost")]
        [Tooltip("Multiplier applied to velocity during a boost power-up.")]
        public float boostMultiplier = 1.8f;

        [Tooltip("Duration of boost power-up in seconds.")]
        public float boostDurationSec = 1.5f;

        [Header("Unlock Requirement")]
        [Tooltip("Track index (0-based) that must be completed to unlock this kart. -1 = unlocked from start.")]
        public int unlockAfterTrackIndex = -1;

        // --- Derived stat ratings (0-3 stars) for UI display on kart select cards ---
        // These are heuristics; the underlying fields above are the source of truth for physics.

        /// <summary>Power rating 0-3: how much punch the kart has off the line.</summary>
        public int PowerRating
        {
            get
            {
                // Scale by impulse * maxSpeed: higher = more power
                float power = impulseMultiplier * maxSpeed;
                if (power >= 380f) return 3;
                if (power >= 320f) return 2;
                if (power >= 260f) return 1;
                return 0;
            }
        }

        /// <summary>Control rating 0-3: how precisely the kart stops where you want it.</summary>
        public int ControlRating
        {
            get
            {
                // High friction + low stop threshold = best control
                float ctrl = frictionPerSecond * 100f / Mathf.Max(stopThreshold, 1f);
                if (ctrl >= 8f) return 3;
                if (ctrl >= 5f) return 2;
                if (ctrl >= 3f) return 1;
                return 0;
            }
        }

        /// <summary>Boost rating 0-3: how strong the boost power-up is for this kart.</summary>
        public int BoostRating
        {
            get
            {
                float boost = boostMultiplier * boostDurationSec;
                if (boost >= 5.5f) return 3;
                if (boost >= 4f) return 2;
                if (boost >= 2.5f) return 1;
                return 0;
            }
        }
    }
}

