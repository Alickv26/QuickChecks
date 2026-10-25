using System.Collections;
using UnityEngine;
using TMPro;
using QuickChecks.Audio;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Visual feedback component on each kart. Handles:
    ///   - Power-up tint: kart's SpriteRenderer changes to the power-up's effectColor while active
    ///   - Wall hit flash: red flash on the kart + "BUMP!" popup text
    ///   - Gravel tint: brown tint while in gravel + "SLOWED" popup
    ///   - Black hole flash: dark purple flash + "REWIND" popup
    ///
    /// Attached to the same GameObject as KartController (added by RaceSceneController).
    /// Subscribes to PowerUpSystem.OnEffectStarted/OnEffectExpired events.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class KartVisualFeedback : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Color _baseColor;
        private PowerUpSystem _powerUpSystem;

        // Tint state — power-up tint takes priority, then gravel, then wall flash
        private Color _powerUpTint = Color.white;
        private bool _hasPowerUpTint = false;
        private bool _isInGravel = false;
        private float _flashTimer = 0f;
        private Color _flashColor = Color.white;
        private float _flashDuration = 0.3f;

        // Popup text (created lazily)
        private GameObject _popupTextGo;
        private TextMeshPro _popupText;
        private float _popupTimer = 0f;
        private const float POPUP_DURATION = 1.5f;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseColor = _sr.color;
        }

        private void Start()
        {
            _powerUpSystem = GetComponent<PowerUpSystem>();
            if (_powerUpSystem != null)
            {
                _powerUpSystem.OnEffectStarted += OnPowerUpStarted;
                _powerUpSystem.OnEffectExpired += OnPowerUpExpired;
            }
        }

        private void OnDestroy()
        {
            if (_powerUpSystem != null)
            {
                _powerUpSystem.OnEffectStarted -= OnPowerUpStarted;
                _powerUpSystem.OnEffectExpired -= OnPowerUpExpired;
            }
        }

        private void Update()
        {
            // Compute the effective color: base → gravel tint → power-up tint → flash overlay
            Color targetColor = _baseColor;

            // Gravel tint (multiply toward brown)
            if (_isInGravel)
            {
                targetColor = Color.Lerp(targetColor, new Color(0.6f, 0.5f, 0.3f), 0.4f);
            }

            // Power-up tint (blend toward effectColor)
            if (_hasPowerUpTint)
            {
                targetColor = Color.Lerp(targetColor, _powerUpTint, 0.5f);
            }

            // Flash overlay (quick burst that decays)
            if (_flashTimer > 0f)
            {
                float flashT = _flashTimer / _flashDuration;
                targetColor = Color.Lerp(targetColor, _flashColor, flashT * 0.6f);
                _flashTimer -= Time.deltaTime;
            }

            _sr.color = targetColor;

            // Popup text fade + float up
            if (_popupTextGo != null && _popupTimer > 0f)
            {
                _popupTimer -= Time.deltaTime;
                float t = _popupTimer / POPUP_DURATION;
                _popupTextGo.transform.position += Vector3.up * 0.5f * Time.deltaTime;
                var c = _popupText.color;
                c.a = t;
                _popupText.color = c;
                if (_popupTimer <= 0f)
                {
                    Destroy(_popupTextGo);
                    _popupTextGo = null;
                    _popupText = null;
                }
            }
        }

        // =====================================================================
        // Power-up tint
        // =====================================================================

        private void OnPowerUpStarted(PowerUpBase powerUp)
        {
            if (powerUp == null) return;
            _powerUpTint = powerUp.effectColor;
            _hasPowerUpTint = true;
            Debug.Log($"[KartVisual] Power-up tint: {powerUp.displayName} ({powerUp.effectColor})");
        }

        private void OnPowerUpExpired(PowerUpBase powerUp)
        {
            _hasPowerUpTint = false;
            Debug.Log("[KartVisual] Power-up tint removed");
        }

        // =====================================================================
        // Wall hit feedback
        // =====================================================================

        /// <summary>Called by KartController when it hits a wall. Red flash + "BUMP!" popup.</summary>
        public void OnWallHit()
        {
            Flash(Color.red, 0.3f);
            ShowPopup("BUMP!", Color.red);
        }

        // =====================================================================
        // Gravel feedback
        // =====================================================================

        /// <summary>Called by GravelSpot when kart enters/exists gravel.</summary>
        public void SetGravelActive(bool active)
        {
            _isInGravel = active;
            if (active)
            {
                ShowPopup("SLOWED", new Color(0.6f, 0.5f, 0.3f));
            }
        }

        // =====================================================================
        // Black hole feedback
        // =====================================================================

        /// <summary>Called by BlackHole when it triggers a rewind. Dark purple flash + popup.</summary>
        public void OnBlackHoleRewind(int swipeCount)
        {
            Flash(new Color(0.15f, 0f, 0.2f), 0.5f);
            ShowPopup($"REWIND -{swipeCount}", new Color(0.7f, 0.3f, 0.8f));
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void Flash(Color color, float duration)
        {
            _flashColor = color;
            _flashDuration = duration;
            _flashTimer = duration;
        }

        private void ShowPopup(string text, Color color)
        {
            // Reuse existing popup if one is active
            if (_popupTextGo == null)
            {
                _popupTextGo = new GameObject("PopupText");
                _popupTextGo.transform.position = transform.position + Vector3.up * 2f;
                _popupText = _popupTextGo.AddComponent<TextMeshPro>();
                _popupText.fontSize = 24;
                _popupText.alignment = TextAlignmentOptions.Center;
                _popupText.raycastTarget = false;
                _popupText.sortingOrder = 100;
            }
            else
            {
                _popupTextGo.transform.position = transform.position + Vector3.up * 2f;
            }

            _popupText.text = text;
            _popupText.color = color;
            _popupTimer = POPUP_DURATION;
        }

        /// <summary>Resets all visual state (called on race reset).</summary>
        public void Reset()
        {
            _hasPowerUpTint = false;
            _isInGravel = false;
            _flashTimer = 0f;
            if (_popupTextGo != null)
            {
                Destroy(_popupTextGo);
                _popupTextGo = null;
                _popupText = null;
            }
            _sr.color = _baseColor;
        }
    }
}
