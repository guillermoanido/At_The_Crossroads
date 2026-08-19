using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Draw, Main1, Combat, Main2, EndTurn }

    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player1;
    [SerializeField] private Player player2;

    [Header("Turn Order")]
    [Tooltip("Sides on the die each player rolls at the start of the match. Highest goes first; ties are re-rolled.")]
    [SerializeField] private int turnOrderDieSides = 20;

    [Tooltip("How long the opening-roll result stays on screen before it clears itself.")]
    [SerializeField] private float rollMessageSeconds = 6f;

    [Header("Setup")]
    [Tooltip("How many cards each player holds at the start of the game.")]
    [SerializeField] private int startingHandSize = 6;

    [Tooltip("OFF = Offline: the match auto-starts on load for hotseat / AI play, and the network HUD is hidden. ON = Online: the game waits for two players to connect (the NetworkManager starts it), the connect HUD is shown, and the AI is disabled.")]
    [SerializeField] private bool onlineMode = false;

    public bool OnlineMode => onlineMode;

    private bool skipNextDraw;
    private int queuedExtraTurns;

    private const float MinScale = 0.2f;
    private const float MaxScale = 1.5f;

    [Header("Live Card Sizes — tweak during play")]
    [Tooltip("Size of the cards fanned in each player's hand.")]
    [Range(MinScale, MaxScale)] public float handCardScale = 1f;

    [Tooltip("Size of cards played onto the board — every equipment/board zone (weapon, shield, armour, equipment, accessory, talent, aura).")]
    [Range(MinScale, MaxScale)] public float playAreaCardScale = 1f;

    [Tooltip("Size of cards resting in the discard and exile piles.")]
    [Range(MinScale, MaxScale)] public float discardExileCardScale = 1f;

    [Tooltip("Card size inside the discard/exile browser and scry popups — one control for all of them.")]
    [Range(MinScale, MaxScale)] public float popupCardScale = 1f;

    [Tooltip("Horizontal gap between cards in the play-area zones so multiple cards spread out instead of overlapping. Tune live.")]
    public float playAreaCardSpacing = 200f;

    public Player ActivePlayer { get; private set; }
    public GamePhase CurrentPhase { get; private set; }

    public Player ControllingPlayer { get; private set; }
    public bool IsControllingPlayer(Player player) => ControllingPlayer == player;

    // Seat 0 = Player 1 (host), seat 1 = Player 2 (client). Used by the networking layer
    // to bind a connection's seat to the actual game Player it controls.
    public Player PlayerForSeat(int seat) => seat == 0 ? player1 : seat == 1 ? player2 : null;
    public int SeatOf(Player player) => player == player1 ? 0 : player == player2 ? 1 : -1;

    // The seat THIS machine controls (0 on the host / offline). The networking layer sets it so
    // the local player is always DISPLAYED in Player 1's (bottom) slot and the opponent in
    // Player 2's (top) slot — regardless of network seat. This is display-only; game logic and
    // seat binding still use PlayerForSeat.
    public int LocalSeat { get; set; }

    // Which on-screen Player renders a given network seat: the local seat → bottom slot (player1),
    // everyone else → top slot (player2). On the host this is the identity map (local seat 0).
    public Player DisplayPlayerForSeat(int seat) => seat == LocalSeat ? player1 : player2;

    // The on-screen Player that represents the LOCAL player — always the bottom slot after the
    // display remap. Used to decide "is this my card?" for hover/inspection in online play.
    public Player LocalDisplayPlayer => DisplayPlayerForSeat(LocalSeat);

    /// Client-side: adopt the turn state the host published. Clients never run the turn loop, so
    /// without this their phase indicator and every "is it my turn?" check would sit on the
    /// defaults all match. Seats are mapped through the display slots, like the rest of the view.
    public void ClientApplyTurnState(GamePhase phase, int activeSeat, int prioritySeat)
    {
        // A real active seat means the host has started the match, which is this machine's cue to
        // drop the connect HUD.
        if (activeSeat >= 0) MatchStarted = true;

        if (activeSeat != lastSyncedActiveSeat)
        {
            lastSyncedActiveSeat = activeSeat;
            ClearHandReveals();
        }

        CurrentPhase = phase;
        ActivePlayer = DisplayPlayerForSeatOrNull(activeSeat);
        ControllingPlayer = DisplayPlayerForSeatOrNull(prioritySeat);
    }

    private int lastSyncedActiveSeat = -1;

    private Player DisplayPlayerForSeatOrNull(int seat) => seat >= 0 ? DisplayPlayerForSeat(seat) : null;

    public float GetScaleForZone(CardZone.ZoneKind kind)
    {
        switch (kind)
        {
            case CardZone.ZoneKind.Discard:
            case CardZone.ZoneKind.Exile:
                return discardExileCardScale;
            default:
                return playAreaCardScale;
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (!onlineMode) StartGame();
    }

    private void Update() => ApplyLiveScales();

    public void GivePriorityTo(Player player)
    {
        ControllingPlayer = player;
        NetworkPlayerSeat.ServerRefreshMatchState();   // no-op offline / on clients
    }

    private void ApplyLiveScales()
    {
        ApplyHandScale(player1);
        ApplyHandScale(player2);
        ApplyZoneScales(player1);
        ApplyZoneScales(player2);
    }

    private void ApplyHandScale(Player player)
    {
        if (player == null || player.handManager == null) return;
        if (Mathf.Approximately(player.handManager.cardScale, handCardScale)) return;
        player.handManager.cardScale = handCardScale;
        player.handManager.RefreshLayout();
    }

    private void ApplyZoneScales(Player player)
    {
        if (player == null) return;
        ApplyZoneScale(player.discardZone);
        ApplyZoneScale(player.weaponZone);
        ApplyZoneScale(player.shieldZone);
        ApplyZoneScale(player.armourZone);
        ApplyZoneScale(player.equipmentZone);
        ApplyZoneScale(player.accessoryZone);
        ApplyZoneScale(player.talentZone);
        ApplyZoneScale(player.auraZone);
        ApplyZoneScale(player.exileZone);
    }

    private void ApplyZoneScale(CardZone zone)
    {
        if (zone == null) return;
        zone.RefreshLayout();
    }

    public void StartGame()
    {
        if (player1 != null) player1.deckManager.Shuffle();
        if (player2 != null) player2.deckManager.Shuffle();
        DealOpeningHands();

        SetActivePlayer(RollForFirstPlayer());
        MatchStarted = true;

        skipNextDraw = true;
        BeginPhase(GamePhase.Draw);
    }

    /// Both players roll; the higher number takes the first turn. Ties are re-rolled, so the roll
    /// always produces a winner. Runs on the server (or locally offline) — the result reaches the
    /// other machine as the synced active seat, and the numbers are announced separately so both
    /// players can see the roll that decided it.
    private Player RollForFirstPlayer()
    {
        if (player1 == null || player2 == null) return player1 != null ? player1 : player2;

        int sides = Mathf.Max(2, turnOrderDieSides);
        int rollP1, rollP2;
        int safety = 0;

        do
        {
            rollP1 = UnityEngine.Random.Range(1, sides + 1);
            rollP2 = UnityEngine.Random.Range(1, sides + 1);
            safety++;
        }
        while (rollP1 == rollP2 && safety < 50);

        int winningSeat = rollP1 >= rollP2 ? 0 : 1;
        Debug.Log($"[Match] Opening roll — Player 1: {rollP1}, Player 2: {rollP2}. " +
                  $"Seat {winningSeat} goes first.");

        if (OnlineMode) NetworkPlayerSeat.ServerAnnounceRoll(rollP1, rollP2, winningSeat);
        else ShowOpeningRoll(rollP1, rollP2, winningSeat);

        return PlayerForSeat(winningSeat);
    }

    /// Writes the opening roll from the point of view of whoever is looking at THIS screen, so each
    /// player reads their own number first. Hotseat shares one screen, so it names both players.
    public void ShowOpeningRoll(int rollSeat0, int rollSeat1, int winningSeat)
    {
        GameLogHUD.Ensure();

        if (!OnlineMode)
        {
            GameLog.Instruct("log.roll_hotseat", rollSeat0, rollSeat1, winningSeat + 1);
        }
        else
        {
            int mine   = LocalSeat == 0 ? rollSeat0 : rollSeat1;
            int theirs = LocalSeat == 0 ? rollSeat1 : rollSeat0;

            GameLog.Instruct(winningSeat == LocalSeat ? "log.roll_you_first" : "log.roll_opponent_first",
                             mine, theirs);
        }

        StartCoroutine(ClearRollMessageLater());
    }

    // Nothing routinely clears the standing instruction, so without this the roll would sit over
    // the board for the whole match.
    private IEnumerator ClearRollMessageLater()
    {
        string shown = GameLog.Instruction;
        yield return new WaitForSeconds(Mathf.Max(1f, rollMessageSeconds));

        // Only clear our own line — a targeting prompt may well have replaced it by now.
        if (GameLog.Instruction == shown) GameLog.ClearInstruction();
    }

    private void DealOpeningHands()
    {
        player1.deckManager.DealStartingHand(player1.handManager, startingHandSize);
        player2.deckManager.DealStartingHand(player2.handManager, startingHandSize);
    }

    public void AdvancePhase()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Draw:    BeginPhase(GamePhase.Main1);   break;
            case GamePhase.Main1:   BeginPhase(GamePhase.Combat);  break;
            case GamePhase.Combat:  BeginPhase(GamePhase.Main2);   break;
            case GamePhase.Main2:   BeginPhase(GamePhase.EndTurn); break;
            case GamePhase.EndTurn: EndTurn();                     break;
        }
    }

    private void EndTurn()
    {
        // Status damage and expiring Block resolve for BOTH players at the end of every turn.
        if (player1 != null) player1.ResolveEndOfTurn();
        if (player2 != null) player2.ResolveEndOfTurn();

        if (queuedExtraTurns > 0)
        {
            queuedExtraTurns--;
            ControllingPlayer = ActivePlayer;
            Debug.Log($"[Phase] {ActivePlayer.name} takes an extra turn.");
            BeginPhase(GamePhase.Draw);
        }
        else SwitchTurn();
    }

    public void QueueExtraTurn() => queuedExtraTurns++;

    public void TakeExtraTurn()
    {
        Debug.Log($"[Phase] {ActivePlayer.name} takes an extra turn!");
        BeginPhase(GamePhase.Draw);
    }

    public void SwitchTurn()
    {
        SetActivePlayer(ActivePlayer == player1 ? player2 : player1);
        BeginPhase(GamePhase.Draw);
    }

    public bool IsActivePlayer(Player player) => ActivePlayer == player;

    #region Match end

    /// The winner, once someone has won. Null while the match is still running.
    public Player Winner { get; private set; }
    public bool MatchOver => Winner != null;

    /// True once the match is actually underway. The connect HUD watches this so the host/join
    /// controls disappear the moment play begins. Clients set it when the first turn state lands.
    public bool MatchStarted { get; private set; }

    /// Called after any HP change. Running out of life loses you the match.
    public void CheckForDefeat(Player player)
    {
        if (MatchOver || player == null || player.CurrentHp > 0) return;
        DeclareWinner(Opponent(player), $"{player.name} is out of life");
    }

    public void DeclareWinner(Player winner, string reason)
    {
        if (MatchOver || winner == null) return;
        Winner = winner;
        Debug.Log($"[Match] {winner.name} WINS — {reason}.");
        GameLog.Instruct(winner == LocalDisplayPlayer ? "log.you_win" : "log.you_lose");
    }

    #endregion

    public Player Opponent(Player player)
    {
        if (player == player1) return player2;
        if (player == player2) return player1;
        return null;
    }

    private void BeginPhase(GamePhase phase)
    {
        CurrentPhase = phase;
        Debug.Log($"[Phase] {ActivePlayer.name} → {phase}");
        GameLogHUD.Ensure();
        NetworkPlayerSeat.ServerRefreshMatchState();   // no-op offline / on clients

        if (phase == GamePhase.Draw) ResolveDrawPhase();
    }

    /// Hands laid open by an effect go back to being backs when the turn ends.
    public void ClearHandReveals()
    {
        if (player1 != null && player1.handManager != null) player1.handManager.ClearReveal();
        if (player2 != null && player2.handManager != null) player2.handManager.ClearReveal();
    }

    private void ResolveDrawPhase()
    {
        ClearHandReveals();

        // "For the rest of this turn" ends here, for both players — a Silence cast on your turn
        // shouldn't still be muzzling the opponent during theirs.
        if (player1 != null) player1.ClearTurnEffects();
        if (player2 != null) player2.ClearTurnEffects();

        ResolveUpkeep();
        if (skipNextDraw) skipNextDraw = false;
        else ActivePlayer.DrawCard();
        BeginPhase(GamePhase.Main1);
    }

    private void ResolveUpkeep()
    {
        Debug.Log($"[Phase] {ActivePlayer.name} → Upkeep");
        ActivePlayer.ResolveUpkeep();
    }

    private void SetActivePlayer(Player player)
    {
        ActivePlayer = player;
        ControllingPlayer = player;
        NetworkPlayerSeat.ServerRefreshMatchState();   // no-op offline / on clients
    }
}
