using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Abstract base for all power-ups. Subclasses implement specific effects.
    /// Power-ups are collected by driving through a PowerUpPickup trigger
    /// on the track. Each kart has 1 slot — collecting a new power-up
    /// replaces the old one (except for instant-use power-ups like Slingshot).
    /// </summary>
    public abstract class PowerUpBase : ScriptableObject
    {
        public string powerUpId;
        public string displayName;
        public Sprite icon;
        public Color effectColor = Color.white;

        [Tooltip("If true, effect applies immediately on pickup. If false, kart holds it and uses on tap.")]
        public bool instantUse = false;

        [Tooltip("Duration of effect in seconds. 0 = instant.")]
        public float durationSec = 0f;

        /// <summary>
        /// Called when the kart picks up this power-up.
        /// </summary>
        public abstract void OnPickup(KartController kart);

        /// <summary>
        /// Called when player taps the power-up button (only for non-instant).
        /// </summary>
        public virtual void OnUse(KartController kart) { }
    }
}
