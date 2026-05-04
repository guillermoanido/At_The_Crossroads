using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Prep, Main1, Combat, Main2, EndTurn }

    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player1;
    [SerializeField] private Player player2;

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
        BeginPhase(GamePhase.Prep);
    }

    // Call this from UI buttons or game logic to move to the next phase.
    public void AdvancePhase()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Prep:    BeginPhase(GamePhase.Main1);   break;
            case GamePhase.Main1:   BeginPhase(GamePhase.Combat);  break;
            case GamePhase.Combat:  BeginPhase(GamePhase.Main2);   break;
            case GamePhase.Main2:   BeginPhase(GamePhase.EndTurn); break;
            case GamePhase.EndTurn: SwitchTurn();                  break;
        }
    }

    private void BeginPhase(GamePhase phase)
    {
        CurrentPhase = phase;

        if (phase == GamePhase.Prep)
        {
            ActivePlayer.DrawCard();
            ActivePlayer.ResetStamina();
        }
    }

    private void SwitchTurn()
    {
        SetActivePlayer(ActivePlayer == player1 ? player2 : player1);
        BeginPhase(GamePhase.Prep);
    }

    public bool IsActivePlayer(Player player) => ActivePlayer == player;

    private void SetActivePlayer(Player player)
    {
        ActivePlayer = player;
    }
}
