using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [SerializeField] private HandManager handManager;

    public List<Card> allCards = new List<Card>();

    private int currentCardIndex = 0;

    private void Start()
    {
        Card[] cards = Resources.LoadAll<Card>("Cards");
        allCards.AddRange(cards);

        for (int i = 0; i < 6; i++)
            DrawCard(handManager);
    }

    public void DrawCard(HandManager hand)
    {
        if (allCards.Count == 0)
        {
            Debug.LogWarning("Deck is empty! Cannot draw a card.");
            return;
        }
        hand.AddCardToHand(allCards[currentCardIndex]);
        currentCardIndex = (currentCardIndex + 1) % allCards.Count;
    }
}
