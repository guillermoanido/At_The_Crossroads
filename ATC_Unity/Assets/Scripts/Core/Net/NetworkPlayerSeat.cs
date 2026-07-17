using Mirror;
using UnityEngine;

/// The per-connection "player object" Mirror spawns for each client (assign this prefab as
/// the NetworkManager's Player Prefab). In step 1 it just carries the seat number so each
/// side knows which Player it controls. Step 2 will add [Command] methods here so a client's
/// clicks are sent to the server to drive that seat's Player.
public class NetworkPlayerSeat : NetworkBehaviour
{
    [Tooltip("0 = host / Player 1, 1 = client / Player 2. Set by the server on spawn.")]
    [SyncVar] public int seatIndex = -1;

    public override void OnStartClient()
    {
        Debug.Log($"[Net] Seat object for seat {seatIndex} spawned on this client (isLocalPlayer={isLocalPlayer}).");
    }

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"[Net] I am the local player — I control seat {seatIndex}.");
    }
}
