using UnityEngine;

namespace QuickChecks.Track.Obstacles
{
    /// <summary>
    /// Solid wall placed inside the track. Must be narrower than the track width
    /// so the player can always go around it. Collision stops the kart dead
    /// (golf-feel, same as boundary walls).
    ///
    /// No special code needed — just a BoxCollider2D (NOT a trigger).
    /// KartController.OnCollisionEnter2D handles the stop.
    /// This component is just for visual + identification.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ObstacleWall : MonoBehaviour
    {
        [SerializeField] private Color wallColor = new Color(0.8f, 0.3f, 0.3f, 0.7f);  // muted red

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = false;  // Solid — kart collides and stops

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = wallColor;
        }
    }
}
