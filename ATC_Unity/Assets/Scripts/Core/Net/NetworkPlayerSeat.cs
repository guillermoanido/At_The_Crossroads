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

    // How many cards this seat holds. Public to everyone so the OPPONENT can mirror the hidden
    // hand as this many face-down backs. The owner ignores it and shows their real cards instead
    // (pushed privately via TargetSyncOwnHand).
    [SyncVar(hook = nameof(OnHandCountChanged))]
    public int handCount;

    // Stats are PUBLIC (both players see each other's HP/stamina/defense), so plain SyncVars to
    // everyone. Applied on clients to the display-mapped Player; PlayerStatsUI polls it. Defaults
    // match Player's defaults so the pre-sync display is already correct.
    [SyncVar(hook = nameof(OnStatSynced))] private int syncHp = 30;
    [SyncVar(hook = nameof(OnStatSynced))] private int syncStamina = 3;
    [SyncVar(hook = nameof(OnStatSynced))] private int syncMaxStamina = 3;
    [SyncVar(hook = nameof(OnStatSynced))] private int syncDefense;

    private bool boardDirty;   // server: a zone changed, push the board snapshot next Update

    // Server-only: the game Player this seat controls, and a registry of live seats.
    public Player BoundPlayer { get; private set; }
    private static readonly List<NetworkPlayerSeat> serverSeats = new List<NetworkPlayerSeat>();

    // Client-side registry of every seat that exists on this machine (used to re-render all hands
    // into the correct slot once we learn which seat is locally controlled).
    private static readonly List<NetworkPlayerSeat> clientSeats = new List<NetworkPlayerSeat>();

    #region Lifecycle / binding

    public override void OnStartServer()
    {
        BoundPlayer = GameManager.Instance != null ? GameManager.Instance.PlayerForSeat(seatIndex) : null;
        if (!serverSeats.Contains(this)) serverSeats.Add(this);
        Debug.Log($"[Net] Seat {seatIndex} bound to {(BoundPlayer != null ? BoundPlayer.name : "NULL")} on server.");

        // Push this seat's hand to clients whenever it changes (deal, draw, play, discard...).
        if (BoundPlayer != null && BoundPlayer.handManager != null)
        {
            BoundPlayer.handManager.OnHandChanged += ServerPushHand;
            ServerPushHand();
        }

        // Push this seat's board to clients whenever any of its zones change.
        if (BoundPlayer != null)
        {
            foreach (var zone in BoundPlayer.SyncedZones())
                if (zone != null) zone.OnChanged += MarkBoardDirty;
            boardDirty = true;   // publish the initial (empty) board once
        }
    }

    public override void OnStopServer()
    {
        serverSeats.Remove(this);
        if (BoundPlayer != null && BoundPlayer.handManager != null)
            BoundPlayer.handManager.OnHandChanged -= ServerPushHand;
        if (BoundPlayer != null)
            foreach (var zone in BoundPlayer.SyncedZones())
                if (zone != null) zone.OnChanged -= MarkBoardDirty;
    }

    // Server: mirror the authoritative stats (SyncVars only send on change) and flush a board
    // snapshot when a zone changed this frame.
    private void Update()
    {
        if (!isServer || BoundPlayer == null) return;

        syncHp = BoundPlayer.CurrentHp;
        syncStamina = BoundPlayer.Stamina;
        syncMaxStamina = BoundPlayer.MaxStamina;
        syncDefense = BoundPlayer.Defense;

        if (boardDirty) { boardDirty = false; ServerPushBoard(); }
    }

    private void MarkBoardDirty() => boardDirty = true;

    // Runs on every client (and the host) once this seat exists. Sets each machine's perspective
    // and seeds the parts that a SyncVar hook won't fire for on a fresh spawn.
    public override void OnStartClient()
    {
        if (!clientSeats.Contains(this)) clientSeats.Add(this);

        // The opponent object is reliably NOT the local player, so we can render its backs here.
        // The host already owns the opponent's real card objects — just show their backs; a
        // remote client instead builds placeholder backs from the synced count. (If we don't yet
        // know our own seat, this may land in the wrong slot; OnStartLocalPlayer re-renders.)
        if (!isLocalPlayer)
            RenderOpponentHand(handCount);
    }

    public override void OnStopClient() => clientSeats.Remove(this);

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"[Net] I am the local player — I control seat {seatIndex}.");
        // SyncVar hooks only fire on a CHANGE, so a false->false initial spawn never runs
        // OnResponsePendingChanged and never hides the scene-default-active Pass button.
        // Seed the local button from the current synced value (false at game start).
        if (GameStack.Instance != null) GameStack.Instance.ShowPassButton(responsePending);

        // Now that our seat is known, the local player is displayed in the BOTTOM slot (player1)
        // and the opponent in the TOP slot (player2) — regardless of which network seat we are.
        if (GameManager.Instance != null) GameManager.Instance.LocalSeat = seatIndex;

        // My own hand is shown face-up in the bottom slot. Ask the server to (re)send it in case
        // the deal happened before this object finished spawning.
        var hand = HandFor(seatIndex);
        if (hand != null) hand.SetFaceUpMode(true);
        CmdRequestHandResync();

        // Re-render opponents into the top slot: their initial OnStartClient render happened
        // before we knew our own seat, so it may have gone to the wrong slot.
        foreach (var s in clientSeats)
            if (!s.isLocalPlayer) s.RenderOpponentHand(s.handCount);
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

    // Asked by a client when its player object finishes spawning, to cover the case where the
    // opening hand was dealt before this seat existed on that client.
    [Command]
    private void CmdRequestHandResync() => ServerPushHand();

    // Play a card from THIS seat's hand, identified by its stable CardDatabase id — NOT a positional
    // index, because the client's hand is an async mirror that may have shifted (a draw, an
    // opponent's discard/return) between the client capturing the card and this command arriving.
    // The host resolves the card by id and validates everything in TryPlayCard (priority,
    // speed/phase, stamina), so illegal/out-of-turn plays are rejected here.
    [Command]
    public void CmdPlayCard(int cardId)
    {
        if (BoundPlayer == null || BoundPlayer.handManager == null) return;

        var db = CardDatabase.Instance;
        GameObject cardGO = null;
        foreach (var go in BoundPlayer.handManager.cardsInHand)
        {
            var d = go != null ? go.GetComponent<CardDisplay>()?.cardData : null;
            if (d != null && db != null && db.Id(d) == cardId) { cardGO = go; break; }
        }

        var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
        if (data != null) BoundPlayer.TryPlayCard(cardGO, data);

        // Always re-sync: a successful play already changed the hand, but on a reject / no-match
        // (e.g. the client re-dropped a card that's already gone) this rebuilds the client's hand so
        // the card it optimistically froze on drop becomes interactive again.
        ServerPushHand();
    }

    #endregion

    #region Hand sync (server → clients)

    // Server-authoritative: publish this seat's hand. Everyone learns the count (for opponent
    // backs); only the owning client gets the real card ids (its private, face-up hand).
    [Server]
    private void ServerPushHand()
    {
        var hand = BoundPlayer != null ? BoundPlayer.handManager : null;
        if (hand == null) return;

        var db = CardDatabase.Instance;
        var ids = new int[hand.cardsInHand.Count];
        for (int i = 0; i < ids.Length; i++)
        {
            var data = hand.cardsInHand[i] != null
                ? hand.cardsInHand[i].GetComponent<CardDisplay>()?.cardData : null;
            ids[i] = db != null ? db.Id(data) : -1;
        }

        handCount = ids.Length;      // SyncVar → all clients (opponent back count)
        Debug.Log($"[Net] Server push: seat {seatIndex} hand = {ids.Length} card(s).");
        TargetSyncOwnHand(ids);      // → only this seat's owner
    }

    // Delivered to the owning client: rebuild its real, face-up hand from the synced ids. The
    // host is skipped — it already holds the authoritative card objects.
    [TargetRpc]
    private void TargetSyncOwnHand(int[] ids)
    {
        if (isServer) return;

        var hand = HandFor(seatIndex);
        if (hand == null) return;

        hand.SetFaceUpMode(true);
        hand.ClearHand();

        var db = CardDatabase.Instance;
        int built = 0;
        foreach (int id in ids)
        {
            var card = db != null ? db.FromId(id) : null;
            if (card != null) { hand.AddCardToHand(card); built++; }
        }
        Debug.Log($"[Net] Own hand delivered on client: seat {seatIndex}, built {built}/{ids.Length} " +
                  $"(db={(db != null ? "ok" : "NULL")}).");
    }

    // Show this seat's hand as face-down backs (it belongs to the opponent on this machine).
    private void RenderOpponentHand(int count)
    {
        if (isLocalPlayer) return;   // never redraw my own hand from a back-count

        var hand = HandFor(seatIndex);
        if (hand == null) return;

        // The host owns the opponent's real cards — flip them to backs rather than rebuild.
        if (isServer)
        {
            hand.SetFaceUpMode(false);
            return;
        }

        // Remote client: mirror the hidden hand as `count` faceless backs.
        hand.ClearHand();
        hand.SetFaceUpMode(false);
        for (int i = 0; i < count; i++) hand.AddFaceDownPlaceholder();
        Debug.Log($"[Net] Opponent backs on client: seat {seatIndex} → {count} back(s).");
    }

    // Uses the DISPLAY mapping (local → bottom slot, opponent → top slot), not the logical seat
    // binding, so each machine shows its own player at the bottom.
    private static HandManager HandFor(int seat)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.DisplayPlayerForSeat(seat) : null;
        return player != null ? player.handManager : null;
    }

    #endregion

    #region Board + stats sync (server → all clients; public info)

    // Snapshot every synced zone as parallel arrays: zoneCounts[i] = number of cards in the i-th
    // zone (SyncedZones order), cardIds = all card ids concatenated in that same zone order. Two
    // flat int[] avoid nested-array serialization and let the client clear every zone deterministically.
    [Server]
    private void ServerPushBoard()
    {
        if (BoundPlayer == null) return;
        var db = CardDatabase.Instance;
        var counts = new List<int>();
        var ids = new List<int>();

        foreach (var zone in BoundPlayer.SyncedZones())
        {
            int n = 0;
            if (zone != null)
            {
                foreach (var go in zone.Cards)
                {
                    var d = go != null ? go.GetComponent<CardDisplay>()?.cardData : null;
                    ids.Add(db != null ? db.Id(d) : -1);
                    n++;
                }
            }
            counts.Add(n);
        }

        RpcSyncBoard(counts.ToArray(), ids.ToArray());
    }

    // Rebuild this seat's board into the DISPLAY-mapped player's zones (local → bottom, opp → top).
    // The host is skipped — it holds the real, authoritative board objects.
    [ClientRpc]
    private void RpcSyncBoard(int[] zoneCounts, int[] cardIds)
    {
        if (isServer) return;

        var player = GameManager.Instance != null ? GameManager.Instance.DisplayPlayerForSeat(seatIndex) : null;
        if (player == null || player.handManager == null) return;

        var prefab = player.handManager.cardPrefab;
        // Instantiate under handPosition first (known to sit under a Canvas, since hand cards work);
        // CardZone.AddCard then reparents into the zone. This keeps CardMovement.Awake happy.
        var parent = player.handManager.handPosition;
        var db = CardDatabase.Instance;

        int idIndex = 0, zi = 0;
        foreach (var zone in player.SyncedZones())
        {
            int n = zi < zoneCounts.Length ? zoneCounts[zi] : 0;
            zi++;

            if (zone == null) { idIndex += n; continue; }
            zone.ClearDisplayCards();

            for (int k = 0; k < n; k++)
            {
                int id = idIndex < cardIds.Length ? cardIds[idIndex] : -1;
                idIndex++;

                var card = db != null ? db.FromId(id) : null;
                if (card == null || prefab == null) continue;

                var go = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
                var disp = go.GetComponent<CardDisplay>();
                if (disp != null) { disp.cardData = card; disp.SetFaceUp(true); }
                CardDisplay.DisableGameplayInteractions(go);   // display-only; activation-over-net is later
                zone.AddCard(go);
            }
        }
    }

    private void OnStatSynced(int _, int __) => ApplyStatsToClient();

    private void ApplyStatsToClient()
    {
        if (isServer) return;   // the host has the real stats
        var player = GameManager.Instance != null ? GameManager.Instance.DisplayPlayerForSeat(seatIndex) : null;
        player?.ClientApplyStats(syncHp, syncStamina, syncMaxStamina, syncDefense);
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

    private void OnHandCountChanged(int _, int now) => RenderOpponentHand(now);

    #endregion
}
