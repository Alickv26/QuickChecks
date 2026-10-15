using UnityEngine;

namespace QuickChecks.Track.Obstacles
{
    /// <summary>
    /// Gravel spot obstacle. When a kart is inside the gravel area:
    /// - Friction is massively increased (kart decelerates much faster)
    /// - Swipe impulse is reduced (swipes move the kart significantly less)
    /// - The kart can still move, but each swipe only covers ~30% normal distance
    /// - Requires more swipes to escape the gravel trap
    ///
    /// Visual: brown/tan semi-transparent rectangle.
    /// Behavior: trigger collider. Sets a flag on the KartController when inside.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class GravelSpot : MonoBehaviour
    {
        [Tooltip("Friction multiplier when inside gravel (0.5 = twice the friction, 0.1 = 10x).")]
        public float gravelFrictionMultiplier = 0.05f;

        [Tooltip("Swipe impulse multiplier when inside gravel (0.3 = 30% normal swipe).")]
        public float gravelSwipeMultiplier = 0.3f;

        [Tooltip("Color of the gravel patch.")]
        public Color gravelColor = new Color(0.6f, 0.5f, 0.3f, 0.4f);  // tan, semi-transparent

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = gravelColor;
                sr.sortingOrder = 2;  // Above track surface, below karts
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var kart = other.GetComponent<Racing.KartController>();
            if (kart == null) return;

            kart.SetGravelMode(true, gravelFrictionMultiplier, gravelSwipeMultiplier);
            Debug.Log($"[GravelSpot] {kart.gameObject.name} entered gravel — friction x{gravelFrictionMultiplier}, swipe x{gravelSwipeMultiplier}.");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var kart = other.GetComponent<Racing.KartController>();
            if (kart == null) return;

            kart.SetGravelMode(false, 1f, 1f);
            Debug.Log($"[GravelSpot] {kart.gameObject.name} exited gravel — normal friction/swipe restored.");
        }
    }
}
