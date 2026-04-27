using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public List<Card> allCards = new List<Card>();

    private int currentCardIndex = 0;

    private void Awake()
    {
        Card[] cards = Resources.LoadAll<Card>("Cards");
        allCards.AddRange(cards);

        if (allCards.Count == 0)
            Debug.LogError("DeckManager: No Card assets found in Resources/Cards.");
    }

    public void DealStartingHand(HandManager hand, int cardCount = 6)
    {
        for (int i = 0; i < cardCount; i++)
            DrawCard(hand);
    }

    public void DrawCard(HandManager hand)
    {
        if (allCards.Count == 0)
        {
            Debug.LogWarning("Deck is empty.");
            return;
        }
        if (hand.IsHandFull)
        {
            Debug.LogWarning("Hand is full.");
            return;
        }
        hand.AddCardToHand(allCards[currentCardIndex]);
        currentCardIndex = (currentCardIndex + 1) % allCards.Count;
    }
}
