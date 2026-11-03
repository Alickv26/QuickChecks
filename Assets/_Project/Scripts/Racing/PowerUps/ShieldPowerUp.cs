using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// SHIELD — Defensive power-up.
    /// Effect: Negates the next offensive power-up effect (relevant in future PvP modes).
    ///
    /// TURN-BASED: Persists across turns (NOT expiresOnTurnEnd). Shield is consumed only
    /// when it actually blocks an offensive effect — so it lasts indefinitely until used
    /// or until the race ends. This is the correct defensive power-up semantic: you don't
    /// lose your shield just because your turn ended.
    ///
    /// In v1 (no offensive power-ups), Shield has minimal effect — it's a placeholder
    /// for future content updates. Still useful for habit formation: players learn
    /// to grab defensive power-ups when they're available.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Shield", fileName = "PowerUp_Shield")]
    public class ShieldPowerUp : PowerUpBase
    {
        public ShieldPowerUp()
        {
            powerUpId = "shield";
            displayName = "Shield";
            effectColor = new Color(0.306f, 0.804f, 0.769f);  // #4ECDC4 mint
            instantUse = true;
            durationSec = 0f;            // No time-based expiry
            expiresOnTurnEnd = false;    // Persists across turns — only consumed when blocking
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            // v1: Shield doesn't do anything mechanically (no offensive power-ups yet).
            // Future: kart.ShieldActive = true; kart.NegateNextOffensiveEffect()
            Debug.Log($"[Shield] Shield activated on {kart.gameObject.name}. " +
                      "Persists across turns until consumed by an offensive effect. " +
                      "(Note: no offensive power-ups in v1, so shield has no mechanical effect yet.)");
        }

        public override void OnExpire(KartController kart)
        {
            // Only called by PowerUpSystem if a new power-up replaces this one, or race ends.
            // NOT called on turn end (expiresOnTurnEnd = false).
            Debug.Log("[Shield] Shield cleared (replaced or race ended).");
        }
    }
}
