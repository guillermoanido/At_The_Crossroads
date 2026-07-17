using Mirror;
using UnityEngine;

/// Single entry point for the local player's "control" inputs (pass priority, advance phase).
/// Wire the Pass button and the phase-advance button OnClick to the methods here instead of
/// calling GameStack/GameManager directly. It routes the intent based on mode:
///   • Offline  → call the game logic directly (unchanged behaviour).
///   • Online   → send a [Command] from THIS client's seat so the host validates and runs it.
public class MatchInput : MonoBehaviour
{
    public static MatchInput Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Hook the phase-advance Button's OnClick to this.
    public void RequestAdvancePhase()
    {
        if (IsOnline())
        {
            var seat = LocalSeat();
            if (seat != null) seat.CmdAdvancePhase();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvancePhase();
        }
    }

    // Hook the Pass Button's OnClick to this.
    public void RequestPass()
    {
        if (IsOnline())
        {
            var seat = LocalSeat();
            if (seat != null) seat.CmdPass();
        }
        else if (GameStack.Instance != null)
        {
            GameStack.Instance.Pass();
        }
    }

    private static bool IsOnline()
        => GameManager.Instance != null && GameManager.Instance.OnlineMode;

    private static NetworkPlayerSeat LocalSeat()
    {
        var lp = NetworkClient.localPlayer;
        return lp != null ? lp.GetComponent<NetworkPlayerSeat>() : null;
    }
}
