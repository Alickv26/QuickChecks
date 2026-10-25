using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace QuickChecks.UI
{
    /// <summary>
    /// Shared Phase 2 UI palette + helpers. One source of truth for the neon synthwave look
    /// specified in ART_BIBLE.md so every menu scene renders identically.
    ///
    /// Palette (Art Bible):
    ///   Background      #0E1414   Deep dark teal          (0.055, 0.078, 0.078)
    ///   Track surface   #1B2A2A   Slightly lighter        (0.106, 0.165, 0.165)
    ///   Track border    #3FE0C2   Cyan accent (signature) (0.247, 0.878, 0.760)
    ///   Kart player     #FFD166   Warm yellow              (1.000, 0.820, 0.400)
    ///   Power-up boost  #FF6B6B   Coral red
    ///   Power-up shield #4ECDC4   Mint
    ///   Power-up magnet #C7A3FF   Lavender
    ///   UI text         #F7F7F2   Off-white                (0.969, 0.969, 0.949)
    ///   UI background   #161C1C   Slightly lighter than bg (0.086, 0.110, 0.110)
    ///   UI accent       #3FE0C2   Cyan (same as track border)
    /// </summary>
    public static class Phase2UiPalette
    {
        // Core palette
        public static readonly Color Background       = new(0.055f, 0.078f, 0.078f);
        public static readonly Color PanelSurface    = new(0.086f, 0.110f, 0.110f);
        public static readonly Color CardSurface     = new(0.106f, 0.165f, 0.165f);
        public static readonly Color CardLocked      = new(0.050f, 0.080f, 0.080f);

        public static readonly Color AccentCyan      = new(0.247f, 0.878f, 0.760f);
        public static readonly Color TextPrimary     = new(0.969f, 0.969f, 0.949f);
        public static readonly Color TextDim         = new(0.55f, 0.55f, 0.55f);
        public static readonly Color TextOnAccent    = new(0.055f, 0.078f, 0.078f);

        // Per-mode accent colors (used in ModeSelect + Results comparison)
        public static readonly Color Solo            = new(0.247f, 0.878f, 0.760f);   // cyan
        public static readonly Color CpuEasy         = new(0.42f, 0.79f, 0.47f);       // green
        public static readonly Color CpuMedium       = new(1.0f, 0.82f, 0.40f);         // yellow
        public static readonly Color CpuHard         = new(1.0f, 0.42f, 0.42f);         // red
        public static readonly Color PassPlay        = new(0.42f, 0.58f, 1.0f);        // blue
        public static readonly Color Online          = new(0.725f, 0.514f, 1.0f);      // purple

        // Difficulty stars
        public static readonly Color StarsFilled     = new(1.0f, 0.82f, 0.40f);       // yellow

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Gets the per-mode accent color.</summary>
        public static Color ModeAccent(Core.RaceMode mode)
        {
            return mode switch
            {
                Core.RaceMode.Solo          => Solo,
                Core.RaceMode.VsCpuEasy      => CpuEasy,
                Core.RaceMode.VsCpuMedium    => CpuMedium,
                Core.RaceMode.VsCpuHard      => CpuHard,
                Core.RaceMode.PassPlay2      => PassPlay,
                Core.RaceMode.PassPlay3      => PassPlay,
                Core.RaceMode.PassPlay4      => PassPlay,
                Core.RaceMode.Online         => Online,
                _ => AccentCyan
            };
        }

        /// <summary>One-line description for a mode (used on Mode Select cards).</summary>
        public static string ModeDescription(Core.RaceMode mode)
        {
            return mode switch
            {
                Core.RaceMode.Solo          => "Race alone + your personal best ghost",
                Core.RaceMode.VsCpuEasy      => "Relaxed CPU — generous aim, low pressure",
                Core.RaceMode.VsCpuMedium    => "Sharp CPU — close to a real player",
                Core.RaceMode.VsCpuHard      => "Ruthless CPU — near-perfect aim",
                Core.RaceMode.PassPlay2      => "2 players, one device, take turns",
                Core.RaceMode.PassPlay3      => "3 players, one device, take turns",
                Core.RaceMode.PassPlay4      => "4 players, one device, take turns",
                Core.RaceMode.Online         => "Live opponent (CPU stub in v1 — real netcode in v1.1)",
                _ => ""
            };
        }

        /// <summary>Difficulty label for a track (e.g. "EASY", "MEDIUM", "EXPERT+").</summary>
        public static string DifficultyLabel(int difficultyStars)
        {
            return difficultyStars switch
            {
                1 => "EASY",
                2 => "MEDIUM",
                3 => "HARD",
                4 => "EXPERT",
                5 => "EXPERT+",
                _ => ""
            };
        }

        /// <summary>Filled / empty stars string for a difficulty rating.</summary>
        public static string StarsDisplay(int filled, int total = 3)
        {
            int f = Mathf.Clamp(filled, 0, total);
            return new string('\u2605', f) + new string('\u2606', total - f);
        }

        // ── Factory helpers (called by GameScenesBuilder at editor time) ────────

        /// <summary>Creates a GameObject with a TextMeshProUGUI anchored to a normalised screen position.</summary>
        public static TMP_Text CreateText(string name, string content, float fontSize, Color color,
            Vector2 anchor, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center,
            bool raycastTarget = false, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = raycastTarget;
            text.enableWordWrapping = true;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return text;
        }

        /// <summary>Creates a coloured Image anchored to a normalised position.</summary>
        public static Image CreateImage(string name, Color color, Vector2 anchor, Vector2 size,
            bool raycast = true, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return img;
        }

        /// <summary>
        /// Builds a square white Texture2D sprite lazily cached across calls.
        /// Used by anything that needs a 1x1 white Image (backgrounds, button faces).
        /// </summary>
        private static Sprite _cachedWhiteSprite;
        public static Sprite WhiteSprite
        {
            get
            {
                if (_cachedWhiteSprite != null) return _cachedWhiteSprite;
                const int size = 16;
                var tex = new Texture2D(size, size);
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                _cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return _cachedWhiteSprite;
            }
        }
    }
}
