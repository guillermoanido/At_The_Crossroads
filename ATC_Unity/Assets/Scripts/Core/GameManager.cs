using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Draw, Main1, Combat, Main2, EndTurn }

    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player1;
    [SerializeField] private Player player2;

    [Header("Card Display")]
    [Tooltip("Visual scale applied to cards entering any zone (Discard, Weapon, Shield, etc.).")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float zoneCardScale = 1f;

    public float ZoneCardScale => zoneCardScale;

    public Player ActivePlayer { get; private set; }
    public GamePhase CurrentPhase { get; private set; }

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
        StartGame();
    }

    private void StartGame()
    {
        player1.deckManager.DealStartingHand(player1.handManager);
        player2.deckManager.DealStartingHand(player2.handManager);
        SetActivePlayer(player1);
        BeginPhase(GamePhase.Draw);
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

    private void BeginPhase(GamePhase phase)
    {
        CurrentPhase = phase;
        Debug.Log($"[Phase] {ActivePlayer.name} → {phase}");

        if (phase == GamePhase.Draw)
        {
            ActivePlayer.DrawCard();
            ActivePlayer.ResetStamina();
            BeginPhase(GamePhase.Main1);
        }
    }

    public void GoToCombat()
    {
        if (CurrentPhase == GamePhase.Main1)
        {
            BeginPhase(GamePhase.Combat);
        }
    }

    // Skip the turn flip — same player draws, refreshes stamina, and proceeds again.
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

    private void SetActivePlayer(Player player)
    {
        ActivePlayer = player;
    }
}
