using System;
using QuickChecks.Racing;
using QuickChecks.Ghost;

namespace QuickChecks.Core
{
    /// <summary>
    /// Common interface for race controllers (PrototypeBootstrapper + RaceSceneController).
    /// Lets PrototypeHUD reference either one without knowing the concrete type.
    /// </summary>
    public interface IRaceStarter
    {
        int SwipeCount { get; }
        KartController Kart { get; }
        int ParSwipes { get; }
        SoloGhostPlayer SoloGhost { get; }
        bool IsFinished { get; }

        event Action<int, long> OnRaceFinished;
        event Action<int, long, bool> OnGhostUpdated;

        void ResetRace();
    }
}
