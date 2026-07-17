using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// The per-connection "player object" Mirror spawns for each client (assign this prefab as
/// the NetworkManager's Player Prefab). It is the bridge between a client's input and the
/// server-authoritative game:
///   • On the server it binds to the actual game Player for its seat.
///   • Clients send intents via [Command]s (pass, advance phase); the server validates
///     authority (right player, right moment) before touching GameManager / GameStack.
///   • The server pushes per-seat state (whose turn it is, whether a response window is
///     open for this seat) via [SyncVar]s so each client drives its OWN Pass button.
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkPlayerSeat : NetworkBehaviour
{
    [Tooltip("0 = host / Player 1, 1 = client / Player 2. Set by the server on spawn.")]
    [SyncVar] public int seatIndex = -1;

    [SyncVar(hook = nameof(OnResponsePendingChanged))]
    public bool responsePending;

    [SyncVar(hook = nameof(OnActiveTurnChanged))]
    public bool isActiveTurn;

    // Server-only: the game Player this seat controls, and a registry of live seats.
    public Player BoundPlayer { get; private set; }
    private static readonly List<NetworkPlayerSeat> serverSeats = new List<NetworkPlayerSeat>();

    #region Lifecycle / binding

    public override void OnStartServer()
    {
        BoundPlayer = GameManager.Instance != null ? GameManager.Instance.PlayerForSeat(seatIndex) : null;
        if (!serverSeats.Contains(this)) serverSeats.Add(this);
        Debug.Log($"[Net] Seat {seatIndex} bound to {(BoundPlayer != null ? BoundPlayer.name : "NULL")} on server.");
    }

    public override void OnStopServer() => serverSeats.Remove(this);

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"[Net] I am the local player — I control seat {seatIndex}.");
        // SyncVar hooks only fire on a CHANGE, so a false->false initial spawn never runs
        // OnResponsePendingChanged and never hides the scene-default-active Pass button.
        // Seed the local button from the current synced value (false at game start).
        if (GameStack.Instance != null) GameStack.Instance.ShowPassButton(responsePending);
    }

    #endregion

    #region Client intents → server

    // Wired through MatchInput. The server only acts if this seat currently has the right.
    [Command]
    public void CmdAdvancePhase()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (BoundPlayer != null && BoundPlayer == gm.ActivePlayer) gm.AdvancePhase();
        else Debug.Log($"[Net] Seat {seatIndex} tried to advance phase out of turn — ignored.");
    }

    [Command]
    public void CmdPass()
    {
        var gm = GameManager.Instance;
        if (gm == null || GameStack.Instance == null) return;
        if (BoundPlayer != null && BoundPlayer == gm.ControllingPlayer) GameStack.Instance.Pass();
        else Debug.Log($"[Net] Seat {seatIndex} tried to pass without priority — ignored.");
    }

    #endregion

    #region Server → clients (per-seat state)

    // Open a response window for exactly one seat (or none). Server-only.
    public static void ServerSetResponseWindow(Player prioritized)
    {
        foreach (var seat in serverSeats)
            seat.responsePending = prioritized != null && seat.BoundPlayer == prioritized;
    }

    // Mark whose turn it is across all seats. Server-only.
    public static void ServerRefreshTurn()
    {
        var active = GameManager.Instance != null ? GameManager.Instance.ActivePlayer : null;
        foreach (var seat in serverSeats)
            seat.isActiveTurn = seat.BoundPlayer == active;
    }

    public static NetworkPlayerSeat ForPlayer(Player player)
    {
        foreach (var seat in serverSeats)
            if (seat.BoundPlayer == player) return seat;
        return null;
    }

    #endregion

    #region SyncVar hooks (run on clients + host)

    private void OnResponsePendingChanged(bool _, bool now)
    {
        // Only the client that owns this seat should show/hide its own Pass button.
        if (isLocalPlayer && GameStack.Instance != null) GameStack.Instance.ShowPassButton(now);
    }

    private void OnActiveTurnChanged(bool _, bool now)
    {
        if (isLocalPlayer) Debug.Log($"[Net] It is {(now ? "now" : "no longer")} my turn (seat {seatIndex}).");
    }

    #endregion
}
