#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.UI;
using QuickChecks.Prototype;

namespace QuickChecks.Editor
{
    /// <summary>
    /// One-click builder for all 4 game scenes:
    ///   MainMenu, TrackSelect, Race, Results
    ///
    /// Usage: Tools > QuickChecks > Build Game Scenes
    /// </summary>
    public static class GameScenesBuilder
    {
        private const string SCENES_DIR = "Assets/_Project/Scenes/";

        [MenuItem("Tools/QuickChecks/Build Game Scenes")]
        public static void BuildAllScenes()
        {
            System.IO.Directory.CreateDirectory(SCENES_DIR);

            // Ensure GameFlowManager exists in a persistent scene
            BuildGameFlowManager();

            BuildMainMenuScene();
            BuildModeSelectScene();
            BuildTrackSelectScene();
            BuildKartSelectScene();
            BuildRaceScene();
            BuildResultsScene();

            // Add scenes to Build Settings
            AddScenesToBuildSettings();

            EditorUtility.DisplayDialog(
                "Game Scenes Built",
                "All 6 game scenes created:\n\n" +
                "• MainMenu.unity\n" +
                "• ModeSelect.unity\n" +
                "• TrackSelect.unity\n" +
                "• KartSelect.unity\n" +
                "• Race.unity\n" +
                "• Results.unity\n\n" +
                "Added to Build Settings.\n" +
                "Open MainMenu to start the game flow.",
                "OK"
            );

            EditorSceneManager.OpenScene(SCENES_DIR + "MainMenu.unity");
        }

        private static void BuildGameFlowManager()
        {
            // GameFlowManager is created at runtime by each scene, but we need
            // to ensure the persistent prefab exists. For prototype, we just
            // create it as a DontDestroyOnLoad singleton in each scene.
        }

        private static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = Phase2UiPalette.Background;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title (centered, accent cyan)
            var titleText = Phase2UiPalette.CreateText("Title", "QuickChecks", 96, Phase2UiPalette.AccentCyan,
                new Vector2(0.5f, 0.72f), new Vector2(900, 120));

            // Subtitle (pulses at runtime — see MainMenuController)
            var subtitleText = Phase2UiPalette.CreateText("Subtitle", "Swipe Golf Racing", 42,
                new Color(0.247f, 0.878f, 0.760f, 0.7f),
                new Vector2(0.5f, 0.62f), new Vector2(700, 60));

            // PLAY button (large, accent cyan)
            var playGo = new GameObject("PlayButton");
            playGo.transform.SetParent(canvasGo.transform, false);
            var playBtn = playGo.AddComponent<Button>();
            var playImg = playGo.AddComponent<Image>();
            playImg.color = Phase2UiPalette.AccentCyan;
            playImg.raycastTarget = true;
            playBtn.targetGraphic = playImg;
            var playRect = playGo.GetComponent<RectTransform>();
            playRect.anchorMin = new Vector2(0.5f, 0.40f);
            playRect.anchorMax = new Vector2(0.5f, 0.40f);
            playRect.pivot = new Vector2(0.5f, 0.5f);
            playRect.sizeDelta = new Vector2(500, 130);

            var plGo = new GameObject("Label");
            plGo.transform.SetParent(playGo.transform, false);
            var plLabel = plGo.AddComponent<TextMeshProUGUI>();
            plLabel.text = "PLAY";
            plLabel.fontSize = 64;
            plLabel.alignment = TextAlignmentOptions.Center;
            plLabel.color = Phase2UiPalette.TextOnAccent;
            plLabel.raycastTarget = false;
            var plR = plGo.GetComponent<RectTransform>();
            plR.anchorMin = Vector2.zero; plR.anchorMax = Vector2.one;
            plR.pivot = new Vector2(0.5f, 0.5f);
            plR.offsetMin = Vector2.zero; plR.offsetMax = Vector2.zero;

            // Settings button (smaller, dark teal)
            var settingsGo = new GameObject("SettingsButton");
            settingsGo.transform.SetParent(canvasGo.transform, false);
            var settingsBtn = settingsGo.AddComponent<Button>();
            var settingsImg = settingsGo.AddComponent<Image>();
            settingsImg.color = Phase2UiPalette.PanelSurface;
            settingsImg.raycastTarget = true;
            settingsBtn.targetGraphic = settingsImg;
            var settingsRect = settingsGo.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.5f, 0.25f);
            settingsRect.anchorMax = new Vector2(0.5f, 0.25f);
            settingsRect.pivot = new Vector2(0.5f, 0.5f);
            settingsRect.sizeDelta = new Vector2(340, 90);

            var slGo = new GameObject("Label");
            slGo.transform.SetParent(settingsGo.transform, false);
            var slLabel = slGo.AddComponent<TextMeshProUGUI>();
            slLabel.text = "Settings";
            slLabel.fontSize = 38;
            slLabel.alignment = TextAlignmentOptions.Center;
            slLabel.color = Phase2UiPalette.TextPrimary;
            slLabel.raycastTarget = false;
            var slR = slGo.GetComponent<RectTransform>();
            slR.anchorMin = Vector2.zero; slR.anchorMax = Vector2.one;
            slR.pivot = new Vector2(0.5f, 0.5f);
            slR.offsetMin = Vector2.zero; slR.offsetMax = Vector2.zero;

            // Footer (player display name + premium status)
            var footerText = Phase2UiPalette.CreateText("Footer", "", 22,
                Phase2UiPalette.TextDim,
                new Vector2(0.5f, 0.05f), new Vector2(900, 30),
                TextAlignmentOptions.Center);

            // --- Settings dialog (hidden by default, controlled by MainMenuController) ---
            var settingsDialog = BuildSettingsDialog(canvasGo.transform);

            // GameFlowManager (persistent singleton)
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // AudioBootstrap (creates MusicPlayer + SfxPlayer, loads track.mp3)
            var audioGo = new GameObject("[AudioBootstrap]");
            audioGo.AddComponent<QuickChecks.Audio.AudioBootstrap>();

            // MainMenuController — wire all the references
            var controllerGo = new GameObject("[MainMenuController]");
            var controller = controllerGo.AddComponent<MainMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("subtitleText").objectReferenceValue = subtitleText;

            // Settings dialog refs
            so.FindProperty("settingsDialog").objectReferenceValue = settingsDialog.Root;
            so.FindProperty("settingsCloseButton").objectReferenceValue = settingsDialog.CloseButton;
            so.FindProperty("displayNameInput").objectReferenceValue = settingsDialog.DisplayNameInput;
            so.FindProperty("saveDisplayNameButton").objectReferenceValue = settingsDialog.SaveDisplayNameButton;
            so.FindProperty("accountStatusLabel").objectReferenceValue = settingsDialog.AccountStatusLabel;
            so.FindProperty("emailInput").objectReferenceValue = settingsDialog.EmailInput;
            so.FindProperty("passwordInput").objectReferenceValue = settingsDialog.PasswordInput;
            so.FindProperty("linkAccountButton").objectReferenceValue = settingsDialog.LinkAccountButton;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = settingsDialog.MusicSlider;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = settingsDialog.SfxSlider;
            so.FindProperty("restorePurchasesButton").objectReferenceValue = settingsDialog.RestoreButton;
            so.FindProperty("settingsStatusLabel").objectReferenceValue = settingsDialog.StatusLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "MainMenu.unity");
            Debug.Log("[GameScenesBuilder] MainMenu scene built (Phase 2 neon layout + settings dialog).");
        }

        /// <summary>
        /// Builds the in-scene settings dialog used by the MainMenu.
        /// Sections (top to bottom):
        ///   - Account: display name input + save button + status + email/password inputs + link button
        ///   - Audio:   music volume slider + SFX volume slider
        ///   - IAP:     restore purchases button + status text
        /// Hidden by default — MainMenuController toggles visibility.
        /// </summary>
        private static SettingsDialogRefs BuildSettingsDialog(Transform canvas)
        {
            var rootGo = new GameObject("SettingsDialog");
            rootGo.transform.SetParent(canvas, false);

            // Full-screen dim overlay (visual only — raycastTarget=false so clicks pass
            // through to the panel + buttons underneath). Without this, the dim Image
            // would intercept every click on the dialog area and the close button would
            // never receive the click event.
            var dimImg = rootGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            dimImg.raycastTarget = false;  // CRITICAL — clicks must reach buttons underneath
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Center panel — taller now to fit account + audio + IAP sections
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(rootGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = Phase2UiPalette.PanelSurface;
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900, 1400);

            // Title
            var titleGo = new GameObject("DialogTitle");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "Settings";
            titleText.fontSize = 56;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Phase2UiPalette.AccentCyan;
            titleText.raycastTarget = false;
            var titleR = titleGo.GetComponent<RectTransform>();
            titleR.anchorMin = new Vector2(0.5f, 1);
            titleR.anchorMax = new Vector2(0.5f, 1);
            titleR.pivot = new Vector2(0.5f, 1);
            titleR.anchoredPosition = new Vector2(0, -30);
            titleR.sizeDelta = new Vector2(700, 80);

            // Close button (top-right)
            var closeGo = new GameObject("CloseButton");
            closeGo.transform.SetParent(panelGo.transform, false);
            var closeBtn = closeGo.AddComponent<Button>();
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            closeBtn.targetGraphic = closeImg;
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-20, -20);
            closeRect.sizeDelta = new Vector2(80, 80);

            var clGo = new GameObject("Label");
            clGo.transform.SetParent(closeGo.transform, false);
            var clText = clGo.AddComponent<TextMeshProUGUI>();
            clText.text = "X";
            clText.fontSize = 48;
            clText.alignment = TextAlignmentOptions.Center;
            clText.color = Phase2UiPalette.TextPrimary;
            clText.raycastTarget = false;
            var clR = clGo.GetComponent<RectTransform>();
            clR.anchorMin = Vector2.zero; clR.anchorMax = Vector2.one;
            clR.pivot = new Vector2(0.5f, 0.5f);
            clR.offsetMin = Vector2.zero; clR.offsetMax = Vector2.zero;

            // === ACCOUNT section ===
            // Section label
            var accountSectionLabel = CreateSettingsSectionLabel(panelGo.transform, "ACCOUNT", 0.88f);

            // Display name input
            CreateSettingsTextLabel(panelGo.transform, "Display Name", 0.82f);
            var displayNameInput = CreateSettingsInputField(panelGo.transform, "DisplayNameInput",
                "Enter display name", 0.77f);

            // Save display name button
            var saveNameBtn = CreateSettingsButton(panelGo.transform, "SaveNameButton",
                "Save Display Name", 0.70f, 400, 60, Phase2UiPalette.AccentCyan, Phase2UiPalette.TextOnAccent);

            // Account status (shows sign-in state + email link hint)
            var accountStatusGo = new GameObject("AccountStatus");
            accountStatusGo.transform.SetParent(panelGo.transform, false);
            var accountStatus = accountStatusGo.AddComponent<TextMeshProUGUI>();
            accountStatus.text = "";
            accountStatus.fontSize = 22;
            accountStatus.alignment = TextAlignmentOptions.Center;
            accountStatus.color = Phase2UiPalette.TextDim;
            accountStatus.enableWordWrapping = true;
            accountStatus.raycastTarget = false;
            var accountStatusR = accountStatusGo.GetComponent<RectTransform>();
            accountStatusR.anchorMin = new Vector2(0.1f, 0.58f);
            accountStatusR.anchorMax = new Vector2(0.9f, 0.58f);
            accountStatusR.pivot = new Vector2(0.5f, 0.5f);
            accountStatusR.sizeDelta = new Vector2(0, 70);

            // Email input
            CreateSettingsTextLabel(panelGo.transform, "Link Email (optional — recovers progress on new device)", 0.50f);
            var emailInput = CreateSettingsInputField(panelGo.transform, "EmailInput",
                "email@example.com", 0.45f, isEmail: true);

            // Password input
            CreateSettingsTextLabel(panelGo.transform, "Password (min 6 chars)", 0.38f);
            var passwordInput = CreateSettingsInputField(panelGo.transform, "PasswordInput",
                "••••••", 0.33f, isPassword: true);

            // Link account button
            var linkBtn = CreateSettingsButton(panelGo.transform, "LinkAccountButton",
                "Link Email", 0.26f, 400, 60, new Color(0.42f, 0.58f, 1f, 0.9f), Color.white);

            // === AUDIO section ===
            CreateSettingsSectionLabel(panelGo.transform, "AUDIO", 0.19f);
            var musicSlider = BuildLabeledSlider(panelGo.transform, "MusicVolumeSlider",
                "Music Volume", 0.13f);
            var sfxSlider = BuildLabeledSlider(panelGo.transform, "SfxVolumeSlider",
                "SFX Volume", 0.04f);

            // === IAP section ===
            CreateSettingsSectionLabel(panelGo.transform, "PURCHASES", -0.04f);
            var restoreBtn = CreateSettingsButton(panelGo.transform, "RestorePurchasesButton",
                "Restore Purchases", -0.12f, 500, 70, Phase2UiPalette.AccentCyan, Phase2UiPalette.TextOnAccent);

            // IAP status text
            var statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(panelGo.transform, false);
            var statusText = statusGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "";
            statusText.fontSize = 24;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Phase2UiPalette.TextPrimary;
            statusText.enableWordWrapping = true;
            statusText.raycastTarget = false;
            var statusR = statusGo.GetComponent<RectTransform>();
            statusR.anchorMin = new Vector2(0.1f, -0.21f);
            statusR.anchorMax = new Vector2(0.9f, -0.21f);
            statusR.pivot = new Vector2(0.5f, 0.5f);
            statusR.sizeDelta = new Vector2(0, 50);

            rootGo.SetActive(false);

            return new SettingsDialogRefs
            {
                Root = rootGo,
                CloseButton = closeBtn,
                DisplayNameInput = displayNameInput,
                SaveDisplayNameButton = saveNameBtn,
                AccountStatusLabel = accountStatus,
                EmailInput = emailInput,
                PasswordInput = passwordInput,
                LinkAccountButton = linkBtn,
                MusicSlider = musicSlider,
                SfxSlider = sfxSlider,
                RestoreButton = restoreBtn,
                StatusLabel = statusText
            };
        }

        private struct SettingsDialogRefs
        {
            public GameObject Root;
            public Button CloseButton;
            public TMP_InputField DisplayNameInput;
            public Button SaveDisplayNameButton;
            public TMP_Text AccountStatusLabel;
            public TMP_InputField EmailInput;
            public TMP_InputField PasswordInput;
            public Button LinkAccountButton;
            public Slider MusicSlider;
            public Slider SfxSlider;
            public Button RestoreButton;
            public TMP_Text StatusLabel;
        }

        /// <summary>Creates a section label (e.g. "ACCOUNT", "AUDIO").</summary>
        private static TMP_Text CreateSettingsSectionLabel(Transform parent, string text, float anchorY)
        {
            var go = new GameObject($"SectionLabel_{text}");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 28;
            t.alignment = TextAlignmentOptions.Left;
            t.color = Phase2UiPalette.AccentCyan;
            t.raycastTarget = false;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.1f, anchorY);
            r.anchorMax = new Vector2(0.9f, anchorY);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(0, 40);
            return t;
        }

        /// <summary>Creates a small text label above an input field.</summary>
        private static TMP_Text CreateSettingsTextLabel(Transform parent, string text, float anchorY)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 22;
            t.alignment = TextAlignmentOptions.Left;
            t.color = Phase2UiPalette.TextDim;
            t.raycastTarget = false;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.1f, anchorY);
            r.anchorMax = new Vector2(0.9f, anchorY);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(0, 30);
            return t;
        }

        /// <summary>
        /// Creates a TMP_InputField that ACTUALLY accepts clicks + typing.
        ///
        /// Standard TMP pattern: the input field GameObject has BOTH the TMP_InputField
        /// component AND an Image (the background + raycast target). If the Image is on
        /// a child, raycast hits the child but TMP_InputField's OnPointerDown is on the
        /// parent — so the click never reaches the input field. The Image MUST be on the
        /// same GameObject as TMP_InputField.
        ///
        /// Also: TMP creates its own caret at runtime — we don't need to manually add one.
        /// </summary>
        private static TMP_InputField CreateSettingsInputField(Transform parent, string name,
            string placeholder, float anchorY, bool isPassword = false, bool isEmail = false)
        {
            // --- Input field GameObject (has TMP_InputField + Image for the background) ---
            // The Image is on the SAME GameObject so raycast hits route to TMP_InputField's
            // OnPointerDown handler.
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, anchorY);
            rect.anchorMax = new Vector2(0.85f, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0, 55);

            // Background Image — ON THE INPUT FIELD GAMEOBJECT (not a child!)
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.07f, 1f);
            bgImg.raycastTarget = true;  // CRITICAL — input field needs this to receive clicks

            // Text area (child — just a RectTransform for text viewport clipping)
            var textAreaGo = new GameObject("TextArea");
            textAreaGo.transform.SetParent(go.transform, false);
            var textAreaRect = textAreaGo.AddComponent<RectTransform>();  // AddComponent, not GetComponent — fresh GameObjects don't have one
            textAreaRect.anchorMin = Vector2.zero; textAreaRect.anchorMax = Vector2.one;
            textAreaRect.pivot = new Vector2(0.5f, 0.5f);
            textAreaRect.offsetMin = new Vector2(10, 5); textAreaRect.offsetMax = new Vector2(-10, -5);

            // User-typed text (child of TextArea)
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textAreaGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Phase2UiPalette.TextPrimary;
            text.raycastTarget = false;
            text.richText = false;
            var textR = textGo.GetComponent<RectTransform>();
            textR.anchorMin = Vector2.zero; textR.anchorMax = Vector2.one;
            textR.pivot = new Vector2(0.5f, 0.5f);
            textR.offsetMin = Vector2.zero; textR.offsetMax = Vector2.zero;

            // Placeholder (child of TextArea — shown when input is empty)
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textAreaGo.transform, false);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 28;
            ph.alignment = TextAlignmentOptions.Left;
            ph.color = Phase2UiPalette.TextDim;
            ph.fontStyle = FontStyles.Italic;
            ph.raycastTarget = false;
            var phR = phGo.GetComponent<RectTransform>();
            phR.anchorMin = Vector2.zero; phR.anchorMax = Vector2.one;
            phR.pivot = new Vector2(0.5f, 0.5f);
            phR.offsetMin = Vector2.zero; phR.offsetMax = Vector2.zero;

            // NO manual caret — TMP creates its own at runtime when the input is selected.

            // InputField component itself (on the same GameObject as the Image — required for clicks)
            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = ph;
            input.caretColor = Phase2UiPalette.AccentCyan;
            input.selectionColor = new Color(0.247f, 0.878f, 0.760f, 0.4f);
            input.characterLimit = isPassword ? 64 : 24;
            // Pick the ContentType based on flags. ContentType drives both validation
            // AND the on-screen keyboard type on mobile (TMP picks the right keyboard).
            input.contentType = isPassword
                ? TMP_InputField.ContentType.Password
                : isEmail
                    ? TMP_InputField.ContentType.EmailAddress
                    : TMP_InputField.ContentType.Standard;

            return input;
        }

        /// <summary>Creates a button with a centered label.</summary>
        private static Button CreateSettingsButton(Transform parent, string name,
            string label, float anchorY, float width, float height,
            Color bg, Color labelColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = bg;
            btn.targetGraphic = img;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelT = labelGo.AddComponent<TextMeshProUGUI>();
            labelT.text = label;
            labelT.fontSize = 28;
            labelT.alignment = TextAlignmentOptions.Center;
            labelT.color = labelColor;
            labelT.raycastTarget = false;
            var labelR = labelGo.GetComponent<RectTransform>();
            labelR.anchorMin = Vector2.zero; labelR.anchorMax = Vector2.one;
            labelR.pivot = new Vector2(0.5f, 0.5f);
            labelR.offsetMin = Vector2.zero; labelR.offsetMax = Vector2.zero;

            return btn;
        }

        /// <summary>
        /// Builds a labeled slider for the settings dialog.
        ///
        /// Slider structure follows Unity's standard pattern but with the Background
        /// Image on the slider's own GameObject (not a child) so the slider receives
        /// OnPointerDown/OnDrag directly. If Background is a separate child, the child's
        /// Image intercepts the raycast and the slider never gets the drag event.
        /// </summary>
        private static Slider BuildLabeledSlider(Transform parent, string sliderName, string labelText, float anchorY)
        {
            // Label
            var labelGo = new GameObject($"{sliderName}_Label");
            labelGo.transform.SetParent(parent, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 32;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Phase2UiPalette.TextPrimary;
            label.raycastTarget = false;
            var labelR = labelGo.GetComponent<RectTransform>();
            labelR.anchorMin = new Vector2(0.1f, anchorY);
            labelR.anchorMax = new Vector2(0.9f, anchorY);
            labelR.pivot = new Vector2(0.5f, 0.5f);
            labelR.sizeDelta = new Vector2(0, 50);

            // Slider GameObject (has Slider component + Background Image on same GameObject
            // so raycasts route to the Slider's OnPointerDown handler)
            var sliderGo = new GameObject(sliderName);
            sliderGo.transform.SetParent(parent, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.1f, anchorY - 0.07f);
            sliderRect.anchorMax = new Vector2(0.9f, anchorY - 0.07f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(0, 50);

            // Background Image — ON THE SLIDER GAMEOBJECT (not a child!)
            var bgImg = sliderGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.07f, 1f);
            bgImg.raycastTarget = true;  // Slider needs this to receive click+drag

            // Fill area (child — just a rect for the fill to stretch inside)
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero; fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(10, 0); fillAreaRect.offsetMax = new Vector2(-10, 0);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = Phase2UiPalette.AccentCyan;
            fillImg.raycastTarget = false;  // fill shouldn't intercept — slider needs the drag
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

            // Handle area + handle (handle IS the drag target — raycastTarget=true)
            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
            handleAreaRect.offsetMin = new Vector2(10, 0); handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Phase2UiPalette.TextPrimary;
            handleImg.raycastTarget = true;  // handle must receive the drag
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(30, 50);

            var slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.6f;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private static void BuildModeSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = Phase2UiPalette.Background;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title (centered, top)
            var titleText = Phase2UiPalette.CreateText("Title", "Select Mode", 56, Phase2UiPalette.AccentCyan,
                new Vector2(0.5f, 0.96f), new Vector2(900, 80));

            // Back button (top-left)
            var backGo = new GameObject("BackButton");
            backGo.transform.SetParent(canvasGo.transform, false);
            var backBtn = backGo.AddComponent<Button>();
            var backImg = backGo.AddComponent<Image>();
            backImg.color = Phase2UiPalette.PanelSurface;
            backBtn.targetGraphic = backImg;
            var backRect = backGo.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(40, -40);
            backRect.sizeDelta = new Vector2(220, 80);

            var blGo = new GameObject("Label");
            blGo.transform.SetParent(backGo.transform, false);
            var blText = blGo.AddComponent<TextMeshProUGUI>();
            blText.text = "< Back";
            blText.fontSize = 32;
            blText.alignment = TextAlignmentOptions.Center;
            blText.color = Phase2UiPalette.TextPrimary;
            blText.raycastTarget = false;
            var blR = blGo.GetComponent<RectTransform>();
            blR.anchorMin = Vector2.zero; blR.anchorMax = Vector2.one;
            blR.pivot = new Vector2(0.5f, 0.5f);
            blR.offsetMin = Vector2.zero; blR.offsetMax = Vector2.zero;

            // Scroll container for the cards (handles landscape overflow on small screens)
            var scrollGo = new GameObject("CardScroll");
            scrollGo.transform.SetParent(canvasGo.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.offsetMin = new Vector2(0, -100);
            scrollRect.offsetMax = new Vector2(0, -140);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24;
            vlg.padding = new RectOffset(40, 40, 40, 40);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            scroll.content = contentRect;
            scroll.viewport = contentRect;  // simplified — no separate viewport mask for prototype

            // Helper to build a category section: label + horizontal container for cards
            void BuildSection(string labelText, float accentHue, out TMP_Text labelOut, out Transform containerOut)
            {
                // Category label
                var catGo = new GameObject($"Category_{labelText}");
                catGo.transform.SetParent(contentGo.transform, false);
                var catText = catGo.AddComponent<TextMeshProUGUI>();
                catText.text = labelText;
                catText.fontSize = 24;
                catText.alignment = TextAlignmentOptions.Left;
                catText.color = Phase2UiPalette.TextDim;
                catText.raycastTarget = false;
                var catLayout = catGo.AddComponent<LayoutElement>();
                catLayout.preferredHeight = 40;
                catLayout.flexibleWidth = 1;

                // Horizontal container for cards
                var rowGo = new GameObject($"Row_{labelText}");
                rowGo.transform.SetParent(contentGo.transform, false);
                var hlg2 = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg2.spacing = 16;
                hlg2.childAlignment = TextAnchor.UpperLeft;
                hlg2.childControlWidth = true;
                hlg2.childControlHeight = false;
                hlg2.childForceExpandWidth = true;
                hlg2.childForceExpandHeight = false;
                var rowLayout = rowGo.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 160;
                rowLayout.flexibleWidth = 1;

                labelOut = catText;
                containerOut = rowGo.transform;
            }

            // SOLO section
            BuildSection("SOLO", 0, out var soloLabel, out var soloContainer);

            // VS CPU section
            BuildSection("VS CPU", 0, out var cpuLabel, out var cpuContainer);

            // PASS & PLAY section
            BuildSection("PASS & PLAY", 0, out var passPlayLabel, out var passPlayContainer);

            // ONLINE section
            BuildSection("ONLINE", 0, out var onlineLabel, out var onlineContainer);

            // GameFlowManager
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // AudioBootstrap
            var audioGo = new GameObject("[AudioBootstrap]");
            audioGo.AddComponent<QuickChecks.Audio.AudioBootstrap>();

            // ModeSelectController — wire all the references
            var ctrlGo = new GameObject("[ModeSelectController]");
            var ctrl = ctrlGo.AddComponent<QuickChecks.UI.ModeSelectController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("soloContainer").objectReferenceValue = soloContainer;
            so.FindProperty("cpuContainer").objectReferenceValue = cpuContainer;
            so.FindProperty("passPlayContainer").objectReferenceValue = passPlayContainer;
            so.FindProperty("onlineContainer").objectReferenceValue = onlineContainer;
            so.FindProperty("soloLabel").objectReferenceValue = soloLabel;
            so.FindProperty("cpuLabel").objectReferenceValue = cpuLabel;
            so.FindProperty("passPlayLabel").objectReferenceValue = passPlayLabel;
            so.FindProperty("onlineLabel").objectReferenceValue = onlineLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "ModeSelect.unity");
            Debug.Log("[GameScenesBuilder] ModeSelect scene built (Phase 2 grouped neon cards).");
        }

        private static void BuildTrackSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = Phase2UiPalette.Background;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title (top center)
            var titleText = Phase2UiPalette.CreateText("Title", "Select Track", 56, Phase2UiPalette.AccentCyan,
                new Vector2(0.5f, 0.96f), new Vector2(900, 80));

            // Mode label (small, below title — shows current mode like "SOLO PRACTICE")
            var modeLabel = Phase2UiPalette.CreateText("ModeLabel", "", 28, Phase2UiPalette.TextDim,
                new Vector2(0.5f, 0.91f), new Vector2(700, 36));

            // Back button (top-left)
            var backGo = new GameObject("BackButton");
            backGo.transform.SetParent(canvasGo.transform, false);
            var backBtn = backGo.AddComponent<Button>();
            var backImg = backGo.AddComponent<Image>();
            backImg.color = Phase2UiPalette.PanelSurface;
            backBtn.targetGraphic = backImg;
            var backRect = backGo.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(40, -40);
            backRect.sizeDelta = new Vector2(220, 80);

            var blGo = new GameObject("Label");
            blGo.transform.SetParent(backGo.transform, false);
            var blText = blGo.AddComponent<TextMeshProUGUI>();
            blText.text = "< Back";
            blText.fontSize = 32;
            blText.alignment = TextAlignmentOptions.Center;
            blText.color = Phase2UiPalette.TextPrimary;
            blText.raycastTarget = false;
            var blR = blGo.GetComponent<RectTransform>();
            blR.anchorMin = Vector2.zero; blR.anchorMax = Vector2.one;
            blR.pivot = new Vector2(0.5f, 0.5f);
            blR.offsetMin = Vector2.zero; blR.offsetMax = Vector2.zero;

            // Scrollable track grid
            var scrollGo = new GameObject("TrackScroll");
            scrollGo.transform.SetParent(canvasGo.transform, false);
            var scrollRectGo = scrollGo.GetComponent<RectTransform>();
            scrollRectGo.anchorMin = new Vector2(0, 0);
            scrollRectGo.anchorMax = new Vector2(1, 1);
            scrollRectGo.pivot = new Vector2(0.5f, 0.5f);
            scrollRectGo.offsetMin = new Vector2(0, 60);
            scrollRectGo.offsetMax = new Vector2(0, -160);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var gridGo = new GameObject("TrackGrid");
            gridGo.transform.SetParent(scrollGo.transform, false);
            var vlg = gridGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(40, 40, 30, 30);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = gridGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var gridRect = gridGo.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0, 1);
            gridRect.anchorMax = new Vector2(1, 1);
            gridRect.pivot = new Vector2(0.5f, 1);
            gridRect.sizeDelta = new Vector2(0, 0);

            scroll.content = gridRect;
            scroll.viewport = gridRect;

            // --- Toast dialog (for locked track messages) ---
            var toastRefs = BuildToastDialog(canvasGo.transform);

            // GameFlowManager
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // AudioBootstrap
            var audioGo = new GameObject("[AudioBootstrap]");
            audioGo.AddComponent<QuickChecks.Audio.AudioBootstrap>();

            // TrackSelectController — wire all the references
            var ctrlGo = new GameObject("[TrackSelectController]");
            var ctrl = ctrlGo.AddComponent<TrackSelectController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("trackGridContainer").objectReferenceValue = gridGo.transform;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("modeLabel").objectReferenceValue = modeLabel;
            so.FindProperty("toastDialog").objectReferenceValue = toastRefs.Root;
            so.FindProperty("toastText").objectReferenceValue = toastRefs.MessageText;
            so.FindProperty("toastOkButton").objectReferenceValue = toastRefs.OkButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "TrackSelect.unity");
            Debug.Log("[GameScenesBuilder] TrackSelect scene built (Phase 2 neon track cards + toast).");
        }

        /// <summary>Builds a small centered modal toast with a message + OK button.</summary>
        private static ToastDialogRefs BuildToastDialog(Transform canvas)
        {
            var rootGo = new GameObject("ToastDialog");
            rootGo.transform.SetParent(canvas, false);

            // Dim background (visual only — raycastTarget=false so clicks reach the OK button)
            var dimImg = rootGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);
            dimImg.raycastTarget = false;
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Panel
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(rootGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = Phase2UiPalette.PanelSurface;
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 360);

            // Toast message
            var msgGo = new GameObject("ToastText");
            msgGo.transform.SetParent(panelGo.transform, false);
            var msgText = msgGo.AddComponent<TextMeshProUGUI>();
            msgText.text = "";
            msgText.fontSize = 36;
            msgText.alignment = TextAlignmentOptions.Center;
            msgText.color = Phase2UiPalette.TextPrimary;
            msgText.enableWordWrapping = true;
            msgText.raycastTarget = false;
            var msgR = msgGo.GetComponent<RectTransform>();
            msgR.anchorMin = new Vector2(0.5f, 0.7f);
            msgR.anchorMax = new Vector2(0.5f, 0.7f);
            msgR.pivot = new Vector2(0.5f, 0.5f);
            msgR.sizeDelta = new Vector2(620, 180);

            // OK button
            var okGo = new GameObject("OkButton");
            okGo.transform.SetParent(panelGo.transform, false);
            var okBtn = okGo.AddComponent<Button>();
            var okImg = okGo.AddComponent<Image>();
            okImg.color = Phase2UiPalette.AccentCyan;
            okBtn.targetGraphic = okImg;
            var okRect = okGo.GetComponent<RectTransform>();
            okRect.anchorMin = new Vector2(0.5f, 0.18f);
            okRect.anchorMax = new Vector2(0.5f, 0.18f);
            okRect.pivot = new Vector2(0.5f, 0.5f);
            okRect.sizeDelta = new Vector2(280, 80);

            var okLabelGo = new GameObject("Label");
            okLabelGo.transform.SetParent(okGo.transform, false);
            var okLabel = okLabelGo.AddComponent<TextMeshProUGUI>();
            okLabel.text = "OK";
            okLabel.fontSize = 36;
            okLabel.alignment = TextAlignmentOptions.Center;
            okLabel.color = Phase2UiPalette.TextOnAccent;
            okLabel.raycastTarget = false;
            var okLabelR = okLabelGo.GetComponent<RectTransform>();
            okLabelR.anchorMin = Vector2.zero; okLabelR.anchorMax = Vector2.one;
            okLabelR.pivot = new Vector2(0.5f, 0.5f);
            okLabelR.offsetMin = Vector2.zero; okLabelR.offsetMax = Vector2.zero;

            rootGo.SetActive(false);
            return new ToastDialogRefs { Root = rootGo, MessageText = msgText, OkButton = okBtn };
        }

        private struct ToastDialogRefs
        {
            public GameObject Root;
            public TMP_Text MessageText;
            public Button OkButton;
        }

        /// <summary>
        /// Builds the leaderboard panel as a hidden overlay on the Results screen.
        /// Activated by LeaderboardButton (Solo mode only).
        /// Layout: full-screen dim → centered panel → title/track/loading/error/your-rank text
        /// → rows container → close button.
        /// </summary>
        private static GameObject BuildLeaderboardPanel(Transform canvas)
        {
            // Root (full-screen dim overlay, hidden by default)
            var rootGo = new GameObject("LeaderboardPanel");
            rootGo.transform.SetParent(canvas, false);
            var dimImg = rootGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            dimImg.raycastTarget = false;  // visual only — clicks reach the panel/buttons
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Centered panel
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(rootGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = Phase2UiPalette.PanelSurface;
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900, 1200);

            // Title
            var titleText = Phase2UiPalette.CreateText("Title", "Leaderboard", 56, Phase2UiPalette.AccentCyan,
                new Vector2(0.5f, 0.95f), new Vector2(800, 80));
            titleText.transform.SetParent(panelGo.transform, false);
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -30);
            titleRect.sizeDelta = new Vector2(800, 80);

            // Track label (small, below title — shows track name + par)
            var trackLabelGo = new GameObject("TrackLabel");
            trackLabelGo.transform.SetParent(panelGo.transform, false);
            var trackLabel = trackLabelGo.AddComponent<TextMeshProUGUI>();
            trackLabel.text = "";
            trackLabel.fontSize = 28;
            trackLabel.alignment = TextAlignmentOptions.Center;
            trackLabel.color = Phase2UiPalette.TextDim;
            trackLabel.raycastTarget = false;
            var trackLabelRect = trackLabelGo.GetComponent<RectTransform>();
            trackLabelRect.anchorMin = new Vector2(0.5f, 1f);
            trackLabelRect.anchorMax = new Vector2(0.5f, 1f);
            trackLabelRect.pivot = new Vector2(0.5f, 1f);
            trackLabelRect.anchoredPosition = new Vector2(0, -120);
            trackLabelRect.sizeDelta = new Vector2(800, 40);

            // Loading label
            var loadingGo = new GameObject("LoadingLabel");
            loadingGo.transform.SetParent(panelGo.transform, false);
            var loadingLabel = loadingGo.AddComponent<TextMeshProUGUI>();
            loadingLabel.text = "Loading leaderboard...";
            loadingLabel.fontSize = 32;
            loadingLabel.alignment = TextAlignmentOptions.Center;
            loadingLabel.color = Phase2UiPalette.TextPrimary;
            loadingLabel.raycastTarget = false;
            var loadingRect = loadingGo.GetComponent<RectTransform>();
            loadingRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingRect.pivot = new Vector2(0.5f, 0.5f);
            loadingRect.anchoredPosition = new Vector2(0, 0);
            loadingRect.sizeDelta = new Vector2(700, 50);

            // Error label
            var errorGo = new GameObject("ErrorLabel");
            errorGo.transform.SetParent(panelGo.transform, false);
            var errorLabel = errorGo.AddComponent<TextMeshProUGUI>();
            errorLabel.text = "";
            errorLabel.fontSize = 28;
            errorLabel.alignment = TextAlignmentOptions.Center;
            errorLabel.color = new Color(1f, 0.42f, 0.42f);
            errorLabel.enableWordWrapping = true;
            errorLabel.raycastTarget = false;
            var errorRect = errorGo.GetComponent<RectTransform>();
            errorRect.anchorMin = new Vector2(0.5f, 0.5f);
            errorRect.anchorMax = new Vector2(0.5f, 0.5f);
            errorRect.pivot = new Vector2(0.5f, 0.5f);
            errorRect.anchoredPosition = new Vector2(0, 0);
            errorRect.sizeDelta = new Vector2(700, 120);

            // Rows container (top-N + your rank below)
            var rowsGo = new GameObject("RowsContainer");
            rowsGo.transform.SetParent(panelGo.transform, false);
            var vlg = rowsGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0, 0.18f);
            rowsRect.anchorMax = new Vector2(1, 0.88f);
            rowsRect.pivot = new Vector2(0.5f, 0.5f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;

            // "Your rank" label (below the rows)
            var yourRankGo = new GameObject("YourRankLabel");
            yourRankGo.transform.SetParent(panelGo.transform, false);
            var yourRankLabel = yourRankGo.AddComponent<TextMeshProUGUI>();
            yourRankLabel.text = "";
            yourRankLabel.fontSize = 28;
            yourRankLabel.alignment = TextAlignmentOptions.Center;
            yourRankLabel.color = Phase2UiPalette.AccentCyan;
            yourRankLabel.raycastTarget = false;
            var yourRankRect = yourRankGo.GetComponent<RectTransform>();
            yourRankRect.anchorMin = new Vector2(0.5f, 0.10f);
            yourRankRect.anchorMax = new Vector2(0.5f, 0.10f);
            yourRankRect.pivot = new Vector2(0.5f, 0.5f);
            yourRankRect.anchoredPosition = new Vector2(0, 0);
            yourRankRect.sizeDelta = new Vector2(800, 40);

            // Close button (top-right)
            var closeGo = new GameObject("CloseButton");
            closeGo.transform.SetParent(panelGo.transform, false);
            var closeBtn = closeGo.AddComponent<Button>();
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            closeBtn.targetGraphic = closeImg;
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-20, -20);
            closeRect.sizeDelta = new Vector2(80, 80);

            var closeLabelGo = new GameObject("Label");
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            var closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
            closeLabel.text = "X";
            closeLabel.fontSize = 48;
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.color = Phase2UiPalette.TextPrimary;
            closeLabel.raycastTarget = false;
            var closeLabelR = closeLabelGo.GetComponent<RectTransform>();
            closeLabelR.anchorMin = Vector2.zero; closeLabelR.anchorMax = Vector2.one;
            closeLabelR.pivot = new Vector2(0.5f, 0.5f);
            closeLabelR.offsetMin = Vector2.zero; closeLabelR.offsetMax = Vector2.zero;

            // Attach LeaderboardPanel component + wire refs
            var panel = rootGo.AddComponent<LeaderboardPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("rowsContainer").objectReferenceValue = rowsGo.transform;
            so.FindProperty("titleLabel").objectReferenceValue = titleText;
            so.FindProperty("trackLabel").objectReferenceValue = trackLabel;
            so.FindProperty("loadingLabel").objectReferenceValue = loadingLabel;
            so.FindProperty("errorLabel").objectReferenceValue = errorLabel;
            so.FindProperty("yourRankLabel").objectReferenceValue = yourRankLabel;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            rootGo.SetActive(false);  // hidden by default
            return rootGo;
        }

        private static void BuildKartSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = Phase2UiPalette.Background;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title (top center)
            var titleText = Phase2UiPalette.CreateText("Title", "Select Kart", 56, Phase2UiPalette.AccentCyan,
                new Vector2(0.5f, 0.96f), new Vector2(900, 80));

            // Track label (small, below title — shows the selected track)
            var trackLabel = Phase2UiPalette.CreateText("TrackLabel", "", 26, Phase2UiPalette.TextDim,
                new Vector2(0.5f, 0.91f), new Vector2(800, 36));

            // Back button (top-left)
            var backGo = new GameObject("BackButton");
            backGo.transform.SetParent(canvasGo.transform, false);
            var backBtn = backGo.AddComponent<Button>();
            var backImg = backGo.AddComponent<Image>();
            backImg.color = Phase2UiPalette.PanelSurface;
            backBtn.targetGraphic = backImg;
            var backRect = backGo.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(40, -40);
            backRect.sizeDelta = new Vector2(220, 80);

            var blGo = new GameObject("Label");
            blGo.transform.SetParent(backGo.transform, false);
            var blText = blGo.AddComponent<TextMeshProUGUI>();
            blText.text = "< Back";
            blText.fontSize = 32;
            blText.alignment = TextAlignmentOptions.Center;
            blText.color = Phase2UiPalette.TextPrimary;
            blText.raycastTarget = false;
            var blR = blGo.GetComponent<RectTransform>();
            blR.anchorMin = Vector2.zero; blR.anchorMax = Vector2.one;
            blR.pivot = new Vector2(0.5f, 0.5f);
            blR.offsetMin = Vector2.zero; blR.offsetMax = Vector2.zero;

            // Scrollable kart grid
            var scrollGo = new GameObject("KartScroll");
            scrollGo.transform.SetParent(canvasGo.transform, false);
            var scrollRectGo = scrollGo.GetComponent<RectTransform>();
            scrollRectGo.anchorMin = new Vector2(0, 0);
            scrollRectGo.anchorMax = new Vector2(1, 1);
            scrollRectGo.pivot = new Vector2(0.5f, 0.5f);
            scrollRectGo.offsetMin = new Vector2(0, 160);  // leaves room for START RACE button
            scrollRectGo.offsetMax = new Vector2(0, -160);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var gridGo = new GameObject("KartGrid");
            gridGo.transform.SetParent(scrollGo.transform, false);
            var vlg = gridGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(40, 40, 30, 30);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = gridGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var gridRect = gridGo.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0, 1);
            gridRect.anchorMax = new Vector2(1, 1);
            gridRect.pivot = new Vector2(0.5f, 1);
            gridRect.sizeDelta = new Vector2(0, 0);

            scroll.content = gridRect;
            scroll.viewport = gridRect;

            // START RACE button (bottom — large CTA, cyan)
            var raceGo = new GameObject("RaceButton");
            raceGo.transform.SetParent(canvasGo.transform, false);
            var raceBtn = raceGo.AddComponent<Button>();
            var raceImg = raceGo.AddComponent<Image>();
            raceImg.color = Phase2UiPalette.AccentCyan;
            raceImg.raycastTarget = true;
            raceBtn.targetGraphic = raceImg;
            var raceRect = raceGo.GetComponent<RectTransform>();
            raceRect.anchorMin = new Vector2(0.5f, 0.04f);
            raceRect.anchorMax = new Vector2(0.5f, 0.04f);
            raceRect.pivot = new Vector2(0.5f, 0.5f);
            raceRect.sizeDelta = new Vector2(700, 100);

            var rlGo = new GameObject("Label");
            rlGo.transform.SetParent(raceGo.transform, false);
            var rlText = rlGo.AddComponent<TextMeshProUGUI>();
            rlText.text = "SELECT A KART";
            rlText.fontSize = 42;
            rlText.alignment = TextAlignmentOptions.Center;
            rlText.color = Phase2UiPalette.TextOnAccent;
            rlText.raycastTarget = false;
            var rlR = rlGo.GetComponent<RectTransform>();
            rlR.anchorMin = Vector2.zero; rlR.anchorMax = Vector2.one;
            rlR.pivot = new Vector2(0.5f, 0.5f);
            rlR.offsetMin = Vector2.zero; rlR.offsetMax = Vector2.zero;

            // Toast dialog
            var toastRefs = BuildToastDialog(canvasGo.transform);

            // GameFlowManager
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // AudioBootstrap
            var audioGo = new GameObject("[AudioBootstrap]");
            audioGo.AddComponent<QuickChecks.Audio.AudioBootstrap>();

            // Try to load the KartLibrary asset from ScriptableObjects/Karts/
            var kartLib = AssetDatabase.LoadAssetAtPath<Racing.KartLibrary>(
                "Assets/_Project/ScriptableObjects/Karts/KartLibrary.asset");

            // KartSelectController — wire all the references
            var ctrlGo = new GameObject("[KartSelectController]");
            var ctrl = ctrlGo.AddComponent<KartSelectController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("kartGridContainer").objectReferenceValue = gridGo.transform;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("raceButton").objectReferenceValue = raceBtn;
            so.FindProperty("raceButtonText").objectReferenceValue = rlText;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("trackLabel").objectReferenceValue = trackLabel;
            so.FindProperty("kartLibrary").objectReferenceValue = kartLib;
            so.FindProperty("toastDialog").objectReferenceValue = toastRefs.Root;
            so.FindProperty("toastText").objectReferenceValue = toastRefs.MessageText;
            so.FindProperty("toastOkButton").objectReferenceValue = toastRefs.OkButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (kartLib == null)
            {
                Debug.LogWarning("[GameScenesBuilder] KartLibrary.asset not found at " +
                                  "Assets/_Project/ScriptableObjects/Karts/KartLibrary.asset — " +
                                  "KartSelectController will fall back to Resources or runtime default.");
            }

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "KartSelect.unity");
            Debug.Log("[GameScenesBuilder] KartSelect scene built (Phase 2 neon kart cards).");
        }

        private static void BuildRaceScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // RaceSceneController builds everything at runtime (camera, track, kart, HUD)
            var ctrlGo = new GameObject("[RaceSceneController]");
            ctrlGo.AddComponent<RaceSceneController>();

            // GameFlowManager (persistent — survives from TrackSelect)
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // AudioBootstrap (creates MusicPlayer + SfxPlayer, loads track.mp3)
            var audioGo = new GameObject("[AudioBootstrap]");
            audioGo.AddComponent<QuickChecks.Audio.AudioBootstrap>();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "Race.unity");
            Debug.Log("[GameScenesBuilder] Race scene built.");
        }

        private static void BuildResultsScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f);  // #0E1414 deep dark teal
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Background panel (full-screen, dark teal)
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.055f, 0.078f, 0.078f);  // #0E1414
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Helper to create text elements
            TMP_Text CreateText(string name, string content, float fontSize, Color color,
                Vector2 anchor, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center)
            {
                var go = new GameObject(name);
                go.transform.SetParent(canvasGo.transform, false);
                var text = go.AddComponent<TextMeshProUGUI>();
                text.text = content;
                text.fontSize = fontSize;
                text.alignment = align;
                text.color = color;
                text.raycastTarget = false;
                text.enableWordWrapping = true;
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                return text;
            }

            var accentCyan = new Color(0.247f, 0.878f, 0.760f);   // #3FE0C2
            var offWhite = new Color(0.969f, 0.969f, 0.949f);    // #F7F7F2
            var dimGray = new Color(0.55f, 0.55f, 0.55f);

            // Mode label (top-left small)
            var modeLabel = CreateText("ModeLabel", "", 28, accentCyan,
                new Vector2(0.05f, 0.96f), new Vector2(500, 36),
                TextAlignmentOptions.Left);

            // Track label (top-right small)
            var trackLabel = CreateText("TrackLabel", "", 26, dimGray,
                new Vector2(0.95f, 0.96f), new Vector2(700, 36),
                TextAlignmentOptions.Right);

            // Title (big, top)
            var titleText = CreateText("Title", "Race Complete!", 84, accentCyan,
                new Vector2(0.5f, 0.88f), new Vector2(1000, 100));

            // Subtitle (under title)
            var subtitleText = CreateText("Subtitle", "", 32, offWhite,
                new Vector2(0.5f, 0.80f), new Vector2(900, 50));

            // Stars (large, prominent — solo/CPU only)
            var starsText = CreateText("Stars", "", 96, accentCyan,
                new Vector2(0.5f, 0.69f), new Vector2(600, 100));

            // Solo stats: swipes / par / time (vertical, centered)
            var swipesValueText = CreateText("SwipesValue", "", 42, offWhite,
                new Vector2(0.5f, 0.56f), new Vector2(800, 50));

            var parValueText = CreateText("ParValue", "", 36, dimGray,
                new Vector2(0.5f, 0.50f), new Vector2(800, 40));

            var timeValueText = CreateText("TimeValue", "", 36, offWhite,
                new Vector2(0.5f, 0.44f), new Vector2(800, 40));

            var bestValueText = CreateText("BestValue", "", 30, accentCyan,
                new Vector2(0.5f, 0.37f), new Vector2(900, 40));

            var newBestText = CreateText("NewBest", "", 42, accentCyan,
                new Vector2(0.5f, 0.30f), new Vector2(900, 60));
            newBestText.gameObject.SetActive(false);

            // --- Comparison panel (Vs CPU / Online) ---
            var cmpGo = new GameObject("ComparisonPanel");
            cmpGo.transform.SetParent(canvasGo.transform, false);
            var cmpRect = cmpGo.GetComponent<RectTransform>();
            cmpRect.anchorMin = new Vector2(0.5f, 0.5f);
            cmpRect.anchorMax = new Vector2(0.5f, 0.5f);
            cmpRect.pivot = new Vector2(0.5f, 0.5f);
            cmpRect.anchoredPosition = new Vector2(0, 0);
            cmpRect.sizeDelta = new Vector2(900, 360);
            cmpGo.SetActive(false);

            // Two-column header row
            TMP_Text CreateCmpText(string name, float x, float y, float w, float h, float fontSize, Color color, TextAlignmentOptions align)
            {
                var go = new GameObject(name);
                go.transform.SetParent(cmpGo.transform, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.fontSize = fontSize;
                t.alignment = align;
                t.color = color;
                t.raycastTarget = false;
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(x, 1f);
                r.anchorMax = new Vector2(x, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0, -y);
                r.sizeDelta = new Vector2(w, h);
                return t;
            }

            // Header row
            var youHeader = CreateCmpText("YouHeader", 0.3f, 0, 360, 50, 32, accentCyan, TextAlignmentOptions.Center);
            youHeader.text = "YOU";
            var oppHeader = CreateCmpText("OppHeader", 0.7f, 0, 360, 50, 32, accentCyan, TextAlignmentOptions.Center);
            oppHeader.text = "OPPONENT";

            // YOU row
            var youNameText = CreateCmpText("YouName", 0.3f, 60, 360, 40, 28, offWhite, TextAlignmentOptions.Center);
            var youSwipesText = CreateCmpText("YouSwipes", 0.3f, 110, 360, 50, 42, offWhite, TextAlignmentOptions.Center);
            var youTimeText = CreateCmpText("YouTime", 0.3f, 170, 360, 40, 28, dimGray, TextAlignmentOptions.Center);

            // OPPONENT row
            var opponentNameText = CreateCmpText("OppName", 0.7f, 60, 360, 40, 28, offWhite, TextAlignmentOptions.Center);
            var opponentSwipesText = CreateCmpText("OppSwipes", 0.7f, 110, 360, 50, 42, offWhite, TextAlignmentOptions.Center);
            var opponentTimeText = CreateCmpText("OppTime", 0.7f, 170, 360, 40, 28, dimGray, TextAlignmentOptions.Center);

            // Comparison summary (under the two columns)
            var comparisonSummaryText = CreateCmpText("ComparisonSummary", 0.5f, 260, 880, 60, 32, offWhite, TextAlignmentOptions.Center);

            // --- Ranking panel (Pass & Play) ---
            var rankGo = new GameObject("RankingPanel");
            rankGo.transform.SetParent(canvasGo.transform, false);
            var rankRect = rankGo.GetComponent<RectTransform>();
            rankRect.anchorMin = new Vector2(0.5f, 0.5f);
            rankRect.anchorMax = new Vector2(0.5f, 0.5f);
            rankRect.pivot = new Vector2(0.5f, 1f);
            rankRect.anchoredPosition = new Vector2(0, -150);
            rankRect.sizeDelta = new Vector2(900, 360);
            rankGo.SetActive(false);

            // Ranking rows container (rows are dynamically added at runtime)
            var rowsGo = new GameObject("RankingRowsContainer");
            rowsGo.transform.SetParent(rankGo.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0, 0);
            rowsRect.anchorMax = new Vector2(1, 1);
            rowsRect.pivot = new Vector2(0.5f, 1f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            // VerticalLayoutGroup so rows stack neatly
            var vlg = rowsGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Retry / Menu buttons (bottom)
            var retryGo = new GameObject("RetryButton");
            retryGo.transform.SetParent(canvasGo.transform, false);
            var retryBtn = retryGo.AddComponent<Button>();
            var retryImg = retryGo.AddComponent<Image>();
            retryImg.color = new Color(0.247f, 0.878f, 0.760f, 0.9f);  // cyan
            retryImg.raycastTarget = true;
            retryBtn.targetGraphic = retryImg;
            var retryRect = retryGo.GetComponent<RectTransform>();
            retryRect.anchorMin = new Vector2(0.3f, 0.08f);
            retryRect.anchorMax = new Vector2(0.3f, 0.08f);
            retryRect.pivot = new Vector2(0.5f, 0.5f);
            retryRect.sizeDelta = new Vector2(300, 100);

            var rlGo = new GameObject("Label");
            rlGo.transform.SetParent(retryGo.transform, false);
            var rlText = rlGo.AddComponent<TextMeshProUGUI>();
            rlText.text = "Retry";
            rlText.fontSize = 42;
            rlText.alignment = TextAlignmentOptions.Center;
            rlText.color = new Color(0.055f, 0.078f, 0.078f);
            rlText.raycastTarget = false;
            var rlR = rlGo.GetComponent<RectTransform>();
            rlR.anchorMin = Vector2.zero; rlR.anchorMax = Vector2.one;
            rlR.pivot = new Vector2(0.5f, 0.5f);
            rlR.offsetMin = Vector2.zero; rlR.offsetMax = Vector2.zero;

            var menuGo = new GameObject("MenuButton");
            menuGo.transform.SetParent(canvasGo.transform, false);
            var menuBtn = menuGo.AddComponent<Button>();
            var menuImg = menuGo.AddComponent<Image>();
            menuImg.color = new Color(0.15f, 0.2f, 0.2f, 0.9f);
            menuImg.raycastTarget = true;
            menuBtn.targetGraphic = menuImg;
            var menuRect = menuGo.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.7f, 0.08f);
            menuRect.anchorMax = new Vector2(0.7f, 0.08f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(300, 100);

            var mlGo = new GameObject("Label");
            mlGo.transform.SetParent(menuGo.transform, false);
            var mlText = mlGo.AddComponent<TextMeshProUGUI>();
            mlText.text = "Track Select";
            mlText.fontSize = 38;
            mlText.alignment = TextAlignmentOptions.Center;
            mlText.color = Color.white;
            mlText.raycastTarget = false;
            var mlR = mlGo.GetComponent<RectTransform>();
            mlR.anchorMin = Vector2.zero; mlR.anchorMax = Vector2.one;
            mlR.pivot = new Vector2(0.5f, 0.5f);
            mlR.offsetMin = Vector2.zero; mlR.offsetMax = Vector2.zero;

            // Leaderboard button (Solo only — hidden by default, shown via ResultsScreenController)
            var lbGo = new GameObject("LeaderboardButton");
            lbGo.transform.SetParent(canvasGo.transform, false);
            var lbBtn = lbGo.AddComponent<Button>();
            var lbImg = lbGo.AddComponent<Image>();
            lbImg.color = new Color(0.42f, 0.58f, 1f, 0.9f);  // blue accent for leaderboard
            lbImg.raycastTarget = true;
            lbBtn.targetGraphic = lbImg;
            var lbRect = lbGo.GetComponent<RectTransform>();
            lbRect.anchorMin = new Vector2(0.5f, 0.08f);
            lbRect.anchorMax = new Vector2(0.5f, 0.08f);
            lbRect.pivot = new Vector2(0.5f, 0.5f);
            lbRect.sizeDelta = new Vector2(300, 100);
            lbGo.SetActive(false);  // hidden by default — ResultsScreenController shows it in Solo

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(lbGo.transform, false);
            var lblText = lblGo.AddComponent<TextMeshProUGUI>();
            lblText.text = "Leaderboard";
            lblText.fontSize = 32;
            lblText.alignment = TextAlignmentOptions.Center;
            lblText.color = Color.white;
            lblText.raycastTarget = false;
            var lblR = lblGo.GetComponent<RectTransform>();
            lblR.anchorMin = Vector2.zero; lblR.anchorMax = Vector2.one;
            lblR.pivot = new Vector2(0.5f, 0.5f);
            lblR.offsetMin = Vector2.zero; lblR.offsetMax = Vector2.zero;

            // --- Leaderboard panel (hidden by default, shown via LeaderboardButton) ---
            var leaderboardPanel = BuildLeaderboardPanel(canvasGo.transform);

            // GameFlowManager
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // AudioBootstrap (creates MusicPlayer + SfxPlayer, loads track.mp3)
            var audioGo = new GameObject("[AudioBootstrap]");
            audioGo.AddComponent<QuickChecks.Audio.AudioBootstrap>();

            // ResultsScreenController — wire ALL the references via SerializedObject
            var ctrlGo = new GameObject("[ResultsScreenController]");
            var ctrl = ctrlGo.AddComponent<ResultsScreenController>();
            var so = new SerializedObject(ctrl);

            // Common refs
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("subtitleText").objectReferenceValue = subtitleText;
            so.FindProperty("modeLabelText").objectReferenceValue = modeLabel;
            so.FindProperty("trackLabelText").objectReferenceValue = trackLabel;

            // Solo / CPU refs
            so.FindProperty("starsText").objectReferenceValue = starsText;
            so.FindProperty("swipesValueText").objectReferenceValue = swipesValueText;
            so.FindProperty("parValueText").objectReferenceValue = parValueText;
            so.FindProperty("timeValueText").objectReferenceValue = timeValueText;
            so.FindProperty("bestValueText").objectReferenceValue = bestValueText;
            so.FindProperty("newBestText").objectReferenceValue = newBestText;

            // Comparison panel refs
            so.FindProperty("comparisonPanel").objectReferenceValue = cmpGo;
            so.FindProperty("youNameText").objectReferenceValue = youNameText;
            so.FindProperty("youSwipesText").objectReferenceValue = youSwipesText;
            so.FindProperty("youTimeText").objectReferenceValue = youTimeText;
            so.FindProperty("opponentNameText").objectReferenceValue = opponentNameText;
            so.FindProperty("opponentSwipesText").objectReferenceValue = opponentSwipesText;
            so.FindProperty("opponentTimeText").objectReferenceValue = opponentTimeText;
            so.FindProperty("comparisonSummaryText").objectReferenceValue = comparisonSummaryText;

            // Ranking panel refs
            so.FindProperty("rankingPanel").objectReferenceValue = rankGo;
            so.FindProperty("rankingRowsContainer").objectReferenceValue = rowsGo.transform;
            // rankingRowPrefab is intentionally left null — runtime builds rows when prefab is missing.

            // Buttons
            so.FindProperty("retryButton").objectReferenceValue = retryBtn;
            so.FindProperty("menuButton").objectReferenceValue = menuBtn;
            so.FindProperty("retryButtonText").objectReferenceValue = rlText;
            so.FindProperty("leaderboardButton").objectReferenceValue = lbBtn;
            so.FindProperty("leaderboardPanel").objectReferenceValue = leaderboardPanel.GetComponent<LeaderboardPanel>();

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "Results.unity");
            Debug.Log("[GameScenesBuilder] Results scene built (Phase 2 mode-aware layout).");
        }

        private static void AddScenesToBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(SCENES_DIR + "MainMenu.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "ModeSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "TrackSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "KartSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "Race.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "Results.unity", true),
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[GameScenesBuilder] Scenes added to Build Settings (6 scenes).");
        }
    }
}
#endif
