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

    public bool HasCards => allCards.Count > 0;

    private void Awake()
    {
        if (deckDefinition != null) LoadFromDefinition(deckDefinition);
        else if (loadFromResources) LoadCardsFromResources();
        if (allCards.Count == 0) Debug.LogWarning($"[Deck] {name} has 0 cards.");
    }

    public void LoadFromDefinition(DeckDefinition deck)
    {
        if (deck == null) return;
        if (!DeckRules.Validate(deck, out var problems))
            foreach (var p in problems) Debug.LogWarning($"[Deck] {deck.deckName}: {p}");
        allCards = new List<Card>(deck.cards);
    }

    /// Replace the deck with one assembled at runtime — the list a player brought from the menu,
    /// or the one a client sent over the wire. Ignored if empty, so a failed transfer leaves the
    /// scene's fallback deck in place rather than starting a match with no cards.
    public void LoadRuntimeDeck(IList<Card> cards, string sourceName)
    {
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning($"[Deck] {name}: '{sourceName}' had no cards — keeping the existing deck.");
            return;
        }

        allCards = new List<Card>(cards);
        Debug.Log($"[Deck] {name} loaded '{sourceName}' ({allCards.Count} cards).");
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

    /// Resolve a Scry: lift `count` cards off the top and put `keepInOrder` back, in that order.
    /// Anything not kept has been binned by the player and never returns to the deck.
    public void ReplaceTop(int count, IList<Card> keepInOrder)
    {
        int lifted = Mathf.Min(count, allCards.Count);
        allCards.RemoveRange(0, lifted);

        if (keepInOrder == null) return;
        for (int i = keepInOrder.Count - 1; i >= 0; i--)
            allCards.Insert(0, keepInOrder[i]);
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
