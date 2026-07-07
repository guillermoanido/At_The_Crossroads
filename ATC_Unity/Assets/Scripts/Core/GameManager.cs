using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Draw, Main1, Combat, Main2, EndTurn }

    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player1;
    [SerializeField] private Player player2;

    [Header("Setup")]
    [Tooltip("How many cards each player draws at the start of the game.")]
    [SerializeField] private int startingHandSize = 5;

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

    private void Update() => ApplyLiveScales();

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
            case GamePhase.EndTurn: SwitchTurn();                  break;
        }
    }

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
        ActivePlayer.DrawCard();
        ActivePlayer.ResetStamina();
        BeginPhase(GamePhase.Main1);
    }

    private void ResolveUpkeep()
    {
        Debug.Log($"[Phase] {ActivePlayer.name} → Upkeep");
        ActivePlayer.ResolveUpkeep();
    }

    private void SetActivePlayer(Player player) => ActivePlayer = player;
}
