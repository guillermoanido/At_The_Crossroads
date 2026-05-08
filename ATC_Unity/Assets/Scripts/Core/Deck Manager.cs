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

    // Returns the next n cards starting from the current draw position. Read-only peek.
    public System.Collections.Generic.IList<Card> PeekTop(int n)
    {
        var result = new System.Collections.Generic.List<Card>();
        if (allCards.Count == 0) return result;
        for (int i = 0; i < n && i < allCards.Count; i++)
            result.Add(allCards[(currentCardIndex + i) % allCards.Count]);
        return result;
    }

    // Writes a reordered slice back into the same top-of-deck slots peeked above.
    public void SetTopOrder(System.Collections.Generic.IList<Card> newOrder)
    {
        if (allCards.Count == 0 || newOrder == null) return;
        for (int i = 0; i < newOrder.Count && i < allCards.Count; i++)
            allCards[(currentCardIndex + i) % allCards.Count] = newOrder[i];
    }

    // Fisher-Yates shuffle. Resets the draw cursor to the top.
    public void Shuffle()
    {
        for (int i = allCards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allCards[i], allCards[j]) = (allCards[j], allCards[i]);
        }
        currentCardIndex = 0;
        Debug.Log($"[Deck] {name} shuffled ({allCards.Count} cards).");
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
