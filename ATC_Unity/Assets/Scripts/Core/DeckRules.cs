using System.Collections.Generic;

public static class DeckRules
{
    public const int MinDeckSize = 30;

    /// PLACEHOLDER NUMBERS — tune these two, they are the whole shape of deck building.
    /// The most demanding cards need 17 in a stat, so a budget of 20 buys one class plus a small
    /// splash; raise it to allow real two-class decks.
    public const int MaxCopiesPerCard = 3;
    public const int AttributePointBudget = 20;

    public static int SpentAttributePoints(DeckDefinition deck)
        => deck == null ? 0 : deck.strength + deck.intellect + deck.wisdom + deck.dexterity;

    public static bool MeetsAttributeRequirement(DeckDefinition deck, Card card)
    {
        if (deck == null || card == null) return false;
        return deck.strength  >= card.strRequired
            && deck.intellect >= card.intRequired
            && deck.wisdom    >= card.wisRequired
            && deck.dexterity >= card.dexRequired;
    }

    public static bool Validate(DeckDefinition deck, out List<string> problems)
    {
        problems = new List<string>();
        if (deck == null) { problems.Add("Deck is null."); return false; }

        int realCount = 0;
        var copies = new Dictionary<Card, int>();
        var alreadyReported = new HashSet<Card>();

        if (deck.cards != null)
        {
            foreach (var card in deck.cards)
            {
                if (card == null) { problems.Add("Deck contains an empty card slot."); continue; }
                realCount++;

                copies.TryGetValue(card, out int seen);
                copies[card] = seen + 1;

                // One line per offending card, however many copies are in there.
                if (!MeetsAttributeRequirement(deck, card) && alreadyReported.Add(card))
                    problems.Add($"'{card.cardName}' requires STR {card.strRequired}/INT {card.intRequired}/WIS {card.wisRequired}/DEX {card.dexRequired}, which the deck's attributes don't meet.");
            }
        }

        foreach (var pair in copies)
            if (pair.Value > MaxCopiesPerCard)
                problems.Add($"'{pair.Key.cardName}' appears {pair.Value} times; the limit is {MaxCopiesPerCard}.");

        if (realCount < MinDeckSize)
            problems.Add($"Deck has {realCount} cards; minimum is {MinDeckSize}.");

        int spent = SpentAttributePoints(deck);
        if (spent > AttributePointBudget)
            problems.Add($"Deck spends {spent} attribute points; the budget is {AttributePointBudget}.");

        return problems.Count == 0;
    }
}
