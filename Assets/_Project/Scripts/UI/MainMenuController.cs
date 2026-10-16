using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Audio;
using QuickChecks.Monetization;

namespace QuickChecks.UI
{
    /// <summary>
    /// Main menu controller (Phase 2). Shows title + PLAY + Settings.
    ///   PLAY     → goes to ModeSelect
    ///   SETTINGS → opens in-scene settings dialog (audio + accessibility stubs)
    ///
    /// Phase 2 additions:
    ///   - Subtitle gently pulses (cyan ↔ dim cyan) on a 2.5s loop.
    ///   - PLAY button has ButtonHoverFx (neon hover + tap animation).
    ///   - Settings opens a modal overlay with: music volume, SFX volume, restore purchases stub.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Settings Dialog (optional — wire in inspector or built at runtime)")]
        [SerializeField] private GameObject settingsDialog;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button restorePurchasesButton;
        [SerializeField] private TMP_Text settingsStatusLabel;

        [Header("Subtitle pulse (optional)")]
        [SerializeField] private float pulsePeriod = 2.5f;
        [SerializeField] private Color pulseDim = new(0.247f, 0.878f, 0.760f, 0.55f);
        [SerializeField] private Color pulseBright = new(0.247f, 0.878f, 0.760f, 1.0f);

        private bool _settingsOpen;

        private void Start()
        {
            if (titleText != null) titleText.text = "QuickChecks";
            if (subtitleText != null) subtitleText.text = "Swipe Golf Racing";

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
                EnsureHoverFx(playButton);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
                EnsureHoverFx(settingsButton);
            }

            // Settings dialog wiring
            if (settingsDialog != null)
            {
                settingsDialog.SetActive(false);
                if (settingsCloseButton != null)
                {
                    settingsCloseButton.onClick.RemoveAllListeners();
                    settingsCloseButton.onClick.AddListener(CloseSettings);
                    EnsureHoverFx(settingsCloseButton);
                }
                if (restorePurchasesButton != null)
                {
                    restorePurchasesButton.onClick.RemoveAllListeners();
                    restorePurchasesButton.onClick.AddListener(OnRestorePurchasesClicked);
                    EnsureHoverFx(restorePurchasesButton);
                }
                if (musicVolumeSlider != null)
                {
                    musicVolumeSlider.value = MusicPlayer.Instance != null ? MusicPlayer.Instance.Volume : 0.6f;
                    musicVolumeSlider.onValueChanged.RemoveAllListeners();
                    musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
                }
                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.value = SfxPlayer.Instance != null ? SfxPlayer.Instance.GetVolume() : 0.6f;
                    sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                    sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
                }
            }

            Debug.Log("[MainMenu] Ready. Tap PLAY to go to mode select.");
            StartCoroutine(PulseSubtitleRoutine());
        }

        private void OnPlayClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[MainMenu] PLAY clicked — going to mode select.");
            SceneManager.LoadScene("ModeSelect");
        }

        private void OnSettingsClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            OpenSettings();
        }

        private void OpenSettings()
        {
            if (settingsDialog == null)
            {
                Debug.Log("[MainMenu] Settings dialog not wired — would show audio + accessibility options.");
                return;
            }
            settingsDialog.SetActive(true);
            _settingsOpen = true;
            if (settingsStatusLabel != null) settingsStatusLabel.text = "";
            Debug.Log("[MainMenu] Settings dialog opened.");
        }

        private void CloseSettings()
        {
            if (settingsDialog == null) return;
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            settingsDialog.SetActive(false);
            _settingsOpen = false;
            Debug.Log("[MainMenu] Settings dialog closed.");
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.SetVolume(value);
            // Persist via PlayerPrefs for next session.
            PlayerPrefs.SetFloat("QC_MusicVolume", value);
            PlayerPrefs.Save();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.SetVolume(value);
            PlayerPrefs.SetFloat("QC_SfxVolume", value);
            PlayerPrefs.Save();
        }

        private void OnRestorePurchasesClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[MainMenu] Restore purchases clicked — calling IAPService.RestorePurchasesAsync().");
            if (settingsStatusLabel != null) settingsStatusLabel.text = "Restoring purchases...";
            StartCoroutine(RestorePurchasesRoutine());
        }

        private IEnumerator RestorePurchasesRoutine()
        {
            var iap = IAPService.Instance;
            if (iap != null)
            {
                var task = iap.RestorePurchasesAsync();
                while (!task.IsCompleted) yield return null;

                bool premium = EntitlementManager.Instance != null && EntitlementManager.Instance.IsPremium;
                if (settingsStatusLabel != null)
                {
                    settingsStatusLabel.text = premium
                        ? "Premium restored. All tracks unlocked!"
                        : "No prior purchase found.";
                }
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
                if (settingsStatusLabel != null) settingsStatusLabel.text = "IAP service unavailable.";
            }
        }

        private IEnumerator PulseSubtitleRoutine()
        {
            while (subtitleText != null)
            {
                float t = 0f;
                while (t < pulsePeriod)
                {
                    t += Time.unscaledDeltaTime;
                    // Sin pulse between dim and bright (smooth both directions)
                    float phase = (Mathf.Sin(t / pulsePeriod * Mathf.PI * 2f) + 1f) * 0.5f;
                    subtitleText.color = Color.Lerp(pulseDim, pulseBright, phase);
                    yield return null;
                }
            }
        }

        private static void EnsureHoverFx(Button btn)
        {
            if (btn == null) return;
            if (btn.GetComponent<ButtonHoverFx>() == null)
            {
                btn.gameObject.AddComponent<ButtonHoverFx>();
            }
        }
    }
}
