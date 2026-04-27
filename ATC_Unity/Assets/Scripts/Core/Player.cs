using UnityEngine;

public class Player : MonoBehaviour
{
    public HandManager handManager;
    public DeckManager deckManager;

    private void Awake()
    {
        handManager.SetOwner(this);
    }
}
