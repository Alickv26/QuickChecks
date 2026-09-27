using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// REWIND — Defensive power-up.
    /// Effect: On use, kart rewinds 1 second of position history.
    /// Duration: 1 use (held until used, no expiry).
    /// Pickup: Held (player must tap Use button to activate).
    ///
    /// Strategy tip: Best used when you've swiped into a corner and want to retry
    /// the previous shot. The kart's position rewinds ~1 second back.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Rewind", fileName = "PowerUp_Rewind")]
    public class RewindPowerUp : PowerUpBase
    {
        [Tooltip("How many seconds to rewind when Use() is called.")]
        public float rewindDurationSec = 1f;

        public RewindPowerUp()
        {
            powerUpId = "rewind";
            displayName = "Rewind";
            effectColor = new Color(0.42f, 0.792f, 0.467f);  // #6BCB77 green
            instantUse = false;  // Held — player must tap to use
            durationSec = 0f;    // No ticking (resolves immediately on use)
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            // For held power-ups, OnPickup just confirms the pickup. Effect applies on Use().
            Debug.Log($"[Rewind] Held. Press Use button to rewind {rewindDurationSec}s.");
        }

        public override void OnUse(KartController kart)
        {
            if (kart == null) return;
            kart.Rewind(rewindDurationSec);
            Debug.Log($"[Rewind] Used: rewound {rewindDurationSec}s.");
        }
    }
}
