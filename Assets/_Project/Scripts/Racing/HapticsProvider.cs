using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Android haptics wrapper. Uses AndroidJavaClass to call the Vibration API.
    /// No-op on non-Android platforms (iOS impl deferred to v1.1).
    ///
    /// Usage:
    ///   HapticsProvider.Vibrate(20);     // 20ms light tap
    ///   HapticsProvider.Vibrate(50);     // 50ms medium tap
    ///   HapticsProvider.Vibrate(100);    // 100ms heavy hit
    ///
    /// All methods are static + safe to call on any platform (they no-op on non-Android).
    /// Respects the player's haptics preference (stored in PlayerPrefs as 'QC_HapticsEnabled').
    /// </summary>
    public static class HapticsProvider
    {
        private const string PREFS_KEY = "QC_HapticsEnabled";
        private static bool? _cachedEnabled;

        /// <summary>True if haptics are enabled + we're on Android. False on iOS / desktop / disabled.</summary>
        public static bool IsAvailable
        {
            get
            {
                if (!Enabled) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
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
        /// Vibrates the device for `milliseconds`. No-op on non-Android or if haptics disabled.
        /// </summary>
        public static void Vibrate(long milliseconds)
        {
            if (!IsAvailable) return;
            try
            {
                using (var activity = GetActivity())
                {
                    if (activity == null) return;
                    var vibrator = activity.CallAndroidObject<UnityEngine.AndroidJavaObject>("getSystemService", "vibrator");
                    if (vibrator != null)
                    {
                        vibrator.Call("vibrate", milliseconds);
                        vibrator.Dispose();
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Haptics are best-effort — never crash the game over a vibration call
                Debug.LogWarning($"[HapticsProvider] Vibrate({milliseconds}) failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Convenience presets for common juice events.
        /// </summary>
        public static void Swipe()     => Vibrate(15);   // light tap on swipe
        public static void Pickup()    => Vibrate(30);   // medium tap on power-up pickup
        public static void Boundary() => Vibrate(50);   // heavier tap on boundary hit
        public static void BlackHole() => Vibrate(80);  // heaviest — rewind rewind
        public static void Finish()   => Vibrate(100);  // celebration on race complete

#if UNITY_ANDROID && !UNITY_EDITOR
        private static UnityEngine.AndroidJavaObject GetActivity()
        {
            using (var unityPlayer = new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return unityPlayer.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity");
            }
        }
#else
        private static object GetActivity() => null;
#endif
    }
}
