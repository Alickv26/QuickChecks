using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Renders a fading trail behind the kart. Each segment fades to 0 alpha over ~3 seconds.
    /// Visual purpose: player can see their swipe path, which makes "fewest swipes" tangible —
    /// you can literally see if you took 4 swipes or 6 to cover the same distance.
    ///
    /// Implementation: maintains a list of recent positions + timestamps, draws them via
    /// a LineRenderer with per-vertex colors (gradient alpha fade). Cheaper than particles
    /// and works on mobile.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class KartTrail : MonoBehaviour
    {
        [SerializeField] private KartController kart;
        [SerializeField] private int maxPoints = 60;
        [SerializeField] private float sampleIntervalSec = 0.05f;
        [SerializeField] private float fadeDurationSec = 3f;
        [SerializeField] private Gradient colorGradient;

        private LineRenderer _line;
        private readonly List<Vector3> _points = new();
        private readonly List<float> _timestamps = new();
        private float _lastSampleTime;

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

            if (colorGradient == null)
            {
                // Default: bright cyan at head, fading to transparent.
                var grad = new Gradient();
                grad.SetKeys(
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
                colorGradient = grad;
            }
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

            // Prune points older than fadeDuration.
            float now = Time.time;
            for (int i = _points.Count - 1; i >= 0; i--)
            {
                if (now - _timestamps[i] > fadeDurationSec)
                {
                    _points.RemoveAt(i);
                    _timestamps.RemoveAt(i);
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

        public void Clear()
        {
            _points.Clear();
            _timestamps.Clear();
            _line.positionCount = 0;
        }
    }
}
