using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Step 1 of ATC networking: stand up a localhost/LAN Host/Client connection, hand each
/// connection a "seat" (0 = host / Player 1, 1 = client / Player 2), and only start the
/// match once both seats are filled. The server (host) is authoritative and is the only
/// side that runs GameManager — clients receive synced state in later steps.
///
/// This is additive: offline / AI / hotseat play still works. Leave GameManager's
/// "Online Mode" OFF and simply never press Host/Client to play locally as before.
public class ATCNetworkManager : NetworkManager
{
    private enum LinkState { Idle, Connecting, Hosting, Joined, Failed }

    [Header("LAN")]
    [Tooltip("Show the host's own LAN address on screen while online, so the other PC knows what to type into the Client field.")]
    [SerializeField] private bool showLanAddress = true;

    [Header("Connection feedback")]
    [Tooltip("Seconds to wait for the host to answer before reporting the connection as failed.")]
    [SerializeField] private float connectTimeout = 10f;

    [Tooltip("Scene the 'Back to menu' button returns to after a failed connection.")]
    [SerializeField] private string menuScene = "Main Menu";

    private bool matchStarted;
    private string cachedLanEndpoint;
    private List<string> cachedLanCandidates;

    private LinkState link = LinkState.Idle;
    private string linkDetail = string.Empty;
    private float connectDeadline;
    private bool connectHudHidden;

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
                link = LinkState.Hosting;
                StartHost();
                break;
            case MatchSettings.Mode.Join:
                networkAddress = MatchSettings.Address;
                Debug.Log($"[Net] Menu asked to join {networkAddress}.");
                BeginConnecting();
                StartClient();
                break;
        }
    }

    // Host / Client / address controls are setup, not gameplay. Once the match is actually running
    // they are just clutter over the board — and pressing one mid-match would tear the game down.
    private void HideConnectHudOnceMatchStarts()
    {
        if (connectHudHidden) return;

        var gm = GameManager.Instance;
        if (gm == null || !gm.MatchStarted) return;

        connectHudHidden = true;

        var hud = GetComponent<NetworkManagerHUD>();
        if (hud != null) hud.enabled = false;

        Debug.Log("[Net] Match started — connect HUD hidden.");
    }

    private void BeginConnecting()
    {
        link = LinkState.Connecting;
        linkDetail = string.Empty;
        connectDeadline = Time.unscaledTime + connectTimeout;
    }

    // A join that goes nowhere is otherwise completely silent: the scene loads, nothing happens,
    // and there is no way to tell a wrong address from a blocked port. Time it out and say so.
    public override void Update()
    {
        base.Update();

        HideConnectHudOnceMatchStarts();

        if (link != LinkState.Connecting) return;

        if (NetworkClient.isConnected)
        {
            link = LinkState.Joined;
            return;
        }

        if (Time.unscaledTime < connectDeadline) return;

        Fail($"No answer from {networkAddress}:{ListenPort} after {connectTimeout:0} seconds.");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        link = NetworkServer.active ? LinkState.Hosting : LinkState.Joined;
        linkDetail = string.Empty;
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        // The host is its own client too, so its own shutdown would otherwise read as a failure.
        if (NetworkServer.active) return;

        if (link == LinkState.Connecting)
            Fail($"{networkAddress} refused the connection.");
        else if (link == LinkState.Joined)
            Fail("Lost the connection to the host.");
    }

    public override void OnClientError(TransportError error, string reason)
    {
        base.OnClientError(error, reason);
        Fail($"{error}: {reason}");
    }

    private void Fail(string detail)
    {
        if (link == LinkState.Failed) return;

        link = LinkState.Failed;
        linkDetail = detail;
        Debug.LogWarning($"[Net] Connection failed — {detail}");
    }

    private void BackToMenu()
    {
        if (NetworkServer.active) StopHost();
        else if (NetworkClient.active) StopClient();

        link = LinkState.Idle;
        if (!string.IsNullOrEmpty(menuScene)) SceneManager.LoadScene(menuScene);
    }

    // Two PCs on one LAN need exactly one thing that isn't on screen anywhere: the host's address.
    // Print it, plus whatever the connection is currently doing, so a failure in front of an
    // audience is diagnosable instead of an empty screen that never changes.
    private void OnGUI()
    {
        if (!Online) return;

        // Everything here is connection setup, so it goes away once the match is live and the board
        // becomes the interface. A failure is the exception: losing the host mid-match is exactly
        // when the player needs to be told why and given a way back to the menu.
        var gm = GameManager.Instance;
        if (gm != null && gm.MatchStarted && link != LinkState.Failed) return;

        GUILayout.BeginArea(new Rect(10, 118, 470, 220));

        switch (link)
        {
            case LinkState.Failed:
                GUILayout.Label("COULD NOT CONNECT");
                GUILayout.Label(linkDetail);
                GUILayout.Space(4);
                GUILayout.Label("Check, in order:");
                GUILayout.Label("1. Both PCs joined to the same Wi-Fi network");
                GUILayout.Label($"2. Host's firewall allows UDP {ListenPort}");
                GUILayout.Label("3. The address matches what the host's screen shows");
                GUILayout.Space(4);
                if (GUILayout.Button("Back to menu", GUILayout.Width(140))) BackToMenu();
                break;

            case LinkState.Connecting:
                GUILayout.Label($"Connecting to {networkAddress}:{ListenPort} ...");
                break;

            case LinkState.Hosting:
                GUILayout.Label("HOSTING - waiting for the other player.");
                GUILayout.Space(4);
                GUILayout.Label("On the other PC choose Join and type:");
                foreach (var address in LanCandidates)
                    GUILayout.Label($"    {address}");
                GUILayout.Label($"(port {ListenPort})");
                break;

            case LinkState.Joined:
                GUILayout.Label("Connected - waiting for the match to start.");
                break;

            default:
                if (showLanAddress && !NetworkClient.isConnected && !NetworkServer.active)
                {
                    GUILayout.Label($"This PC on the LAN:  {LanEndpoint}");
                    GUILayout.Label("Host: press Host.   Other PC: type that address above, press Client.");
                }
                break;
        }

        GUILayout.EndArea();
    }

    /// This machine's LAN address and listen port — what the other player must type in. Resolved
    /// once and cached; enumerating adapters every OnGUI frame would be wasteful.
    private string LanEndpoint
    {
        get
        {
            if (string.IsNullOrEmpty(cachedLanEndpoint))
                cachedLanEndpoint = $"{LanAddress.Best()}:{ListenPort}";
            return cachedLanEndpoint;
        }
    }

    /// Every address the other PC could try, cached for the same reason as LanEndpoint. The host
    /// screen lists them all rather than guessing, so a PC on two networks still shows both.
    private List<string> LanCandidates
    {
        get
        {
            if (cachedLanCandidates == null) cachedLanCandidates = LanAddress.Candidates();
            return cachedLanCandidates;
        }
    }

    private static string ListenPort
    {
        get
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
