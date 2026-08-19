using System;
using System.Collections;
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
    [SyncVar(hook = nameof(OnStatSynced))] private int syncBurn;
    [SyncVar(hook = nameof(OnStatSynced))] private int syncBleed;
    [SyncVar(hook = nameof(OnStatSynced))] private int syncDivineShield;

    // Whose turn it is, who holds priority and which phase the match is in. Public information,
    // and the same on every seat — clients don't run the turn loop, so without this their phase
    // indicator and "can I act?" checks would be stuck on the defaults forever.
    [SyncVar(hook = nameof(OnPhaseSynced))] private GameManager.GamePhase syncPhase;
    [SyncVar(hook = nameof(OnActiveSeatSynced))] private int syncActiveSeat = -1;
    [SyncVar(hook = nameof(OnPrioritySeatSynced))] private int syncPrioritySeat = -1;

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

        // Tapping moves no card between zones, so it would never trigger a board push on its own.
        CardTapState.TapStateChanged += OnCardTapped;
    }

    public override void OnStopServer()
    {
        serverSeats.Remove(this);
        CardTapState.TapStateChanged -= OnCardTapped;
        if (BoundPlayer != null && BoundPlayer.handManager != null)
            BoundPlayer.handManager.OnHandChanged -= ServerPushHand;
        if (BoundPlayer != null)
            foreach (var zone in BoundPlayer.SyncedZones())
                if (zone != null) zone.OnChanged -= MarkBoardDirty;

        // A seat that leaves mid-choice would otherwise strand the effect waiting for it, and with
        // it the whole stack. Answer the open request as a cancel on the way out.
        if (HasPendingTargetRequest) ServerFinishTargetRequest(null);
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
        syncBurn = BoundPlayer.Burn;
        syncBleed = BoundPlayer.Bleed;
        syncDivineShield = BoundPlayer.DivineShield;

        if (boardDirty) ServerPushBoard();   // clears the flag itself
    }

    private void MarkBoardDirty() => boardDirty = true;

    // Only republish when the card that flipped belongs to this seat — otherwise every tap would
    // rebuild both players' boards on every client.
    private void OnCardTapped(CardTapState tapState)
    {
        if (tapState == null || BoundPlayer == null) return;
        if (Targetable.OwnerOf(tapState.gameObject) == BoundPlayer) boardDirty = true;
    }

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

        // Seed the turn/phase view: hooks only fire on a change, so a seat that spawns already
        // holding the current values would never push them.
        ApplyMatchStateToClient();
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

        // Hand the server our deck before asking for cards — the opening hand is dealt from it.
        SubmitLocalDeck();
        CmdRequestHandResync();

        // Re-render opponents into the top slot: their initial OnStartClient render happened
        // before we knew our own seat, so it may have gone to the wrong slot.
        foreach (var s in clientSeats)
            if (!s.isLocalPlayer) s.RenderOpponentHand(s.handCount);

        // Turn/priority are stored as SEAT indices and read back through the display mapping, so
        // they only resolve correctly now that we know which seat is ours.
        ApplyMatchStateToClient();
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

    /// True once this seat has told the server what it is playing with. The match waits for both.
    public bool HasSubmittedDeck { get; private set; }

    // Each player brings their own deck, and only the host runs the game — so the client has to
    // hand its deck over. Cards travel as CardDatabase ids, the same stable numbering hands and
    // boards already use. Attributes come along so the server can check the deck is legal.
    [Command]
    private void CmdSubmitDeck(int[] cardIds, int strength, int intellect, int wisdom, int dexterity)
    {
        var db = CardDatabase.Instance;
        var cards = new List<Card>();

        if (cardIds != null && db != null)
        {
            foreach (int id in cardIds)
            {
                var card = db.FromId(id);
                if (card != null) cards.Add(card);
            }
        }

        WarnAboutIllegalCards(cards, strength, intellect, wisdom, dexterity);

        if (BoundPlayer != null && BoundPlayer.deckManager != null)
            BoundPlayer.deckManager.LoadRuntimeDeck(cards, $"seat {seatIndex}'s deck");

        HasSubmittedDeck = true;
        Debug.Log($"[Net] Seat {seatIndex} submitted a {cards.Count}-card deck.");

        // The last deck to arrive is usually what the match was waiting on.
        (NetworkManager.singleton as ATCNetworkManager)?.TryStartMatch();
    }

    // The host is authoritative about legality, but a mismatched deck is a setup mistake rather
    // than an attack — log it loudly and play on rather than dropping the player.
    [Server]
    private void WarnAboutIllegalCards(List<Card> cards, int strength, int intellect, int wisdom, int dexterity)
    {
        if (cards.Count < DeckRules.MinDeckSize)
            Debug.LogWarning($"[Net] Seat {seatIndex} sent {cards.Count} cards; minimum is {DeckRules.MinDeckSize}.");

        foreach (var card in cards)
        {
            if (strength >= card.strRequired && intellect >= card.intRequired
                && wisdom >= card.wisRequired && dexterity >= card.dexRequired) continue;

            Debug.LogWarning($"[Net] Seat {seatIndex}'s deck contains '{card.cardName}', which its " +
                             $"attributes don't meet.");
        }
    }

    // Runs on the owning client as it joins: send whatever deck the menu picked. Always sends,
    // even with nothing chosen (scene opened directly) — an empty deck is the signal to keep the
    // scene's own cards, and it lets the server tell "no deck wanted" from "deck still in flight".
    private void SubmitLocalDeck()
    {
        var deck = MatchSettings.SelectedDeck;
        var db = CardDatabase.Instance;

        if (deck == null || db == null)
        {
            CmdSubmitDeck(new int[0], 0, 0, 0, 0);
            return;
        }

        var ids = new int[deck.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = db.Id(deck.cards[i]);

        CmdSubmitDeck(ids, deck.strength, deck.intellect, deck.wisdom, deck.dexterity);
    }

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

    // Activate a permanent this seat controls (double-click in play). The client only knows a
    // picture of the board, so it names the card by its instance id and the host looks up the real
    // object. Ownership is checked here; TryActivateCard re-checks priority, phase, tap and stamina.
    [Command]
    public void CmdActivateCard(int cardInstanceId)
    {
        if (BoundPlayer == null) return;

        var cardGO = CardInstance.Find(cardInstanceId);
        var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
        if (data == null) return;

        var zone = cardGO.GetComponentInParent<CardZone>();
        if (zone == null || !BoundPlayer.OwnsZone(zone))
        {
            Debug.Log($"[Net] Seat {seatIndex} tried to activate a card it doesn't control — ignored.");
            return;
        }

        BoundPlayer.TryActivateCard(cardGO, data);
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
    // zone (SyncedZones order); cardIds / instanceIds are all cards concatenated in that same zone
    // order. Flat int[] avoid nested-array serialization and let the client clear every zone
    // deterministically. The instance id is what lets a client point back at an exact card.
    [Server]
    private void ServerPushBoard()
    {
        if (BoundPlayer == null) return;
        var db = CardDatabase.Instance;
        var counts = new List<int>();
        var ids = new List<int>();
        var instances = new List<int>();
        var tapped = new List<bool>();

        foreach (var zone in BoundPlayer.SyncedZones())
        {
            int n = 0;
            if (zone != null)
            {
                foreach (var go in zone.Cards)
                {
                    var d = go != null ? go.GetComponent<CardDisplay>()?.cardData : null;
                    ids.Add(db != null ? db.Id(d) : -1);
                    instances.Add(CardInstance.EnsureId(go));
                    tapped.Add(go != null && go.GetComponent<CardTapState>() is CardTapState tap && tap.IsTapped);
                    n++;
                }
            }
            counts.Add(n);
        }

        boardDirty = false;
        RpcSyncBoard(counts.ToArray(), ids.ToArray(), instances.ToArray(), tapped.ToArray());
    }

    // Rebuild this seat's board into the DISPLAY-mapped player's zones (local → bottom, opp → top).
    // The host is skipped — it holds the real, authoritative board objects.
    [ClientRpc]
    private void RpcSyncBoard(int[] zoneCounts, int[] cardIds, int[] instanceIds, bool[] tappedFlags)
    {
        if (isServer) return;

        var player = GameManager.Instance != null ? GameManager.Instance.DisplayPlayerForSeat(seatIndex) : null;
        if (player == null || player.handManager == null) return;

        var prefab = player.handManager.cardPrefab;
        // Instantiate under handPosition first (known to sit under a Canvas, since hand cards work);
        // CardZone.AddCard then reparents into the zone. This keeps CardMovement.Awake happy.
        var parent = player.handManager.handPosition;
        var db = CardDatabase.Instance;

        int cardIndex = 0, zoneIndex = 0;
        foreach (var zone in player.SyncedZones())
        {
            int n = zoneIndex < zoneCounts.Length ? zoneCounts[zoneIndex] : 0;
            zoneIndex++;

            if (zone == null) { cardIndex += n; continue; }
            zone.ClearDisplayCards();

            for (int k = 0; k < n; k++)
            {
                int cardId = cardIndex < cardIds.Length ? cardIds[cardIndex] : -1;
                int instanceId = cardIndex < instanceIds.Length ? instanceIds[cardIndex] : CardInstance.None;
                bool isTapped = cardIndex < tappedFlags.Length && tappedFlags[cardIndex];
                cardIndex++;

                var card = db != null ? db.FromId(cardId) : null;
                if (card == null || prefab == null) continue;

                var go = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
                var display = go.GetComponent<CardDisplay>();
                if (display != null) { display.cardData = card; display.SetFaceUp(true); }

                CardInstance.Bind(go, instanceId);
                ConfigureMirroredCard(go, zone);
                zone.AddCard(go);

                // After AddCard: it resets localRotation, which would undo the tapped angle.
                if (isTapped) go.GetComponent<CardTapState>()?.Tap();
            }
        }
    }

    // A client's board card is a picture of the host's: never dragged and never resolved locally,
    // but it must still hover-preview and be clickable when an effect asks this player to choose a
    // target. In the discard/exile piles it stays fully inert so the click reaches the pile browser
    // underneath — cards there are never legal targets anyway.
    private static void ConfigureMirroredCard(GameObject cardGO, CardZone zone)
    {
        var movement = cardGO.GetComponent<CardMovement>();
        if (movement != null) movement.enabled = false;

        var actions = cardGO.GetComponent<CardBoardActions>();
        if (actions == null) return;

        bool isPile = zone.Kind == CardZone.ZoneKind.Discard || zone.Kind == CardZone.ZoneKind.Exile;
        actions.SetMirrored(true);
        actions.enabled = !isPile;
    }

    // Publish any board change that is still queued for the next Update. Called before a target
    // request goes out so the client's copy — and the instance ids in it — already match the ids
    // the request is about to name.
    [Server]
    public static void ServerFlushBoards()
    {
        foreach (var seat in serverSeats)
            if (seat != null && seat.boardDirty) seat.ServerPushBoard();
    }

    private void OnStatSynced(int _, int __) => ApplyStatsToClient();

    private void ApplyStatsToClient()
    {
        if (isServer) return;   // the host has the real stats
        var player = GameManager.Instance != null ? GameManager.Instance.DisplayPlayerForSeat(seatIndex) : null;
        player?.ClientApplyStats(syncHp, syncStamina, syncMaxStamina, syncDefense,
                                 syncBurn, syncBleed, syncDivineShield);
    }

    private void ApplyMatchStateToClient()
    {
        if (isServer) return;   // the host runs the turn loop itself
        GameManager.Instance?.ClientApplyTurnState(syncPhase, syncActiveSeat, syncPrioritySeat);
    }

    #endregion

    #region Remote stack reorder (Timewatch)

    private System.Action onReorderFinished;

    /// Server: let this seat's remote player rearrange the stack. Card NAMES go over the wire since
    /// the client only needs to read them, and the answer comes back as a permutation the host
    /// applies — the client can't add or drop an item.
    [Server]
    public void ServerRequestStackReorder(List<string> cardNames, System.Action onFinished)
    {
        if (cardNames == null || cardNames.Count < 2) { onFinished?.Invoke(); return; }

        onReorderFinished = onFinished;
        TargetReorderStack(cardNames.ToArray());
    }

    [TargetRpc]
    private void TargetReorderStack(string[] cardNames)
        => StackReorderPanel.Show(cardNames, chosen => CmdApplyStackOrder(chosen, cardNames.Length));

    [Command]
    private void CmdApplyStackOrder(int[] resolutionOrder, int shown)
    {
        GameStack.Instance?.ApplyOrder(EffectRunner.ToStackOrder(resolutionOrder, shown));

        var finished = onReorderFinished;
        onReorderFinished = null;
        finished?.Invoke();
    }

    #endregion

    #region Revealing a hand to the player who earned the look

    /// Server: show `handOwner`'s hand to THIS seat's player. On a remote client the opponent's
    /// hand is only a row of blank backs, so the real cards have to be sent for it to reveal.
    [Server]
    public void ServerRevealHandTo(Player handOwner)
    {
        var hand = handOwner != null ? handOwner.handManager : null;
        var db = CardDatabase.Instance;
        if (hand == null || db == null) return;

        var ids = new int[hand.cardsInHand.Count];
        for (int i = 0; i < ids.Length; i++)
        {
            var data = hand.cardsInHand[i] != null
                ? hand.cardsInHand[i].GetComponent<CardDisplay>()?.cardData : null;
            ids[i] = db.Id(data);
        }

        TargetRevealHand(GameManager.Instance != null ? GameManager.Instance.SeatOf(handOwner) : -1, ids);
    }

    [TargetRpc]
    private void TargetRevealHand(int revealedSeat, int[] cardIds)
    {
        var hand = HandFor(revealedSeat);
        var db = CardDatabase.Instance;
        if (hand == null || db == null) return;

        // Swap the blank backs for the real cards, then let the reveal flag show them face-up.
        hand.ClearHand();
        foreach (int id in cardIds)
        {
            var card = db.FromId(id);
            if (card != null) hand.AddCardToHand(card);
        }
        hand.RevealUntilEndOfTurn();
    }

    #endregion

    #region Remote Scry (the scrying player sorts their own deck, wherever they are sitting)

    private System.Action onScryFinished;

    /// Server: ask this seat's remote player to Scry. The host sends the cards it is showing them,
    /// the client answers with which ones to bin, and the host applies it — the client never gets
    /// to choose cards the host didn't offer.
    [Server]
    public void ServerRequestScry(int count, System.Action onFinished)
    {
        var deck = BoundPlayer != null ? BoundPlayer.deckManager : null;
        var db = CardDatabase.Instance;
        if (deck == null || db == null) { onFinished?.Invoke(); return; }

        var top = deck.PeekTop(count);
        if (top.Count == 0) { onFinished?.Invoke(); return; }

        var ids = new int[top.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = db.Id(top[i]);

        onScryFinished = onFinished;
        TargetScry(ids);
    }

    [TargetRpc]
    private void TargetScry(int[] cardIds)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.LocalDisplayPlayer : null;
        var panel = player != null ? player.scryPanel : null;
        var db = CardDatabase.Instance;

        if (panel == null || db == null) { CmdFinishScry(0, new int[0]); return; }

        var cards = new List<Card>();
        foreach (int id in cardIds)
        {
            var card = db.FromId(id);
            if (card != null) cards.Add(card);
        }

        panel.OpenRemote(cards, binned => CmdFinishScry(cards.Count, binned));
    }

    /// The client's answer: how many cards it was shown, and which of them (by index) to discard.
    /// The count comes back explicitly so binning nothing is not mistaken for showing nothing.
    [Command]
    private void CmdFinishScry(int shown, int[] binnedIndices)
    {
        var deck = BoundPlayer != null ? BoundPlayer.deckManager : null;
        if (deck != null && shown > 0)
        {
            var top = deck.PeekTop(shown);
            var binned = new HashSet<int>(binnedIndices ?? new int[0]);

            var kept = new List<Card>();
            var discarded = new List<Card>();
            for (int i = 0; i < top.Count; i++)
            {
                if (binned.Contains(i)) discarded.Add(top[i]);
                else kept.Add(top[i]);
            }

            // Only the window that was shown is rebuilt; the rest of the deck is untouched.
            deck.ReplaceTop(top.Count, kept);
            foreach (var card in discarded) BoundPlayer.PutCardInDiscard(card);
            Debug.Log($"[Scry] Seat {seatIndex} kept {kept.Count} on top and discarded {discarded.Count}.");
        }

        var finished = onScryFinished;
        onScryFinished = null;
        finished?.Invoke();
    }

    #endregion

    #region Remote targeting (host asks, the owning client answers)

    // How long the host waits for a client to pick before giving up. The stack is paused for the
    // whole match while a request is open, so it must never wait forever.
    private const float TargetRequestTimeout = 60f;

    private int targetRequestId;
    private List<Targetable> pendingTargets;
    private Action<Targetable> onTargetChosen;
    private Action onTargetCancelled;
    private Coroutine targetTimeout;

    private bool HasPendingTargetRequest => pendingTargets != null;

    /// Server: ask this seat's remote player to pick one of the cards matching `filter`. The legal
    /// set is worked out here, on the authority, and only the resulting ids cross the wire — the
    /// client cannot widen its own choices.
    [Server]
    public void ServerRequestTarget(Predicate<Targetable> filter, string prompt,
                                    Action<Targetable> onChosen, Action onCancel)
    {
        var targets = TargetingService.Collect(filter);
        if (targets.Count == 0)
        {
            Debug.Log($"[Net] Seat {seatIndex} has no legal target — the effect fizzles.");
            onCancel?.Invoke();
            return;
        }

        // Never strand an older request: answering it as a cancel keeps its effect moving.
        if (HasPendingTargetRequest) ServerFinishTargetRequest(null);

        // Make sure the client's board (and the ids in it) is current before naming ids to it.
        ServerFlushBoards();

        var instanceIds = new int[targets.Count];
        for (int i = 0; i < targets.Count; i++)
            instanceIds[i] = CardInstance.EnsureId(targets[i].gameObject);

        targetRequestId++;
        pendingTargets = targets;
        onTargetChosen = onChosen;
        onTargetCancelled = onCancel;
        targetTimeout = StartCoroutine(ExpireTargetRequest(targetRequestId));

        Debug.Log($"[Net] Asking seat {seatIndex} to choose 1 of {targets.Count} target(s).");
        TargetChooseTarget(targetRequestId, instanceIds, prompt);
    }

    // Delivered to the one client that must choose: light up its own copies of those cards and
    // report back whatever it picks (or a cancel).
    [TargetRpc]
    private void TargetChooseTarget(int requestId, int[] instanceIds, string prompt)
    {
        var service = TargetingService.Instance;
        var targets = ResolveLocalTargets(instanceIds);

        if (service == null || targets.Count == 0)
        {
            Debug.LogWarning($"[Net] Target request {requestId} could not be shown ({targets.Count} of " +
                             $"{instanceIds.Length} card(s) found locally) — answering as a cancel.");
            CmdAnswerTarget(requestId, CardInstance.None);
            return;
        }

        service.RequestFrom(
            targets,
            chosen => CmdAnswerTarget(requestId, CardInstance.IdOf(chosen.gameObject)),
            () => CmdAnswerTarget(requestId, CardInstance.None),
            requester: GameManager.Instance != null ? GameManager.Instance.LocalDisplayPlayer : null,
            prompt: prompt);
    }

    // The client's answer. `CardInstance.None` means it cancelled (or could not choose).
    [Command]
    private void CmdAnswerTarget(int requestId, int cardInstanceId)
    {
        if (!HasPendingTargetRequest || requestId != targetRequestId) return;   // stale answer

        Targetable chosen = null;
        foreach (var target in pendingTargets)
        {
            if (target == null) continue;
            if (CardInstance.IdOf(target.gameObject) != cardInstanceId) continue;
            chosen = target;
            break;
        }

        ServerFinishTargetRequest(chosen);
    }

    // Turn the ids the host sent into this machine's own card objects.
    private static List<Targetable> ResolveLocalTargets(int[] instanceIds)
    {
        var targets = new List<Targetable>();
        if (instanceIds == null) return targets;

        foreach (int id in instanceIds)
        {
            var cardGO = CardInstance.Find(id);
            var target = cardGO != null ? cardGO.GetComponent<Targetable>() : null;
            if (target != null) targets.Add(target);
        }
        return targets;
    }

    // Deliberately not [Server]: this also runs while the server is shutting down, and the effect
    // waiting on the answer must always be released or the stack hangs.
    private void ServerFinishTargetRequest(Targetable chosen)
    {
        var chosenCallback = onTargetChosen;
        var cancelCallback = onTargetCancelled;

        pendingTargets = null;
        onTargetChosen = null;
        onTargetCancelled = null;
        if (targetTimeout != null) { StopCoroutine(targetTimeout); targetTimeout = null; }

        if (chosen != null) chosenCallback?.Invoke(chosen);
        else cancelCallback?.Invoke();
    }

    private IEnumerator ExpireTargetRequest(int requestId)
    {
        yield return new WaitForSeconds(TargetRequestTimeout);

        if (!HasPendingTargetRequest || requestId != targetRequestId) yield break;
        Debug.LogWarning($"[Net] Seat {seatIndex} did not choose a target in time — cancelling so play can continue.");
        ServerFinishTargetRequest(null);
    }

    #endregion

    #region Server → clients (per-seat state)

    // Open a response window for exactly one seat (or none). Server-only.
    public static void ServerSetResponseWindow(Player prioritized)
    {
        foreach (var seat in serverSeats)
            seat.responsePending = prioritized != null && seat.BoundPlayer == prioritized;
    }

    // Publish the shared turn state — active player, priority holder and phase — to every seat.
    // Server-only, and a no-op offline (no seats are registered).
    public static void ServerRefreshMatchState()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        foreach (var seat in serverSeats)
            if (seat != null) seat.ServerApplyMatchState(gm);
    }

    /// Tell both machines what the opening roll was. Only the numbers travel — each client writes
    /// its own sentence, so every player reads their own roll first rather than "Player 1"/"Player 2"
    /// from the host's perspective. One ClientRpc reaches every client, so a single seat sends it.
    public static void ServerAnnounceRoll(int rollSeat0, int rollSeat1, int winningSeat)
    {
        if (!NetworkServer.active)
        {
            GameManager.Instance?.ShowOpeningRoll(rollSeat0, rollSeat1, winningSeat);
            return;
        }

        foreach (var seat in serverSeats)
        {
            if (seat == null) continue;
            seat.RpcAnnounceRoll(rollSeat0, rollSeat1, winningSeat);
            return;
        }

        // Server with no seats spawned yet (shouldn't happen once a match starts) — show it here.
        GameManager.Instance?.ShowOpeningRoll(rollSeat0, rollSeat1, winningSeat);
    }

    [ClientRpc]
    private void RpcAnnounceRoll(int rollSeat0, int rollSeat1, int winningSeat)
        => GameManager.Instance?.ShowOpeningRoll(rollSeat0, rollSeat1, winningSeat);

    private void ServerApplyMatchState(GameManager gm)
    {
        isActiveTurn = BoundPlayer == gm.ActivePlayer;
        syncPhase = gm.CurrentPhase;
        syncActiveSeat = gm.SeatOf(gm.ActivePlayer);
        syncPrioritySeat = gm.SeatOf(gm.ControllingPlayer);
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

    private void OnPhaseSynced(GameManager.GamePhase _, GameManager.GamePhase __) => ApplyMatchStateToClient();
    private void OnActiveSeatSynced(int _, int __) => ApplyMatchStateToClient();
    private void OnPrioritySeatSynced(int _, int __) => ApplyMatchStateToClient();

    #endregion
}
