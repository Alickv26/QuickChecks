using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Lightweight neon hover/tap animation for menu buttons.
    ///
    /// Behaviour:
    ///   - On pointer enter: scales the button up to 1.04× over 0.12s (ease-out) and brightens
    ///     the button face by blending toward white 12%.
    ///   - On pointer exit: scales back to 1.0× over 0.10s, restores colour.
    ///   - On pointer down: scales down to 0.97× instantly, plays SfxPlayer.PlayMenuClick().
    ///   - On pointer up: scales back to 1.04× (hover) or 1.0× (no hover).
    ///
    /// Auto-discovers the target Image component on Awake. If no Image is found, falls back to
    /// scaling only (no colour blend).
    ///
    /// Why a separate component:
    ///   - Keeps MainMenuController / ModeSelectController / TrackSelectController focused on flow.
    ///   - One animation rule for every button — consistent feel across scenes.
    ///   - Plays nicely with Button.onClick (we don't intercept the click, just animate + SFX).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Target transform to scale (defaults to this GameObject's transform).")]
        [SerializeField] private Transform targetTransform;

        [Tooltip("Scale applied on hover.")]
        [SerializeField] private float hoverScale = 1.04f;

        [Tooltip("Scale applied on press (pointer down).")]
        [SerializeField] private float pressScale = 0.97f;

        [Tooltip("Hover animation duration (seconds).")]
        [SerializeField] private float hoverDuration = 0.12f;

        [Tooltip("Press animation duration (seconds).")]
        [SerializeField] private float pressDuration = 0.06f;

        [Tooltip("Colour blend toward white on hover (0 = no blend, 1 = pure white).")]
        [SerializeField, Range(0f, 1f)] private float hoverBrighten = 0.12f;

        [Tooltip("Play SfxPlayer.PlayMenuClick on press?")]
        [SerializeField] private bool playClickSfx = true;

        private Button _button;
        private Image _image;
        private Color _baseColor;
        private Coroutine _scaleRoutine;
        private bool _isPointerInside;
        private bool _isPressed;

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
            if (!_isPressed) AnimateScaleTo(hoverScale, hoverDuration);
            if (_image != null) _image.color = Color.Lerp(_baseColor, Color.white, hoverBrighten);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            if (!_isPressed) AnimateScaleTo(1f, hoverDuration);
            if (_image != null) _image.color = _baseColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            _isPressed = true;
            AnimateScaleTo(pressScale, pressDuration);
            if (playClickSfx && Audio.SfxPlayer.Instance != null)
            {
                Audio.SfxPlayer.Instance.PlayMenuClick();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            // Restore scale (hover or default).
            AnimateScaleTo(_isPointerInside ? hoverScale : 1f, pressDuration);
            if (_isPointerInside && _image != null)
            {
                _image.color = Color.Lerp(_baseColor, Color.white, hoverBrighten);
            }
            else if (_image != null)
            {
                _image.color = _baseColor;
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
