using UnityEngine;

public class Player : MonoBehaviour
{
    public HandManager handManager;
    public DeckManager deckManager;

    [SerializeField] private int maxStamina = 3;

    public int Stamina { get; private set; }
    public int MaxStamina => maxStamina;

    private void Awake()
    {
        handManager.SetOwner(this);
        Stamina = maxStamina;
    }

    public void DrawCard()
    {
        deckManager.DrawCard(handManager);
    }

    public void ResetStamina()
    {
        Stamina = maxStamina;
    }

    // Returns false if the player doesn't have enough stamina.
    public bool SpendStamina(int amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }
}
