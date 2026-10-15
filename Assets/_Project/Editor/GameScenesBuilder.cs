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
            BuildTrackSelectScene();
            BuildRaceScene();
            BuildResultsScene();

            // Add scenes to Build Settings
            AddScenesToBuildSettings();

            EditorUtility.DisplayDialog(
                "Game Scenes Built",
                "All 4 game scenes created:\n\n" +
                "• MainMenu.unity\n" +
                "• TrackSelect.unity\n" +
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
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "QuickChecks";
            titleText.fontSize = 96;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.247f, 0.878f, 0.760f);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(800, 120);

            // Subtitle
            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(canvasGo.transform, false);
            var subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.text = "Swipe Golf Racing";
            subText.fontSize = 42;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.969f, 0.969f, 0.949f, 0.7f);
            var subRect = subGo.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.6f);
            subRect.anchorMax = new Vector2(0.5f, 0.6f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(600, 60);

            // Play button
            var playGo = new GameObject("PlayButton");
            playGo.transform.SetParent(canvasGo.transform, false);
            var playBtn = playGo.AddComponent<Button>();
            var playImg = playGo.AddComponent<Image>();
            playImg.color = new Color(0.247f, 0.878f, 0.760f, 0.9f);
            playImg.raycastTarget = true;
            playBtn.targetGraphic = playImg;
            var playRect = playGo.GetComponent<RectTransform>();
            playRect.anchorMin = new Vector2(0.5f, 0.4f);
            playRect.anchorMax = new Vector2(0.5f, 0.4f);
            playRect.pivot = new Vector2(0.5f, 0.5f);
            playRect.sizeDelta = new Vector2(400, 120);

            var playLabelGo = new GameObject("Label");
            playLabelGo.transform.SetParent(playGo.transform, false);
            var playLabel = playLabelGo.AddComponent<TextMeshProUGUI>();
            playLabel.text = "PLAY";
            playLabel.fontSize = 56;
            playLabel.alignment = TextAlignmentOptions.Center;
            playLabel.color = new Color(0.055f, 0.078f, 0.078f);
            playLabel.raycastTarget = false;
            var plRect = playLabelGo.GetComponent<RectTransform>();
            plRect.anchorMin = Vector2.zero;
            plRect.anchorMax = Vector2.one;
            plRect.pivot = new Vector2(0.5f, 0.5f);
            plRect.offsetMin = Vector2.zero;
            plRect.offsetMax = Vector2.zero;

            // Settings button
            var settingsGo = new GameObject("SettingsButton");
            settingsGo.transform.SetParent(canvasGo.transform, false);
            var settingsBtn = settingsGo.AddComponent<Button>();
            var settingsImg = settingsGo.AddComponent<Image>();
            settingsImg.color = new Color(0.15f, 0.2f, 0.2f, 0.9f);
            settingsImg.raycastTarget = true;
            settingsBtn.targetGraphic = settingsImg;
            var settingsRect = settingsGo.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.5f, 0.25f);
            settingsRect.anchorMax = new Vector2(0.5f, 0.25f);
            settingsRect.pivot = new Vector2(0.5f, 0.5f);
            settingsRect.sizeDelta = new Vector2(300, 80);

            var sLabelGo = new GameObject("Label");
            sLabelGo.transform.SetParent(settingsGo.transform, false);
            var sLabel = sLabelGo.AddComponent<TextMeshProUGUI>();
            sLabel.text = "Settings";
            sLabel.fontSize = 36;
            sLabel.alignment = TextAlignmentOptions.Center;
            sLabel.color = new Color(0.969f, 0.969f, 0.949f);
            sLabel.raycastTarget = false;
            var slRect = sLabelGo.GetComponent<RectTransform>();
            slRect.anchorMin = Vector2.zero;
            slRect.anchorMax = Vector2.one;
            slRect.pivot = new Vector2(0.5f, 0.5f);
            slRect.offsetMin = Vector2.zero;
            slRect.offsetMax = Vector2.zero;

            // GameFlowManager (persistent singleton)
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // MainMenuController
            var controllerGo = new GameObject("[MainMenuController]");
            var controller = controllerGo.AddComponent<MainMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("subtitleText").objectReferenceValue = subText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "MainMenu.unity");
            Debug.Log("[GameScenesBuilder] MainMenu scene built.");
        }

        private static void BuildTrackSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "Select Track";
            titleText.fontSize = 56;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.247f, 0.878f, 0.760f);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.95f);
            titleRect.anchorMax = new Vector2(0.5f, 0.95f);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(800, 80);

            // Back button
            var backGo = new GameObject("BackButton");
            backGo.transform.SetParent(canvasGo.transform, false);
            var backBtn = backGo.AddComponent<Button>();
            var backImg = backGo.AddComponent<Image>();
            backImg.color = new Color(0.15f, 0.2f, 0.2f, 0.9f);
            backImg.raycastTarget = true;
            backBtn.targetGraphic = backImg;
            var backRect = backGo.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(40, -40);
            backRect.sizeDelta = new Vector2(200, 80);

            var blGo = new GameObject("Label");
            blGo.transform.SetParent(backGo.transform, false);
            var blText = blGo.AddComponent<TextMeshProUGUI>();
            blText.text = "← Back";
            blText.fontSize = 32;
            blText.alignment = TextAlignmentOptions.Center;
            blText.color = Color.white;
            blText.raycastTarget = false;
            var blR = blGo.GetComponent<RectTransform>();
            blR.anchorMin = Vector2.zero; blR.anchorMax = Vector2.one;
            blR.pivot = new Vector2(0.5f, 0.5f);
            blR.offsetMin = Vector2.zero; blR.offsetMax = Vector2.zero;

            // Track grid container (VerticalLayoutGroup for auto-stacking)
            var gridGo = new GameObject("TrackGrid");
            gridGo.transform.SetParent(canvasGo.transform, false);
            var vlg = gridGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.padding = new RectOffset(40, 40, 140, 40);
            vlg.childAlignment = TextAnchor.UpperCenter;
            var gridRect = gridGo.GetComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            // GameFlowManager
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // TrackSelectController
            var ctrlGo = new GameObject("[TrackSelectController]");
            var ctrl = ctrlGo.AddComponent<TrackSelectController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("trackGridContainer").objectReferenceValue = gridGo.transform;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "TrackSelect.unity");
            Debug.Log("[GameScenesBuilder] TrackSelect scene built.");
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

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "Race.unity");
            Debug.Log("[GameScenesBuilder] Race scene built.");
        }

        private static void BuildResultsScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10;
            cam.backgroundColor = new Color(0.055f, 0.078f, 0.078f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // Canvas
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Helper to create text
            TMP_Text CreateText(string name, string content, float fontSize, Color color, Vector2 anchor, Vector2 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(canvasGo.transform, false);
                var text = go.AddComponent<TextMeshProUGUI>();
                text.text = content;
                text.fontSize = fontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = color;
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                return text;
            }

            var titleText = CreateText("Title", "Race Complete!", 64,
                new Color(0.247f, 0.878f, 0.760f), new Vector2(0.5f, 0.8f), new Vector2(800, 80));

            var swipesText = CreateText("Swipes", "", 48,
                Color.white, new Vector2(0.5f, 0.65f), new Vector2(600, 60));

            var timeText = CreateText("Time", "", 42,
                Color.white, new Vector2(0.5f, 0.57f), new Vector2(600, 50));

            var bestText = CreateText("Best", "", 36,
                new Color(0.247f, 0.878f, 0.760f), new Vector2(0.5f, 0.48f), new Vector2(600, 50));

            var newBestText = CreateText("NewBest", "", 42,
                new Color(0.247f, 0.878f, 0.760f), new Vector2(0.5f, 0.38f), new Vector2(600, 80));
            newBestText.gameObject.SetActive(false);

            // Retry button
            var retryGo = new GameObject("RetryButton");
            retryGo.transform.SetParent(canvasGo.transform, false);
            var retryBtn = retryGo.AddComponent<Button>();
            var retryImg = retryGo.AddComponent<Image>();
            retryImg.color = new Color(0.247f, 0.878f, 0.760f, 0.9f);
            retryImg.raycastTarget = true;
            retryBtn.targetGraphic = retryImg;
            var retryRect = retryGo.GetComponent<RectTransform>();
            retryRect.anchorMin = new Vector2(0.3f, 0.15f);
            retryRect.anchorMax = new Vector2(0.3f, 0.15f);
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

            // Menu button
            var menuGo = new GameObject("MenuButton");
            menuGo.transform.SetParent(canvasGo.transform, false);
            var menuBtn = menuGo.AddComponent<Button>();
            var menuImg = menuGo.AddComponent<Image>();
            menuImg.color = new Color(0.15f, 0.2f, 0.2f, 0.9f);
            menuImg.raycastTarget = true;
            menuBtn.targetGraphic = menuImg;
            var menuRect = menuGo.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.7f, 0.15f);
            menuRect.anchorMax = new Vector2(0.7f, 0.15f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(300, 100);

            var mlGo = new GameObject("Label");
            mlGo.transform.SetParent(menuGo.transform, false);
            var mlText = mlGo.AddComponent<TextMeshProUGUI>();
            mlText.text = "Menu";
            mlText.fontSize = 42;
            mlText.alignment = TextAlignmentOptions.Center;
            mlText.color = Color.white;
            mlText.raycastTarget = false;
            var mlR = mlGo.GetComponent<RectTransform>();
            mlR.anchorMin = Vector2.zero; mlR.anchorMax = Vector2.one;
            mlR.pivot = new Vector2(0.5f, 0.5f);
            mlR.offsetMin = Vector2.zero; mlR.offsetMax = Vector2.zero;

            // GameFlowManager
            var flowGo = new GameObject("[GameFlowManager]");
            flowGo.AddComponent<GameFlowManager>();

            // ResultsScreenController
            var ctrlGo = new GameObject("[ResultsScreenController]");
            var ctrl = ctrlGo.AddComponent<ResultsScreenController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("swipesText").objectReferenceValue = swipesText;
            so.FindProperty("timeText").objectReferenceValue = timeText;
            so.FindProperty("bestText").objectReferenceValue = bestText;
            so.FindProperty("newBestText").objectReferenceValue = newBestText;
            so.FindProperty("retryButton").objectReferenceValue = retryBtn;
            so.FindProperty("menuButton").objectReferenceValue = menuBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENES_DIR + "Results.unity");
            Debug.Log("[GameScenesBuilder] Results scene built.");
        }

        private static void AddScenesToBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(SCENES_DIR + "MainMenu.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "TrackSelect.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "Race.unity", true),
                new EditorBuildSettingsScene(SCENES_DIR + "Results.unity", true),
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[GameScenesBuilder] Scenes added to Build Settings.");
        }
    }
}
#endif
