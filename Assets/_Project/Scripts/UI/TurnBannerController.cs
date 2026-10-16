using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Racing;

namespace QuickChecks.UI
{
    /// <summary>
    /// Turn banner — top-center indicator showing whose turn it is.
    /// Color-coded: Player 1 = cyan, Player 2/CPU = red, Player 3 = green, Player 4 = blue.
    /// Slides in from top when turn starts, slides out when turn ends.
    /// </summary>
    public class TurnBannerController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private Image bannerBackground;

        [Header("Animation")]
        [SerializeField] private float slideInDuration = 0.3f;
        [SerializeField] private float displayDuration = 1.5f;
        [SerializeField] private float slideOutDuration = 0.3f;
        [SerializeField] private float bannerHeight = 80f;
        [SerializeField] private float offscreenY = 100f;

        [Header("Colors (neon synthwave)")]
        [SerializeField] private Color player1Color = new Color(0.247f, 0.878f, 0.760f, 0.9f);  // cyan
        [SerializeField] private Color player2Color = new Color(1f, 0.42f, 0.42f, 0.9f);  // red
        [SerializeField] private Color player3Color = new Color(0.42f, 0.79f, 0.47f, 0.9f);  // green
        [SerializeField] private Color player4Color = new Color(0.42f, 0.58f, 1f, 0.9f);  // blue

        [Header("Solo mode")]
        [SerializeField] private Color soloColor = new Color(0.247f, 0.878f, 0.760f, 0.9f);

        private RectTransform _rect;
        private bool _isAnimating = false;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (bannerBackground != null) bannerBackground.color = Color.clear;
            if (bannerText != null) bannerText.text = "";
            HideInstant();
        }

        public void ShowTurnBanner(int playerIndex, string playerName, bool isMultiplayer)
        {
            if (_isAnimating) return;

            Color color = GetPlayerColor(playerIndex);
            string message;

            if (!isMultiplayer)
            {
                message = "YOUR TURN";
            }
            else if (playerIndex == 0)
            {
                message = "YOUR TURN";
            }
            else
            {
                message = $"{playerName.ToUpper()}'S TURN";
            }

            StartCoroutine(AnimateBanner(message, color));
        }

        private IEnumerator AnimateBanner(string message, Color color)
        {
            _isAnimating = true;

            // Setup
            if (bannerText != null)
            {
                bannerText.text = message;
                bannerText.color = Color.white;
            }
            if (bannerBackground != null)
            {
                bannerBackground.color = color;
            }

            // Slide in from top
            float elapsed = 0f;
            Vector2 startPos = new Vector2(0, offscreenY);
            Vector2 endPos = new Vector2(0, -bannerHeight / 2);

            while (elapsed < slideInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideInDuration);
                // Ease out
                t = 1f - Mathf.Pow(1f - t, 3f);
                _rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }
            _rect.anchoredPosition = endPos;

            // Display
            yield return new WaitForSeconds(displayDuration);

            // Slide out to top
            elapsed = 0f;
            startPos = _rect.anchoredPosition;

            while (elapsed < slideOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideOutDuration);
                // Ease in
                t = t * t * t;
                _rect.anchoredPosition = Vector2.Lerp(startPos, new Vector2(0, offscreenY), t);

                // Fade out
                if (bannerBackground != null)
                {
                    var c = bannerBackground.color;
                    c.a = color.a * (1f - t);
                    bannerBackground.color = c;
                }
                if (bannerText != null)
                {
                    var c = bannerText.color;
                    c.a = 1f - t;
                    bannerText.color = c;
                }
                yield return null;
            }

            HideInstant();
            _isAnimating = false;
        }

        private void HideInstant()
        {
            if (_rect != null) _rect.anchoredPosition = new Vector2(0, offscreenY);
            if (bannerBackground != null) bannerBackground.color = Color.clear;
            if (bannerText != null) bannerText.text = "";
        }

        private Color GetPlayerColor(int playerIndex)
        {
            return playerIndex switch
            {
                0 => player1Color,
                1 => player2Color,
                2 => player3Color,
                3 => player4Color,
                _ => soloColor
            };
        }
    }
}
