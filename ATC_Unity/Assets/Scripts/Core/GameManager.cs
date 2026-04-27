using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player1;
    [SerializeField] private Player player2;

    public Player ActivePlayer { get; private set; }

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
    }

    public void EndTurn()
    {
        SetActivePlayer(ActivePlayer == player1 ? player2 : player1);
    }

    public bool IsActivePlayer(Player player) => ActivePlayer == player;

    private void SetActivePlayer(Player player)
    {
        ActivePlayer = player;
    }
}
