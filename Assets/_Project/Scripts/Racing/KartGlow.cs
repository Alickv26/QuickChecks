using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Pulsing neon glow ring around a kart. Activates when it's this kart's turn,
    /// deactivates when it's not. Creates a visual "this is the active kart" indicator.
    ///
    /// Implementation: a child GameObject with a SpriteRenderer using a ring texture,
    /// scaled larger than the kart, with pulsing alpha animation.
    /// </summary>
    public class KartGlow : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float minAlpha = 0.3f;
        [SerializeField] private float maxAlpha = 0.7f;
        [SerializeField] private float glowScale = 2.5f;
        [SerializeField] private Color glowColor = new Color(0.247f, 0.878f, 0.760f);  // cyan default

        private GameObject _glowObject;
        private SpriteRenderer _glowRenderer;
        private bool _isActive = false;
        private float _pulseTimer = 0f;

        private void Awake()
        {
            CreateGlowObject();
            SetActive(false);
        }

        private void CreateGlowObject()
        {
            _glowObject = new GameObject("KartGlow");
            _glowObject.transform.SetParent(transform, false);
            _glowObject.transform.localPosition = Vector3.zero;
            _glowObject.transform.localScale = new Vector3(glowScale, glowScale, 1);

            _glowRenderer = _glowObject.AddComponent<SpriteRenderer>();
            _glowRenderer.sprite = CreateRingSprite();
            _glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            _glowRenderer.sortingOrder = 9;  // Below kart (10), above trail (5)
            _glowRenderer.drawMode = SpriteDrawMode.Simple;
        }

        private Sprite _cachedRingSprite;
        private Sprite CreateRingSprite()
        {
            if (_cachedRingSprite == null)
            {
                const int size = 64;
                const int ringThickness = 6;
                var texture = new Texture2D(size, size);
                var pixels = new Color32[size * size];
                var transparent = new Color32(0, 0, 0, 0);
                var white = new Color32(255, 255, 255, 255);

                int center = size / 2;
                int outerRadius = center - 2;
                int innerRadius = outerRadius - ringThickness;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int dx = x - center;
                        int dy = y - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        int idx = y * size + x;
                        if (dist <= outerRadius && dist >= innerRadius)
                        {
                            // Soft edge: fade alpha at outer boundary
                            float edgeFade = 1f;
                            if (dist > outerRadius - 2) edgeFade = (outerRadius - dist) / 2f;
                            if (dist < innerRadius + 2) edgeFade = (dist - innerRadius) / 2f;
                            byte alpha = (byte)(255 * Mathf.Clamp01(edgeFade));
                            pixels[idx] = new Color32(255, 255, 255, alpha);
                        }
                        else
                        {
                            pixels[idx] = transparent;
                        }
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _cachedRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                _cachedRingSprite.name = "KartGlowRing";
            }
            return _cachedRingSprite;
        }

        private void Update()
        {
            if (!_isActive || _glowRenderer == null) return;

            _pulseTimer += Time.deltaTime * pulseSpeed;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;  // 0..1
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);

            var c = _glowRenderer.color;
            c.a = alpha;
            _glowRenderer.color = c;
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (_glowObject != null)
            {
                _glowObject.SetActive(active);
            }
            if (!active && _glowRenderer != null)
            {
                var c = _glowRenderer.color;
                c.a = 0f;
                _glowRenderer.color = c;
            }
        }

        public void SetGlowColor(Color color)
        {
            glowColor = color;
            if (_glowRenderer != null)
            {
                var c = _glowRenderer.color;
                c.r = color.r;
                c.g = color.g;
                c.b = color.b;
                _glowRenderer.color = c;
            }
        }
    }
}
