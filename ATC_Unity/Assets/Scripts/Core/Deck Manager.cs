using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public List<Card> allCards = new List<Card>();

    private int currentCardIndex = 0;

    private void Start()
    {
        Card[] cards = Resources.LoadAll<Card>("Cards");

        allCards.AddRange(cards);

        HandManager hand = FindFirstObjectByType<HandManager>();
        for (int i = 0; i < 6; i++)
        {
            DrawCard(hand);
        }
    }

    public void DrawCard(HandManager handManager)
    {
        if (allCards.Count == 0)
        {
            Debug.LogWarning("Deck is empty! Cannot draw a card.");
            return;
        }
        Card nextCard = allCards[currentCardIndex];
        handManager.AddCardToHand(nextCard);
        currentCardIndex = (currentCardIndex +1) & allCards.Count;
    }



}
