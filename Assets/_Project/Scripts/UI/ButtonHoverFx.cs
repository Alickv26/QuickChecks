using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Lightweight neon hover/click animation for menu buttons.
    ///
    /// IMPORTANT — Unity's pointer event routing:
    ///   Unity's EventSystem only calls ONE IPointerDownHandler / IPointerUpHandler per
    ///   GameObject. If this component implemented them, Unity would call THIS component
    ///   instead of UnityEngine.UI.Button, breaking Button's pressed-state tracking and
    ///   preventing Button.onClick from ever firing. (That's why the Settings close
    ///   button + Save + Link Email buttons didn't respond to clicks.)
    ///
    ///   Fix: only use IPointerEnterHandler + IPointerExitHandler + IPointerClickHandler.
    ///   - Enter/Exit drive the hover animation (no conflict with Button).
    ///   - IPointerClickHandler is called IN ADDITION to Button's internal click handler
    ///     (Unity calls all matching IPointerClickHandlers on the same GameObject).
    ///     So the click SFX fires AND Button.onClick still fires.
    ///
    /// Behaviour:
    ///   - On pointer enter: scales the button up to 1.04× over 0.12s (ease-out) and
    ///     brightens the button face by blending toward white 12%.
    ///   - On pointer exit: scales back to 1.0× over 0.10s, restores colour.
    ///   - On pointer click: plays SfxPlayer.PlayMenuClick(). Button.onClick fires
    ///     separately via its own internal handler.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonHoverFx : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("Target transform to scale (defaults to this GameObject's transform).")]
        [SerializeField] private Transform targetTransform;

        [Tooltip("Scale applied on hover.")]
        [SerializeField] private float hoverScale = 1.04f;

        [Tooltip("Hover animation duration (seconds).")]
        [SerializeField] private float hoverDuration = 0.12f;

        [Tooltip("Colour blend toward white on hover (0 = no blend, 1 = pure white).")]
        [SerializeField, Range(0f, 1f)] private float hoverBrighten = 0.12f;

        [Tooltip("Play SfxPlayer.PlayMenuClick on click?")]
        [SerializeField] private bool playClickSfx = true;

        private Button _button;
        private Image _image;
        private Color _baseColor;
        private Coroutine _scaleRoutine;
        private bool _isPointerInside;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
            if (_image != null) _baseColor = _image.color;
            if (targetTransform == null) targetTransform = transform;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            _isPointerInside = true;
            AnimateScaleTo(hoverScale, hoverDuration);
            if (_image != null) _image.color = Color.Lerp(_baseColor, Color.white, hoverBrighten);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            AnimateScaleTo(1f, hoverDuration);
            if (_image != null) _image.color = _baseColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Unity calls ALL matching IPointerClickHandlers on the same GameObject —
            // Button's internal handler fires its own onClick, and ours plays the SFX.
            // We do NOT consume eventData (no ExecuteEvents.BeginUsed), so the chain
            // continues and Button still receives its click.
            if (playClickSfx && Audio.SfxPlayer.Instance != null)
            {
                Audio.SfxPlayer.Instance.PlayMenuClick();
            }
        }

        private void AnimateScaleTo(float targetScale, float duration)
        {
            if (targetTransform == null) return;
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(targetTransform.localScale,
                                                       Vector3.one * targetScale,
                                                       duration));
        }

        private IEnumerator ScaleRoutine(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                targetTransform.localScale = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out (cubic): 1 - (1-t)^3
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                targetTransform.localScale = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }
            targetTransform.localScale = to;
        }

        /// <summary>Allows code to update the base color (e.g. when state changes).</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (_image != null && !_isPointerInside) _image.color = color;
        }
    }
}
