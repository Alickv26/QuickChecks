using System;
using System.Threading.Tasks;
using UnityEngine;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Stub for real-time online turn-based multiplayer over WebSocket.
    /// v1 implementation simulates an online opponent using CPUPlayer
    /// so the game flow can be built and tested end-to-end.
    /// v1.1+ will replace SimulateOpponent() with real WebSocket connection.
    ///
    /// Protocol (see Docs/MULTIPLAYER_REVISION.md):
    ///   Client -> Server -> Opponent:
    ///     { "type": "swipe", "matchId": "...", "swipe": {...} }
    ///     { "type": "turn_end", "matchId": "...", "finalPos": {...} }
    ///   Server -> Both:
    ///     { "type": "turn_start", "matchId": "...", "player": "A"|"B" }
    ///     { "type": "turn_skip", "matchId": "...", "player": "A"|"B" }
    ///     { "type": "race_end", "matchId": "...", "winner": "A"|"B", ... }
    /// </summary>
    public class OnlineTurnClient : MonoBehaviour
    {
        [SerializeField] private string websocketUrl = "wss://YOUR-WSS-URL/quickchecks";
        [SerializeField] private float turnTimeoutSec = 30f;
        [SerializeField] private bool useSimulatedOpponent = true; // v1: always true
        [SerializeField] private Racing.CPUPlayer.Difficulty simulatedDifficulty = Racing.CPUPlayer.Difficulty.Medium;

        public string MatchId { get; private set; }
        public string OpponentName { get; private set; }
        public bool IsMyTurn { get; private set; }
        public bool IsConnected { get; private set; }

        public event Action OnMyTurnStarted;
        public event Action OnOpponentTurnStarted;
        public event Action<QuickChecks.Input.SwipeData> OnOpponentSwipeReceived;
        public event Action OnTurnSkipped;
        public event Action<string> OnRaceEnded; // "won" | "lost" | "draw"

        // WebSocket reference would go here in v1.1:
        // private WebSocket _ws;

        public async Task<bool> ConnectAsync(string playerToken)
        {
            if (useSimulatedOpponent)
            {
                await Task.Delay(100);
                MatchId = System.Guid.NewGuid().ToString();
                OpponentName = "Player_" + UnityEngine.Random.Range(1000, 9999);
                IsConnected = true;
                return true;
            }

            // v1.1: real WebSocket connection.
            // _ws = new WebSocket(websocketUrl + $"?token={playerToken}");
            // _ws.OnMessage += HandleServerMessage;
            // await _ws.Connect();
            // return _ws.IsOpen;
            await Task.Delay(100);
            return false;
        }

        public async Task FindMatchAsync(string trackId)
        {
            if (!IsConnected) return;

            if (useSimulatedOpponent)
            {
                // Simulate matchmaking delay.
                await Task.Delay(500 + UnityEngine.Random.Range(0, 1000));
                // Match found.
                return;
            }

            // v1.1: POST /rpc/find_match { track_id } -> match_id + opponent_id
            await Task.Delay(100);
        }

        /// <summary>
        /// Send my swipe to opponent. Called by SwipeDetector (via event).
        /// </summary>
        public async Task SendSwipeAsync(QuickChecks.Input.SwipeData swipe)
        {
            if (!IsConnected) return;

            if (useSimulatedOpponent)
            {
                // v1: nothing to send, opponent is local CPU.
                await Task.Delay(50);
                return;
            }

            // v1.1: serialize + send over WebSocket.
            // string json = JsonUtility.ToJson(swipe);
            // await _ws.SendText(json);
            await Task.Delay(50);
        }

        public async Task EndMyTurnAsync()
        {
            if (!IsConnected) return;
            if (useSimulatedOpponent)
            {
                await Task.Delay(50);
                // Opponent (CPU) takes their turn.
                IsMyTurn = false;
                OnOpponentTurnStarted?.Invoke();
                return;
            }
            await Task.Delay(50);
        }

        public async Task ConcedeAsync()
        {
            if (!IsConnected) return;
            await Task.Delay(50);
            OnRaceEnded?.Invoke("lost");
        }

        public void Disconnect()
        {
            // v1.1: _ws?.Close();
            IsConnected = false;
            IsMyTurn = false;
        }

        // v1.1: WebSocket message handler
        // private void HandleServerMessage(byte[] data)
        // {
        //     var json = System.Text.Encoding.UTF8.GetString(data);
        //     var msg = JsonUtility.FromJson<ServerMessage>(json);
        //     switch (msg.type)
        //     {
        //         case "turn_start":
        //             IsMyTurn = msg.player == "A";
        //             if (IsMyTurn) OnMyTurnStarted?.Invoke();
        //             else OnOpponentTurnStarted?.Invoke();
        //             break;
        //         case "swipe":
        //             OnOpponentSwipeReceived?.Invoke(msg.swipe);
        //             break;
        //         case "turn_skip":
        //             OnTurnSkipped?.Invoke();
        //             break;
        //         case "race_end":
        //             OnRaceEnded?.Invoke(msg.winner == "A" ? "won" : "lost");
        //             break;
        //     }
        // }
    }

    // v1.1: serialization
    // [Serializable] public class ServerMessage { public string type; public string player; public string winner; public QuickChecks.Input.SwipeData swipe; }
}
