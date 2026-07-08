using System.Collections.Generic;

// The rules a deck must satisfy to be legal. Deck building enforces these before a match.
public static class DeckRules
{
    public const int MinDeckSize = 40;

    // A card is only allowed if the deck's attribute allocation meets that card's requirements.
    // (For warriors the gating attribute is Strength, for rogues Dexterity, etc.)
    public static bool MeetsAttributeRequirement(DeckDefinition deck, Card card)
    {
        if (deck == null || card == null) return false;
        return deck.strength  >= card.strRequired
            && deck.intellect >= card.intRequired
            && deck.wisdom    >= card.wisRequired
            && deck.dexterity >= card.dexRequired;
    }

    // True if the deck is legal; otherwise `problems` explains every reason it isn't.
    public static bool Validate(DeckDefinition deck, out List<string> problems)
    {
        problems = new List<string>();
        if (deck == null) { problems.Add("Deck is null."); return false; }

        if (deck.Count < MinDeckSize)
            problems.Add($"Deck has {deck.Count} cards; minimum is {MinDeckSize}.");

        if (deck.cards != null)
        {
            foreach (var card in deck.cards)
            {
                if (card == null) { problems.Add("Deck contains an empty card slot."); continue; }
                if (!MeetsAttributeRequirement(deck, card))
                    problems.Add($"'{card.cardName}' requires STR {card.strRequired}/INT {card.intRequired}/WIS {card.wisRequired}/DEX {card.dexRequired}, which the deck's attributes don't meet.");
            }
        }

        return problems.Count == 0;
    }
}
