using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Tooltip("The deck. Draws come off index 0; cards are removed when drawn.")]
    public List<Card> allCards = new List<Card>();

    [Tooltip("If set, this built deck (validated against DeckRules) is played instead of the Resources/Inspector list.")]
    [SerializeField] private DeckDefinition deckDefinition;

    [Tooltip("If true, also loads every Card asset under Resources/Cards into this deck on Awake. Turn off to use only the Inspector list.")]
    [SerializeField] private bool loadFromResources = true;

    public int CardsRemaining => allCards.Count;

    private void Awake()
    {
        if (deckDefinition != null) LoadFromDefinition(deckDefinition);
        else if (loadFromResources) LoadCardsFromResources();
        if (allCards.Count == 0) Debug.LogWarning($"[Deck] {name} has 0 cards.");
    }

    // Populates this play-deck from a built DeckDefinition, warning (but still loading) if it's illegal.
    public void LoadFromDefinition(DeckDefinition deck)
    {
        if (deck == null) return;
        if (!DeckRules.Validate(deck, out var problems))
            foreach (var p in problems) Debug.LogWarning($"[Deck] {deck.deckName}: {p}");
        allCards = new List<Card>(deck.cards);
    }

    public void DealStartingHand(HandManager hand, int cardCount)
    {
        for (int i = 0; i < cardCount; i++) DrawCard(hand);
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

    public IList<Card> PeekTop(int n)
    {
        var result = new List<Card>();
        for (int i = 0; i < n && i < allCards.Count; i++)
            result.Add(allCards[i]);
        return result;
    }

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

    private void LoadCardsFromResources()
    {
        var resourceCards = Resources.LoadAll<Card>("Cards");
        allCards.AddRange(resourceCards);
    }
}
