namespace QuickChecks.Core
{
    /// <summary>
    /// Game modes available in QuickChecks.
    /// </summary>
    public enum RaceMode
    {
        Solo,           // Race alone + ghost replay
        VsCpuEasy,      // 1v1 vs CPU (easy difficulty)
        VsCpuMedium,    // 1v1 vs CPU (medium difficulty)
        VsCpuHard,      // 1v1 vs CPU (hard difficulty)
        PassPlay2,      // 2 players, pass and play
        PassPlay3,      // 3 players, pass and play
        PassPlay4,      // 4 players, pass and play
        Online          // Online turn-based (v1 stub — CPU masquerades as online opponent)
    }

    /// <summary>
    /// Extension methods for RaceMode.
    /// </summary>
    public static class RaceModeExtensions
    {
        public static bool IsSolo(this RaceMode mode) => mode == RaceMode.Solo;
        public static bool IsVsCpu(this RaceMode mode) =>
            mode == RaceMode.VsCpuEasy || mode == RaceMode.VsCpuMedium || mode == RaceMode.VsCpuHard;
        public static bool IsPassPlay(this RaceMode mode) =>
            mode == RaceMode.PassPlay2 || mode == RaceMode.PassPlay3 || mode == RaceMode.PassPlay4;
        public static bool IsOnline(this RaceMode mode) => mode == RaceMode.Online;
        public static bool IsMultiplayer(this RaceMode mode) => !mode.IsSolo();

        public static int PlayerCount(this RaceMode mode)
        {
            return mode switch
            {
                RaceMode.Solo => 1,
                RaceMode.VsCpuEasy => 2,
                RaceMode.VsCpuMedium => 2,
                RaceMode.VsCpuHard => 2,
                RaceMode.PassPlay2 => 2,
                RaceMode.PassPlay3 => 3,
                RaceMode.PassPlay4 => 4,
                RaceMode.Online => 2,
                _ => 1
            };
        }

        public static string DisplayName(this RaceMode mode)
        {
            return mode switch
            {
                RaceMode.Solo => "Solo Practice",
                RaceMode.VsCpuEasy => "vs CPU (Easy)",
                RaceMode.VsCpuMedium => "vs CPU (Medium)",
                RaceMode.VsCpuHard => "vs CPU (Hard)",
                RaceMode.PassPlay2 => "Pass & Play (2P)",
                RaceMode.PassPlay3 => "Pass & Play (3P)",
                RaceMode.PassPlay4 => "Pass & Play (4P)",
                RaceMode.Online => "Online Match",
                _ => "Unknown"
            };
        }

        public static string ShortName(this RaceMode mode)
        {
            return mode switch
            {
                RaceMode.Solo => "Solo",
                RaceMode.VsCpuEasy => "CPU Easy",
                RaceMode.VsCpuMedium => "CPU Med",
                RaceMode.VsCpuHard => "CPU Hard",
                RaceMode.PassPlay2 => "2P",
                RaceMode.PassPlay3 => "3P",
                RaceMode.PassPlay4 => "4P",
                RaceMode.Online => "Online",
                _ => "?"
            };
        }
    }
}
