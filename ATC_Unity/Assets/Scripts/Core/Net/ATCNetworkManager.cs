using Mirror;
using UnityEngine;

/// Step 1 of ATC networking: stand up a localhost/LAN Host/Client connection, hand each
/// connection a "seat" (0 = host / Player 1, 1 = client / Player 2), and only start the
/// match once both seats are filled. The server (host) is authoritative and is the only
/// side that runs GameManager — clients receive synced state in later steps.
///
/// This is additive: offline / AI / hotseat play still works. Leave GameManager's
/// "Online Mode" OFF and simply never press Host/Client to play locally as before.
public class ATCNetworkManager : NetworkManager
{
    private bool matchStarted;

    public override void Start()
    {
        base.Start();

        // Cap at 2 (host-local + 1 remote). Mirror then rejects any extra client at
        // transport-connect time, before it consumes a connection slot.
        maxConnections = 2;

        // Offline mode keeps networking dormant and hides the connect HUD so nobody
        // accidentally hosts. Online mode shows it. (No GameManager yet = treat as online.)
        bool online = GameManager.Instance == null || GameManager.Instance.OnlineMode;
        var hud = GetComponent<NetworkManagerHUD>();
        if (hud != null) hud.enabled = online;
    }

    // Server-side, once per connecting client. Instead of a generic avatar we hand the
    // connection the next free seat and make its seat object the connection's player object
    // — that is what grants that client authority over it.
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (matchStarted)
        {
            Debug.LogWarning($"[Net] Match already started — rejecting join {conn.connectionId}.");
            conn.Disconnect();
            return;
        }

        int seat = FirstFreeSeat();
        if (seat < 0)
        {
            Debug.LogWarning($"[Net] No free seat for connection {conn.connectionId} — disconnecting.");
            conn.Disconnect();
            return;
        }

        GameObject seatObj = Instantiate(playerPrefab);
        var seatComp = seatObj.GetComponent<NetworkPlayerSeat>();
        if (seatComp == null)
        {
            Debug.LogError("[Net] Player Prefab has no NetworkPlayerSeat — the match would start " +
                           "but be uncontrollable. Assign the NetworkPlayerSeat prefab as the " +
                           "NetworkManager's Player Prefab.");
            Destroy(seatObj);
            conn.Disconnect();
            return;
        }
        seatComp.seatIndex = seat;

        NetworkServer.AddPlayerForConnection(conn, seatObj);
        Debug.Log($"[Net] Connection {conn.connectionId} took seat {seat} " +
                  $"({(seat == 0 ? "Player 1 / host" : "Player 2 / client")}).");

        TryStartMatch();
    }

    // Lowest free seat index in [0,1], derived from seats currently held by live connections
    // (a disconnected seat nulls its identity, so it is not counted). Avoids the numPlayers
    // shortcut, which duplicates an index when a non-top seat vacates and someone rejoins.
    private int FirstFreeSeat()
    {
        bool[] taken = new bool[2];
        foreach (var kv in NetworkServer.connections)
        {
            var identity = kv.Value.identity;
            if (identity == null) continue;
            var s = identity.GetComponent<NetworkPlayerSeat>();
            if (s != null && s.seatIndex >= 0 && s.seatIndex < 2) taken[s.seatIndex] = true;
        }
        for (int i = 0; i < 2; i++)
            if (!taken[i]) return i;
        return -1;
    }

    private void TryStartMatch()
    {
        if (matchStarted) return;   // never restart an in-progress match (e.g. on a rejoin)
        if (numPlayers < 2) return;

        matchStarted = true;        // set before StartGame so re-entrancy can't double-fire
        Debug.Log("[Net] Both seats filled — starting the match on the server.");
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
        else Debug.LogWarning("[Net] No GameManager found in the scene to start.");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        matchStarted = false;       // so a fresh host session can start a new match
    }
}
