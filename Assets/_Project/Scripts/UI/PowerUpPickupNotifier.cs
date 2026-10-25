using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Racing;

namespace QuickChecks.UI
{
    /// <summary>
    /// In-race toast that pops up briefly whenever the player's kart collects a power-up.
    /// Shows the power-up's displayName + effectDescription, then fades out after 2.5s.
    ///
    /// Built + wired in RaceSceneController.BuildHUD(). Listens to PowerUpSystem.OnCollected
    /// on the player's kart. Doesn't fire for CPU/opponent karts in multiplayer (we'd need
    /// per-kart notifiers for that — out of scope for v1).
    ///
    /// Style: dark panel with a colored accent bar (matching the power-up's effectColor),
    /// large name, small description below. Anchored to the top-center of the screen.
    /// </summary>
    public class PowerUpPickupNotifier : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image accentBar;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Timing")]
        [Tooltip("Seconds the toast stays fully visible before starting to fade.")]
        [SerializeField] private float visibleDuration = 1.8f;
        [Tooltip("Seconds the fade-out takes after visibleDuration.")]
        [SerializeField] private float fadeDuration = 0.7f;

        [Header("Colors")]
        [SerializeField] private Color panelColor = new Color(0.055f, 0.078f, 0.078f, 0.92f);

        private CanvasGroup _canvasGroup;
        private Coroutine _fadeRoutine;
        private PowerUpSystem _subscribedSystem;

        private void Awake()
        {
            _canvasGroup = panelRoot?.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && panelRoot != null)
            {
                _canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Subscribes to a kart's PowerUpSystem.OnCollected event. Called by
        /// RaceSceneController after the player's kart is built.
        /// </summary>
        public void WatchKart(PowerUpSystem powerUpSystem)
        {
            Unsubscribe();
            if (powerUpSystem == null) return;
            _subscribedSystem = powerUpSystem;
            _subscribedSystem.OnCollected += OnPowerUpCollected;
        }

        private void Unsubscribe()
        {
            if (_subscribedSystem != null)
            {
                _subscribedSystem.OnCollected -= OnPowerUpCollected;
                _subscribedSystem = null;
            }
        }

        private void OnPowerUpCollected(PowerUpBase powerUp)
        {
            if (powerUp == null) return;
            Show(powerUp);
        }

        /// <summary>Shows the toast with the power-up's name + description + accent color.</summary>
        public void Show(PowerUpBase powerUp)
        {
            if (panelRoot == null) return;

            // Populate
            if (nameText != null) nameText.text = powerUp.displayName ?? powerUp.powerUpId ?? "Power-Up";
            if (descriptionText != null) descriptionText.text = powerUp.effectDescription ?? "";
            if (accentBar != null) accentBar.color = powerUp.effectColor;

            // Make panel visible — this also activates the PowerUpPickupNotifier GameObject
            // itself (which must be active for StartCoroutine to work)
            panelRoot.SetActive(true);
            gameObject.SetActive(true);
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            // Restart the fade-out timer
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            // Stay fully visible for visibleDuration seconds
            yield return new WaitForSeconds(visibleDuration);

            // Fade out over fadeDuration seconds
            if (_canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                _canvasGroup.alpha = 0f;
            }

            if (panelRoot != null) panelRoot.SetActive(false);
            _fadeRoutine = null;
        }
    }
}
