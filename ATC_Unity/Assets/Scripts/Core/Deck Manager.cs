using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Tooltip("The deck. Draws come off index 0; cards are removed when drawn.")]
    public List<Card> allCards = new List<Card>();

    [Tooltip("If true, also loads every Card asset under Resources/Cards into this deck on Awake. Turn off to use only the Inspector list.")]
    [SerializeField] private bool loadFromResources = true;

    private void Awake()
    {
        if (loadFromResources)
        {
            Card[] cards = Resources.LoadAll<Card>("Cards");
            allCards.AddRange(cards);
        }

        if (allCards.Count == 0)
            Debug.LogWarning($"[Deck] {name} has 0 cards.");
    }

    public int CardsRemaining => allCards.Count;

    public void DealStartingHand(HandManager hand, int cardCount = 6)
    {
        for (int i = 0; i < cardCount; i++)
            DrawCard(hand);
    }

    public void DrawCard(HandManager hand)
    {
        if (allCards.Count == 0)
        {
            Debug.LogWarning($"[Deck] {name} is empty — no card drawn.");
            return;
        }
        if (hand.IsHandFull)
        {
            Debug.LogWarning($"[Deck] {hand.name} is full — no card drawn.");
            return;
        }

        var card = allCards[0];
        allCards.RemoveAt(0);
        hand.AddCardToHand(card);
    }

    // Returns the next n cards from the top of the deck. Read-only peek.
    public IList<Card> PeekTop(int n)
    {
        var result = new List<Card>();
        for (int i = 0; i < n && i < allCards.Count; i++)
            result.Add(allCards[i]);
        return result;
    }

    // Writes a reordered slice back into the top of the deck.
    public void SetTopOrder(IList<Card> newOrder)
    {
        if (newOrder == null) return;
        for (int i = 0; i < newOrder.Count && i < allCards.Count; i++)
            allCards[i] = newOrder[i];
    }

    public void Shuffle()
    {
        for (int i = allCards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allCards[i], allCards[j]) = (allCards[j], allCards[i]);
        }
        Debug.Log($"[Deck] {name} shuffled ({allCards.Count} cards).");
    }
}
