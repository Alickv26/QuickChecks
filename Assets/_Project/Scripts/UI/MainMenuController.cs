using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Audio;
using QuickChecks.Monetization;
using QuickChecks.Multiplayer;

namespace QuickChecks.UI
{
    /// <summary>
    /// Main menu controller (Phase 2). Shows title + PLAY + Settings.
    ///   PLAY     → goes to ModeSelect
    ///   SETTINGS → opens in-scene settings dialog (audio + account + IAP)
    ///
    /// Settings dialog sections (top to bottom):
    ///   - Account: display name (editable) + email link (input + button) + account status
    ///   - Audio:   music volume + SFX volume sliders
    ///   - IAP:     restore purchases button + status
    ///
    /// Display name + email link both call SupabaseAuth methods.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Settings Dialog")]
        [SerializeField] private GameObject settingsDialog;
        [SerializeField] private Button settingsCloseButton;

        [Header("Settings — Account section")]
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private Button saveDisplayNameButton;
        [SerializeField] private TMP_Text accountStatusLabel;       // shows current email or "Not linked"
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button linkAccountButton;

        [Header("Settings — Audio section")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Settings — IAP section")]
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

            WireSettingsDialog();

            Debug.Log("[MainMenu] Ready. Tap PLAY to go to mode select.");
            StartCoroutine(PulseSubtitleRoutine());
        }

        private void WireSettingsDialog()
        {
            if (settingsDialog == null)
            {
                Debug.LogWarning("[MainMenu] Settings dialog not wired.");
                return;
            }
            settingsDialog.SetActive(false);

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.RemoveAllListeners();
                settingsCloseButton.onClick.AddListener(CloseSettings);
                EnsureHoverFx(settingsCloseButton);
            }

            // Account section
            if (saveDisplayNameButton != null)
            {
                saveDisplayNameButton.onClick.RemoveAllListeners();
                saveDisplayNameButton.onClick.AddListener(OnSaveDisplayNameClicked);
                EnsureHoverFx(saveDisplayNameButton);
            }
            if (linkAccountButton != null)
            {
                linkAccountButton.onClick.RemoveAllListeners();
                linkAccountButton.onClick.AddListener(OnLinkAccountClicked);
                EnsureHoverFx(linkAccountButton);
            }

            // Audio section
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

            // IAP section
            if (restorePurchasesButton != null)
            {
                restorePurchasesButton.onClick.RemoveAllListeners();
                restorePurchasesButton.onClick.AddListener(OnRestorePurchasesClicked);
                EnsureHoverFx(restorePurchasesButton);
            }
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
                Debug.Log("[MainMenu] Settings dialog not wired — would show audio + account + IAP options.");
                return;
            }
            settingsDialog.SetActive(true);
            _settingsOpen = true;
            if (settingsStatusLabel != null) settingsStatusLabel.text = "";

            // Populate account section from current SupabaseAuth state.
            RefreshAccountSection();

            Debug.Log("[MainMenu] Settings dialog opened.");
        }

        /// <summary>
        /// Refreshes the account section: pre-fills display name + shows email link status.
        /// Called when the dialog opens + after any account action.
        /// </summary>
        private void RefreshAccountSection()
        {
            if (displayNameInput != null)
            {
                string name = SupabaseAuth.Instance != null ? SupabaseAuth.Instance.DisplayName : "";
                if (string.IsNullOrEmpty(name) && GameFlowManager.Instance != null)
                {
                    name = GameFlowManager.Instance.profile?.displayName ?? "";
                }
                displayNameInput.text = name ?? "";
            }

            if (accountStatusLabel != null)
            {
                bool signedIn = SupabaseAuth.Instance != null && SupabaseAuth.Instance.IsSignedIn;
                if (signedIn)
                {
                    string userId = SupabaseAuth.Instance.UserId ?? "";
                    string shortId = userId.Length > 8 ? userId.Substring(0, 8) : userId;
                    accountStatusLabel.text = $"Signed in (ID: {shortId}…)\n" +
                                              "Link an email to recover progress on a new device.";
                    accountStatusLabel.color = Phase2UiPalette.AccentCyan;
                }
                else
                {
                    accountStatusLabel.text = "Not signed in.\nAnonymous account will be created on first launch.";
                    accountStatusLabel.color = Phase2UiPalette.TextDim;
                }
            }

            if (emailInput != null) emailInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
        }

        private void CloseSettings()
        {
            if (settingsDialog == null) return;
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            settingsDialog.SetActive(false);
            _settingsOpen = false;
            Debug.Log("[MainMenu] Settings dialog closed.");
        }

        // =====================================================================
        // Account section handlers
        // =====================================================================

        private void OnSaveDisplayNameClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            if (displayNameInput == null) return;
            string newName = displayNameInput.text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                SetAccountStatus("Display name can't be empty.", false);
                return;
            }
            if (newName.Length > 24)
            {
                SetAccountStatus("Display name too long (max 24 chars).", false);
                return;
            }

            // If Supabase isn't ready yet, save locally only + the next sign-in will sync.
            if (SupabaseAuth.Instance == null || !SupabaseAuth.Instance.IsSignedIn)
            {
                if (GameFlowManager.Instance != null && GameFlowManager.Instance.profile != null)
                {
                    GameFlowManager.Instance.profile.displayName = newName;
                    GameFlowManager.Instance.profile.Save();
                }
                SetAccountStatus("Saved locally. Will sync after sign-in.", true);
                return;
            }

            SetAccountStatus("Saving...", true);
            StartCoroutine(SaveDisplayNameRoutine(newName));
        }

        private IEnumerator SaveDisplayNameRoutine(string newName)
        {
            bool ok = false;
            yield return SupabaseAuth.Instance.UpdateDisplayNameAsync(newName, success => ok = success);

            if (ok)
            {
                // Mirror to local PlayerProfile too.
                if (GameFlowManager.Instance != null && GameFlowManager.Instance.profile != null)
                {
                    GameFlowManager.Instance.profile.displayName = newName;
                    GameFlowManager.Instance.profile.Save();
                }
                SetAccountStatus($"Display name updated to '{newName}'.", true);
            }
            else
            {
                SetAccountStatus("Failed to update display name. Try again.", false);
            }
        }

        private void OnLinkAccountClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            if (emailInput == null || passwordInput == null) return;

            string email = emailInput.text?.Trim();
            string password = passwordInput.text;

            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                SetAccountStatus("Please enter a valid email.", false);
                return;
            }
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                SetAccountStatus("Password must be at least 6 characters.", false);
                return;
            }

            if (SupabaseAuth.Instance == null || !SupabaseAuth.Instance.IsSignedIn)
            {
                SetAccountStatus("Not signed in yet. Try again in a moment.", false);
                return;
            }

            SetAccountStatus("Linking email...", true);
            StartCoroutine(LinkAccountRoutine(email, password));
        }

        private IEnumerator LinkAccountRoutine(string email, string password)
        {
            string errorMsg = null;
            bool ok = false;
            yield return SupabaseAuth.Instance.LinkEmailAsync(email, password,
                (success, err) =>
                {
                    ok = success;
                    errorMsg = err;
                });

            if (ok)
            {
                SetAccountStatus($"Email '{email}' linked. You can now sign in on any device.", true);
                if (passwordInput != null) passwordInput.text = "";
            }
            else
            {
                string friendly = ParseSupabaseError(errorMsg);
                SetAccountStatus(friendly, false);
            }
        }

        /// <summary>Translates Supabase error JSON into a human-readable message.</summary>
        private static string ParseSupabaseError(string errorJson)
        {
            if (string.IsNullOrEmpty(errorJson)) return "Failed to link email. Try again.";
            try
            {
                var root = Newtonsoft.Json.Linq.JObject.Parse(errorJson);
                string code = root["code"]?.ToString() ?? "";
                string msg = root["message"]?.ToString() ?? "";
                if (code == "email_exists" || msg.Contains("already been registered"))
                    return "That email is already linked to another account.";
                if (code == "weak_password" || msg.Contains("Password"))
                    return "Password is too weak. Use at least 6 characters.";
                if (code == "invalid_email" || msg.Contains("email"))
                    return "Invalid email address.";
                return $"Failed: {msg}";
            }
            catch
            {
                return "Failed to link email. Try again.";
            }
        }

        private void SetAccountStatus(string message, bool success)
        {
            if (accountStatusLabel == null) return;
            accountStatusLabel.text = message;
            accountStatusLabel.color = success ? Phase2UiPalette.AccentCyan : new Color(1f, 0.42f, 0.42f);
        }

        // =====================================================================
        // Audio section handlers
        // =====================================================================

        private void OnMusicVolumeChanged(float value)
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.SetVolume(value);
            PlayerPrefs.SetFloat("QC_MusicVolume", value);
            PlayerPrefs.Save();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.SetVolume(value);
            PlayerPrefs.SetFloat("QC_SfxVolume", value);
            PlayerPrefs.Save();
        }

        // =====================================================================
        // IAP section handlers
        // =====================================================================

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
                    settingsStatusLabel.color = premium
                        ? Phase2UiPalette.AccentCyan
                        : new Color(1f, 0.82f, 0.40f);
                }
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
                if (settingsStatusLabel != null)
                {
                    settingsStatusLabel.text = "IAP service unavailable.";
                    settingsStatusLabel.color = new Color(1f, 0.42f, 0.42f);
                }
            }
        }

        // =====================================================================
        // Subtitle pulse animation
        // =====================================================================

        private IEnumerator PulseSubtitleRoutine()
        {
            while (subtitleText != null)
            {
                float t = 0f;
                while (t < pulsePeriod)
                {
                    t += Time.unscaledDeltaTime;
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
