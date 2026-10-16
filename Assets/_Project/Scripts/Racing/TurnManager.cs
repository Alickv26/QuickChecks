using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Orchestrates turn-based racing for any mode (CPU, pass-and-play, online).
    /// Tracks whose turn it is, increments swipe counts, detects race end.
    ///
    /// Turn flow:
    ///   1. StartTurn(playerIndex) -> camera snaps to kart, input enabled for active player
    ///   2. Player swipes -> kart moves -> coasts
    ///   3. Kart velocity drops below stopThreshold -> OnKartStopped fires
    ///   4. EndTurn() -> check win conditions -> StartTurn(nextPlayer)
    ///
    /// Race ends when:
    ///   - All karts have crossed finish line, OR
    ///   - All karts hit maxSwipeCap (DNF)
    /// Winner = fewest swipes; tiebreaker = faster finish time.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public enum RaceMode
        {
            Solo,
            VsCpuEasy,
            VsCpuMedium,
            VsCpuHard,
            PassAndPlay,
            Online
        }

        public enum TurnPhase
        {
            WaitingForTurn,
            PlayerInput,
            KartMoving,
            TurnEnded,
            RaceFinished
        }

        [Serializable]
        public class PlayerState
        {
            public string playerName;
            public KartController kart;
            public bool isCPU;
            public bool isFinished;
            public int swipeCount;
            public long finishTimeMs; // 0 if not finished
            public int checkpointIndex; // last checkpoint passed
        }

        [Header("Race Config")]
        [SerializeField] private RaceMode mode = RaceMode.Solo;
        [SerializeField] private int maxSwipeCap = 50; // fallback if track is null; otherwise track.GetMaxSwipeCap()
        [SerializeField] private float onlineTurnTimeoutSec = 30f;

        [Header("References (set at race start)")]
        [SerializeField] private List<PlayerState> players = new();
        [SerializeField] private Track.TrackDefinition trackDefinition;

        // State
        private int _currentPlayerIndex = 0;
        private TurnPhase _phase = TurnPhase.WaitingForTurn;
        private long _raceStartTimeMs;
        private float _turnTimer = 0f;

        // Events
        public event Action<int> OnTurnStart;        // playerIndex
        public event Action<int, int> OnTurnEnd;      // playerIndex, swipeCount
        public event Action<int, long> OnPlayerFinished; // playerIndex, finishTimeMs
        public event Action<int> OnRaceFinished;      // winnerPlayerIndex (-1 = no winner / DNF)

        public RaceMode Mode => mode;
        public TurnPhase Phase => _phase;
        public int CurrentPlayerIndex => _currentPlayerIndex;
        public PlayerState CurrentPlayer => players.Count > 0 ? players[_currentPlayerIndex] : null;
        public IReadOnlyList<PlayerState> Players => players;

        /// <summary>
        /// Replaces the player list WITHOUT starting a race. Used by RaceSceneController.TransitionToResults()
        /// to synthesise a 1-player TurnManager snapshot for Solo mode (which doesn't actually use a
        /// TurnManager during the race) so RaceResultBuilder.Build() can read from a uniform source.
        ///
        /// This does not change _phase, _currentPlayerIndex, or any turn state — it's purely a data hook.
        /// </summary>
        public void SetPlayersForResults(List<PlayerState> snapshot)
        {
            players = snapshot ?? new List<PlayerState>();
        }

        private void Update()
        {
            if (_phase == TurnPhase.PlayerInput && mode == RaceMode.Online)
            {
                _turnTimer -= Time.deltaTime;
                if (_turnTimer <= 0f)
                {
                    // Turn timed out — auto-skip.
                    SkipCurrentTurn();
                }
            }
        }

        public void StartRace(List<PlayerState> racers, Track.TrackDefinition track, RaceMode raceMode)
        {
            players = racers;
            trackDefinition = track;
            mode = raceMode;

            // Decision 1 (DECISIONS.md): if track has a swipe cap, use it (formula or designer-set).
            if (trackDefinition != null)
            {
                maxSwipeCap = trackDefinition.GetMaxSwipeCap();
            }

            _raceStartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _currentPlayerIndex = 0;
            _phase = TurnPhase.WaitingForTurn;
            StartNextTurn();
        }

        private void StartNextTurn()
        {
            // Advance to the NEXT player (start from i=1 to skip current player).
            for (int i = 1; i <= players.Count; i++)
            {
                int idx = (_currentPlayerIndex + i) % players.Count;
                if (!players[idx].isFinished)
                {
                    _currentPlayerIndex = idx;
                    break;
                }
            }

            // Check race end: all finished or all DNF'd at cap.
            if (AllPlayersDone())
            {
                FinishRace();
                return;
            }

            _phase = TurnPhase.PlayerInput;
            _turnTimer = onlineTurnTimeoutSec;

            var player = CurrentPlayer;
            if (player != null)
            {
                OnTurnStart?.Invoke(_currentPlayerIndex);

                // CPU players take their turn via CPUPlayer component.
                // Small delay so camera can pan to CPU kart before it moves.
                if (player.isCPU)
                {
                    var cpu = player.kart.GetComponent<CPUPlayer>();
                    if (cpu != null)
                    {
                        // Delay CPU turn by 1s to let camera pan
                        StartCoroutine(DelayedCpuTurn(cpu, 1f));
                    }
                }
            }
        }

        private System.Collections.IEnumerator DelayedCpuTurn(CPUPlayer cpu, float delaySec)
        {
            yield return new WaitForSeconds(delaySec);
            cpu.TakeTurn(trackDefinition);
        }

        /// <summary>
        /// Called by KartController when the kart's velocity drops below stopThreshold.
        /// </summary>
        public void NotifyKartStopped(int playerIndex)
        {
            if (playerIndex != _currentPlayerIndex) return;
            if (_phase != TurnPhase.PlayerInput && _phase != TurnPhase.KartMoving) return;

            // Check if player has an extra move (Double Move power-up)
            var player = players[playerIndex];
            if (player.kart != null && player.kart.HasExtraMove)
            {
                bool consumed = player.kart.ConsumeExtraMove();
                if (consumed)
                {
                    Debug.Log($"[TurnManager] Player {playerIndex} consumed extra move — turn continues (swipe again).");
                    _phase = TurnPhase.PlayerInput;  // Back to input phase, waiting for next swipe
                    return;
                }
            }

            _phase = TurnPhase.TurnEnded;
            OnTurnEnd?.Invoke(playerIndex, players[playerIndex].swipeCount);
            StartNextTurn();
        }

        /// <summary>
        /// Called when a kart crosses the finish line (after passing all checkpoints in order).
        /// </summary>
        public void NotifyPlayerFinished(int playerIndex)
        {
            var p = players[playerIndex];
            if (p.isFinished) return;

            p.isFinished = true;
            p.finishTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartTimeMs;
            OnPlayerFinished?.Invoke(playerIndex, p.finishTimeMs);

            // In vs CPU mode: race ends immediately when the HUMAN player (index 0) finishes.
            // No need to wait for CPU to also finish — it's a single-player race.
            if (mode != RaceMode.Solo && mode != RaceMode.PassAndPlay && mode != RaceMode.Online)
            {
                if (playerIndex == 0)
                {
                    Debug.Log($"[TurnManager] Player 0 finished — ending vs CPU race immediately.");
                    FinishRace();
                    return;
                }
            }

            // Race continues until ALL players are done (both finished or DNF'd).
            if (AllPlayersDone()) FinishRace();
        }

        /// <summary>
        /// Called by SwipeDetector (via event) when the active player swipes.
        /// Increments swipe count, checks for cap.
        /// </summary>
        public void RegisterSwipe(int playerIndex)
        {
            if (playerIndex != _currentPlayerIndex) return;
            if (_phase != TurnPhase.PlayerInput) return;

            var p = players[playerIndex];
            p.swipeCount++;
            _phase = TurnPhase.KartMoving;

            if (p.swipeCount >= maxSwipeCap && !p.isFinished)
            {
                // DNF — kart can still finish its current move but won't get more turns.
                p.isFinished = true;
                p.finishTimeMs = -1; // DNF marker
                OnPlayerFinished?.Invoke(playerIndex, -1);
            }
        }

        private void SkipCurrentTurn()
        {
            _phase = TurnPhase.TurnEnded;
            OnTurnEnd?.Invoke(_currentPlayerIndex, players[_currentPlayerIndex].swipeCount);
            StartNextTurn();
        }

        private bool AllPlayersDone()
        {
            foreach (var p in players)
            {
                if (!p.isFinished) return false;
            }
            return true;
        }

        private void FinishRace()
        {
            _phase = TurnPhase.RaceFinished;

            // Determine winner: fewest swipes, tiebreaker = fastest time.
            int winner = -1;
            int bestSwipes = int.MaxValue;
            long bestTime = long.MaxValue;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p.isFinished || p.finishTimeMs < 0) continue; // skip DNF
                if (p.swipeCount < bestSwipes ||
                    (p.swipeCount == bestSwipes && p.finishTimeMs < bestTime))
                {
                    bestSwipes = p.swipeCount;
                    bestTime = p.finishTimeMs;
                    winner = i;
                }
            }

            OnRaceFinished?.Invoke(winner);
        }
    }
}
