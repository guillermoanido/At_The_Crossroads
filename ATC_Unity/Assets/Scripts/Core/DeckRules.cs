using System.Collections.Generic;

public static class DeckRules
{
    public const int MinDeckSize = 40;

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
        if (deck.cards != null)
        {
            foreach (var card in deck.cards)
            {
                if (card == null) { problems.Add("Deck contains an empty card slot."); continue; }
                realCount++;
                if (!MeetsAttributeRequirement(deck, card))
                    problems.Add($"'{card.cardName}' requires STR {card.strRequired}/INT {card.intRequired}/WIS {card.wisRequired}/DEX {card.dexRequired}, which the deck's attributes don't meet.");
            }
        }

        if (realCount < MinDeckSize)
            problems.Add($"Deck has {realCount} cards; minimum is {MinDeckSize}.");

        return problems.Count == 0;
    }
}
