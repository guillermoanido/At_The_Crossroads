using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Draw, Main1, Combat, Main2, EndTurn }

    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player1;
    [SerializeField] private Player player2;

    [Header("Setup")]
    [Tooltip("How many cards each player holds at the start of the game.")]
    [SerializeField] private int startingHandSize = 6;

    [Tooltip("Key the non-active player presses to take priority for a reflex response (and again to hand it back). Local-multiplayer stand-in for per-client input.")]
    [SerializeField] private Key passPriorityKey = Key.Tab;

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

    public Player ActivePlayer { get; private set; }
    public GamePhase CurrentPhase { get; private set; }

    // Who may currently touch cards. Defaults to the active player; the non-active player can take it
    // temporarily (a reflex window) via passPriorityKey. Networked play will drive this from client identity.
    public Player ControllingPlayer { get; private set; }
    public bool IsControllingPlayer(Player player) => ControllingPlayer == player;

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

    private void Start() => StartGame();

    private void Update()
    {
        ApplyLiveScales();
        HandlePriorityInput();
    }

    // Local-multiplayer stand-in: the non-active player grabs priority to answer with a reflex, then hands it back.
    private void HandlePriorityInput()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[passPriorityKey].wasPressedThisFrame) TogglePriority();
    }

    public void TogglePriority()
    {
        ControllingPlayer = (ControllingPlayer == ActivePlayer) ? Opponent(ActivePlayer) : ActivePlayer;
        Debug.Log($"[Priority] {(ControllingPlayer != null ? ControllingPlayer.name : "<none>")} now has priority.");
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

    private void StartGame()
    {
        DealOpeningHands();
        SetActivePlayer(player1);
        skipNextDraw = true;   // the first player skips the draw on their very first turn
        BeginPhase(GamePhase.Draw);
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
        if (queuedExtraTurns > 0)
        {
            queuedExtraTurns--;
            ControllingPlayer = ActivePlayer;   // priority returns to the player taking the extra turn
            Debug.Log($"[Phase] {ActivePlayer.name} takes an extra turn.");
            BeginPhase(GamePhase.Draw);   // same active player goes again
        }
        else SwitchTurn();
    }

    // Queued by an effect (e.g. Light Speed): the current player takes another turn once this one ends.
    // Counts, so two extra-turn sources in one turn grant two extra turns.
    public void QueueExtraTurn() => queuedExtraTurns++;

    public void GoToCombat()
    {
        if (CurrentPhase == GamePhase.Main1) BeginPhase(GamePhase.Combat);
    }

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

        if (phase == GamePhase.Draw) ResolveDrawPhase();
    }

    private void ResolveDrawPhase()
    {
        ResolveUpkeep();
        if (skipNextDraw) skipNextDraw = false;
        else ActivePlayer.DrawCard();
        ActivePlayer.ResetStamina();
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
        ControllingPlayer = player;   // priority returns to whoever's turn it is
    }
}
