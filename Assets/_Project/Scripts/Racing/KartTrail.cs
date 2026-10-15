using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Renders a fading trail behind the kart. Each segment fades to 0 alpha over ~3 seconds.
    ///
    /// TRAIL WALL MODE: When trailWallActive is true, the trail does NOT fade — it stays
    /// visible as a solid pink barrier until the player's turn comes again. This creates
    /// a "wall" that other karts can see and must avoid. When the player's turn starts
    /// again, the trail wall is cleared (normal fading resumes).
    ///
    /// Implementation: maintains a list of recent positions + timestamps, draws them via
    /// a LineRenderer with per-vertex colors (gradient alpha fade). In trail wall mode,
    /// the fade is disabled and the color changes to hot pink.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class KartTrail : MonoBehaviour
    {
        [SerializeField] private KartController kart;
        [SerializeField] private int maxPoints = 60;
        [SerializeField] private float sampleIntervalSec = 0.05f;
        [SerializeField] private float fadeDurationSec = 3f;
        [SerializeField] private Gradient colorGradient;

        // Trail wall state
        private bool _trailWallActive = false;
        private static readonly Color WALL_COLOR = new Color(1f, 0.42f, 0.79f, 0.9f);  // hot pink
        private static readonly Color WALL_COLOR_END = new Color(1f, 0.42f, 0.79f, 0.5f);  // pink, semi-transparent

        private LineRenderer _line;
        private readonly List<Vector3> _points = new();
        private readonly List<float> _timestamps = new();
        private float _lastSampleTime;
        private Gradient _normalGradient;
        private Gradient _wallGradient;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = 0;
            _line.useWorldSpace = true;
            _line.numCornerVertices = 4;
            _line.numCapVertices = 4;
            _line.alignment = LineAlignment.View;
            _line.startWidth = 0.4f;
            _line.endWidth = 0.2f;
            _line.sortingOrder = 5;
            _line.material = new Material(Shader.Find("Sprites/Default"));

            // Normal gradient: cyan → yellow, fading to transparent
            _normalGradient = new Gradient();
            _normalGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.247f, 0.878f, 0.760f), 0f),
                    new GradientColorKey(new Color(1f, 0.82f, 0.4f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );

            // Wall gradient: solid pink, no fade
            _wallGradient = new Gradient();
            _wallGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(WALL_COLOR, 0f),
                    new GradientColorKey(WALL_COLOR_END, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.5f, 1f)
                }
            );

            colorGradient = _normalGradient;
            _line.colorGradient = colorGradient;
        }

        private void Update()
        {
            if (kart == null) return;

            // Sample position when kart is moving + interval has elapsed.
            if (Time.time - _lastSampleTime >= sampleIntervalSec)
            {
                _lastSampleTime = Time.time;
                _points.Add(kart.transform.position);
                _timestamps.Add(Time.time);

                // Trim old points (cap memory).
                while (_points.Count > maxPoints)
                {
                    _points.RemoveAt(0);
                    _timestamps.RemoveAt(0);
                }
            }

            // In trail wall mode: DON'T prune old points (trail stays as barrier).
            // In normal mode: prune points older than fadeDuration.
            if (!_trailWallActive)
            {
                float now = Time.time;
                for (int i = _points.Count - 1; i >= 0; i--)
                {
                    if (now - _timestamps[i] > fadeDurationSec)
                    {
                        _points.RemoveAt(i);
                        _timestamps.RemoveAt(i);
                    }
                }
            }

            // Update line renderer.
            if (_points.Count >= 2)
            {
                _line.positionCount = _points.Count;
                _line.SetPositions(_points.ToArray());
            }
            else
            {
                _line.positionCount = 0;
            }
        }

        /// <summary>
        /// Activates trail wall mode: trail stops fading, turns pink, stays as a barrier.
        /// Called when TrailWall power-up is consumed (on next swipe).
        /// </summary>
        public void ActivateTrailWall()
        {
            _trailWallActive = true;
            _line.colorGradient = _wallGradient;
            _line.startWidth = 0.6f;  // Thicker for visibility as a "wall"
            _line.endWidth = 0.6f;
            _line.sortingOrder = 6;  // Above normal trails
            Debug.Log($"[KartTrail] Trail wall ACTIVATED — trail stays as pink barrier for {kart.gameObject.name}.");
        }

        /// <summary>
        /// Deactivates trail wall mode: trail resumes normal fading.
        /// Called when it's this player's turn again (trail wall disappears).
        /// </summary>
        public void DeactivateTrailWall()
        {
            _trailWallActive = false;
            _line.colorGradient = _normalGradient;
            _line.startWidth = 0.4f;
            _line.endWidth = 0.2f;
            _line.sortingOrder = 5;
            // Clear the wall trail points (fresh start for new turn)
            _points.Clear();
            _timestamps.Clear();
            _line.positionCount = 0;
            Debug.Log($"[KartTrail] Trail wall DEACTIVATED — trail cleared, normal fading resumed.");
        }

        public bool IsTrailWallActive => _trailWallActive;

        public void Clear()
        {
            _points.Clear();
            _timestamps.Clear();
            _line.positionCount = 0;
        }
    }
}
