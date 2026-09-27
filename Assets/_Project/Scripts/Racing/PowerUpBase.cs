using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Abstract base for all power-ups. Subclasses implement specific effects.
    /// Power-ups are collected by driving through a PowerUpPickup trigger
    /// on the track. Each kart has 1 slot — collecting a new power-up
    /// replaces the old one (except for instant-use power-ups like Slingshot).
    ///
    /// Concrete subclasses (in Scripts/Racing/PowerUps/):
    ///   - BoostPowerUp      (Speed/utility, instant use, 1.5s duration)
    ///   - SlingshotPowerUp  (Speed/utility, instant use, immediate effect)
    ///   - PhaseDodgePowerUp (Speed/utility, holds until used or 5s expires)
    ///   - ShieldPowerUp     (Defensive, instant use, 10s duration)
    ///   - RewindPowerUp     (Defensive, holds, 1 use)
    ///   - MagnetPowerUp     (Defensive, instant use, 6s duration)
    /// </summary>
    public abstract class PowerUpBase : ScriptableObject
    {
        [Header("Identity")]
        public string powerUpId;
        public string displayName;
        public Sprite icon;

        [Header("Visual")]
        [Tooltip("Color used for the pickup sprite on track + UI icon background.")]
        public Color effectColor = Color.white;

        [Header("Behavior")]
        [Tooltip("If true, effect applies immediately on pickup. If false, kart holds it and uses on tap.")]
        public bool instantUse = false;

        [Tooltip("Duration of effect in seconds. 0 = instant (resolves immediately, no ticking).")]
        public float durationSec = 0f;

        /// <summary>Called when the kart picks up this power-up. For instant-use, applies effect here.</summary>
        public abstract void OnPickup(KartController kart);

        /// <summary>Called when player taps the power-up button (only for non-instant power-ups).</summary>
        public virtual void OnUse(KartController kart) { }

        /// <summary>Called every frame while the power-up effect is active (for ticking effects like Magnet).</summary>
        public virtual void OnUpdate(KartController kart, float elapsedSec) { }

        /// <summary>Called when the effect duration expires (cleanup — reset kart state, etc.).</summary>
        public virtual void OnExpire(KartController kart) { }
    }
}
