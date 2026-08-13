using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    [Header("LAN")]
    [Tooltip("Show the host's own LAN address on screen while online, so the other PC knows what to type into the Client field.")]
    [SerializeField] private bool showLanAddress = true;

    private bool matchStarted;
    private string cachedLanEndpoint;

    private bool Online => GameManager.Instance == null || GameManager.Instance.OnlineMode;

    public override void Start()
    {
        base.Start();

        // Cap at 2 (host-local + 1 remote). Mirror then rejects any extra client at
        // transport-connect time, before it consumes a connection slot.
        maxConnections = 2;

        // Offline mode keeps networking dormant and hides the connect HUD so nobody
        // accidentally hosts. Online mode shows it. (No GameManager yet = treat as online.)
        var hud = GetComponent<NetworkManagerHUD>();
        if (hud != null) hud.enabled = Online;

        ActOnMenuChoice();
    }

    // The menu decided host-or-join before loading this scene. Consuming the choice means a manual
    // scene reload won't silently reconnect, and opening the scene directly still shows the HUD.
    private void ActOnMenuChoice()
    {
        switch (MatchSettings.ConsumePendingMode())
        {
            case MatchSettings.Mode.Host:
                Debug.Log("[Net] Menu asked to host.");
                StartHost();
                break;
            case MatchSettings.Mode.Join:
                networkAddress = MatchSettings.Address;
                Debug.Log($"[Net] Menu asked to join {networkAddress}.");
                StartClient();
                break;
        }
    }

    // Two PCs on one LAN need exactly one thing that isn't on screen anywhere: the host's address.
    // Print it under Mirror's connect HUD so the second player can type it in and press Client.
    private void OnGUI()
    {
        if (!showLanAddress || !Online) return;
        if (NetworkClient.isConnected || NetworkServer.active) return;

        GUILayout.BeginArea(new Rect(10, 118, 420, 60));
        GUILayout.Label($"This PC on the LAN:  {LanEndpoint}");
        GUILayout.Label("Host: press Host.   Other PC: type that address above, press Client.");
        GUILayout.EndArea();
    }

    /// This machine's LAN address and listen port — what the other player must type in. Resolved
    /// once and cached; enumerating adapters every OnGUI frame would be wasteful.
    private string LanEndpoint
    {
        get
        {
            if (string.IsNullOrEmpty(cachedLanEndpoint))
                cachedLanEndpoint = $"{FindLanAddress()}:{FindListenPort()}";
            return cachedLanEndpoint;
        }
    }

    private static string FindLanAddress()
    {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up) continue;
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    return unicast.Address.ToString();
        }
        return "unknown";
    }

    private static string FindListenPort()
    {
        try
        {
            return Transport.active != null ? Transport.active.ServerUri().Port.ToString() : "?";
        }
        catch
        {
            return "?";   // a transport that can't describe itself before starting
        }
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

    public override void OnStartServer()
    {
        base.OnStartServer();
        CardInstance.ResetIds();   // a new session numbers its cards from scratch
    }

    /// Starts the match once both seats are filled AND both have handed over their decks. Called
    /// again as each deck arrives, since the last one to land is usually what it was waiting for.
    public void TryStartMatch()
    {
        if (matchStarted) return;   // never restart an in-progress match (e.g. on a rejoin)
        if (numPlayers < 2) return;
        if (!AllSeatsHaveDecks()) return;

        matchStarted = true;        // set before StartGame so re-entrancy can't double-fire
        Debug.Log("[Net] Both seats filled and both decks in — starting the match on the server.");
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
        else Debug.LogWarning("[Net] No GameManager found in the scene to start.");
    }

    // Every client submits as it spawns — an empty submission means "I have no deck, use the
    // scene's", so waiting on all of them is safe even when nobody came through the menu.
    private static bool AllSeatsHaveDecks()
    {
        foreach (var connection in NetworkServer.connections.Values)
        {
            var seat = connection?.identity != null ? connection.identity.GetComponent<NetworkPlayerSeat>() : null;
            if (seat != null && !seat.HasSubmittedDeck) return false;
        }
        return true;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        matchStarted = false;       // so a fresh host session can start a new match
    }
}
