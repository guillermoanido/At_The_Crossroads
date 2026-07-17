using Mirror;
using UnityEngine;

/// Step 1 of ATC networking: stand up a localhost Host/Client connection, hand each
/// connection a "seat" (0 = host / Player 1, 1 = client / Player 2), and only start the
/// match once both seats are filled. The server (host) is authoritative and is the only
/// side that runs GameManager — clients receive synced state in later steps.
///
/// This is additive: offline / AI / hotseat play still works. Leave GameManager's
/// "Auto Start Offline" ON and simply never press Host/Client to play locally as before.
public class ATCNetworkManager : NetworkManager
{
    public override void Start()
    {
        base.Start();
        // Offline mode keeps networking dormant and hides the connect HUD so nobody
        // accidentally hosts. Online mode shows it. (No GameManager yet = treat as online.)
        bool online = GameManager.Instance == null || GameManager.Instance.OnlineMode;
        var hud = GetComponent<NetworkManagerHUD>();
        if (hud != null) hud.enabled = online;
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        if (numPlayers >= maxConnections)
            Debug.Log("[Net] Match is already full; the extra connection will be rejected on add.");
    }

    // Server-side, once per connecting client. Instead of a generic avatar we hand the
    // connection the next free seat and make its seat object the connection's player object
    // — that is what grants that client authority over it.
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        int seat = numPlayers; // 0 for the first player added, 1 for the second

        if (seat >= 2)
        {
            Debug.LogWarning($"[Net] No free seat for connection {conn.connectionId} — disconnecting.");
            conn.Disconnect();
            return;
        }

        GameObject seatObj = Instantiate(playerPrefab);
        var seatComp = seatObj.GetComponent<NetworkPlayerSeat>();
        if (seatComp != null) seatComp.seatIndex = seat;

        NetworkServer.AddPlayerForConnection(conn, seatObj);
        Debug.Log($"[Net] Connection {conn.connectionId} took seat {seat} " +
                  $"({(seat == 0 ? "Player 1 / host" : "Player 2 / client")}).");

        TryStartMatch();
    }

    private void TryStartMatch()
    {
        if (numPlayers < 2) return;

        Debug.Log("[Net] Both seats filled — starting the match on the server.");
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
        else Debug.LogWarning("[Net] No GameManager found in the scene to start.");
    }
}
