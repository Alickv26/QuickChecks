using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Android haptics wrapper. Uses Unity's built-in Handheld.Vibrate() which works
    /// on Android without any JNI plumbing. No-op on non-Android platforms.
    ///
    /// Note: Handheld.Vibrate() fires the device's default vibration (~1s). We can't
    /// control duration on all devices via this API — for finer control we'd need
    /// the Android Vibration API via JNI, but that requires the Android JNI package
    /// to be enabled (Package Manager → Built-in → Android JNI). For the beta,
    /// Handheld.Vibrate() is good enough — the subtle 15ms-100ms differences aren't
    /// perceptible enough to justify the JNI complexity.
    ///
    /// All methods are static + safe to call on any platform (they no-op on non-Android).
    /// Respects the player's haptics preference (stored in PlayerPrefs as 'QC_HapticsEnabled').
    /// </summary>
    public static class HapticsProvider
    {
        private const string PREFS_KEY = "QC_HapticsEnabled";
        private static bool? _cachedEnabled;

        /// <summary>True if haptics are enabled + we're on a platform that supports vibration.</summary>
        public static bool IsAvailable
        {
            get
            {
                if (!Enabled) return false;
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Player preference — haptics on/off. Defaults to true.</summary>
        public static bool Enabled
        {
            get
            {
                if (_cachedEnabled.HasValue) return _cachedEnabled.Value;
                _cachedEnabled = PlayerPrefs.GetInt(PREFS_KEY, 1) == 1;
                return _cachedEnabled.Value;
            }
            set
            {
                _cachedEnabled = value;
                PlayerPrefs.SetInt(PREFS_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Vibrates the device. Uses Handheld.Vibrate() (Unity built-in, works on Android + iOS).
        /// No-op on desktop or if haptics disabled.
        ///
        /// Note: Handheld.Vibrate() doesn't take a duration parameter on all platforms —
        /// it fires the default system vibration. The `milliseconds` parameter is kept
        /// for API compatibility but is a no-op (we just call Handheld.Vibrate()).
        /// </summary>
        public static void Vibrate(long milliseconds)
        {
            if (!IsAvailable) return;
            try
            {
                Handheld.Vibrate();
            }
            catch (System.Exception ex)
            {
                // Haptics are best-effort — never crash the game over a vibration call
                Debug.LogWarning($"[HapticsProvider] Vibrate({milliseconds}) failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Convenience presets for common juice events.
        /// All presets just call Handheld.Vibrate() — the device's default vibration
        /// is short enough for "subtle" feel. If we want per-event duration control
        /// later, we'll switch to the Android Vibration API via JNI.
        /// </summary>
        public static void Swipe()      => Vibrate(15);
        public static void Pickup()     => Vibrate(30);
        public static void Boundary()   => Vibrate(50);
        public static void BlackHole()  => Vibrate(80);
        public static void Finish()    => Vibrate(100);
    }
}
