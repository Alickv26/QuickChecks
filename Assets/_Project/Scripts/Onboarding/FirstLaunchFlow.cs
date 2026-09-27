using UnityEngine;
using QuickChecks.Onboarding;
using QuickChecks.Core;
using QuickChecks.Input;

namespace QuickChecks.Onboarding
{
    /// <summary>
    /// Top-level orchestrator for first-launch onboarding.
    /// Checks if tutorial has been completed; if not, runs it.
    ///
    /// Flow:
    ///   1. Awake: check PlayerPrefs 'QuickChecks_TutorialCompleted' flag
    ///   2. If false: create TutorialController GameObject, subscribe to OnTutorialComplete
    ///   3. When tutorial completes: proceed to main menu (in production: load MainMenu scene)
    ///   4. If true: skip directly to main menu
    ///
    /// Decision 7 (DECISIONS.md): forced 30-sec tutorial on first launch.
    /// Decision 11: anonymous auth also runs on first launch (handled by SupabaseClient).
    /// Decision 22: display name generated on first launch (handled by DisplayNameGenerator).
    /// </summary>
    public class FirstLaunchFlow : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputSettings inputSettings;
        [SerializeField] private GameEventSO<SwipeData> swipeEvent;

        [Header("Skip Tutorial (Debug)")]
        [Tooltip("If true, always skip tutorial (for testing main menu during dev).")]
        [SerializeField] private bool skipTutorialInEditor = false;

        private void Start()
        {
            bool tutorialCompleted = TutorialController.IsTutorialCompleted();

#if UNITY_EDITOR
            if (skipTutorialInEditor)
            {
                tutorialCompleted = true;
                Debug.Log("[FirstLaunchFlow] Skipping tutorial (skipTutorialInEditor=true).");
            }
#endif

            if (tutorialCompleted)
            {
                Debug.Log("[FirstLaunchFlow] Tutorial already completed. Proceeding to main menu.");
                ProceedToMainMenu();
            }
            else
            {
                Debug.Log("[FirstLaunchFlow] First launch detected — starting tutorial.");
                StartTutorial();
            }
        }

        private void StartTutorial()
        {
            var go = new GameObject("TutorialController");
            var controller = go.AddComponent<TutorialController>();

            // Wire up references via reflection (same pattern as PrototypeBootstrapper)
            SetPrivateField(controller, "inputSettings", inputSettings);
            SetPrivateField(controller, "swipeEvent", swipeEvent);

            controller.OnTutorialComplete += HandleTutorialComplete;
        }

        private void HandleTutorialComplete()
        {
            Debug.Log("[FirstLaunchFlow] Tutorial completed. Proceeding to main menu.");
            ProceedToMainMenu();
        }

        private void ProceedToMainMenu()
        {
            // In production: SceneManager.LoadScene("MainMenu");
            // For prototype: just log and let the existing scene continue.
            Debug.Log("[FirstLaunchFlow] Would load main menu scene here (production: SceneManager.LoadScene).");
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null || string.IsNullOrEmpty(fieldName)) return;
            var type = obj.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
