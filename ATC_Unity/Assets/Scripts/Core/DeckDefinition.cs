using System.Collections.Generic;
using UnityEngine;

// A built deck: the chosen cards plus the attribute allocation that gates which cards are legal.
// Deck-building UI (later) edits this asset; DeckRules validates it; DeckManager plays from it.
[CreateAssetMenu(fileName = "New Deck", menuName = "Deck")]
public class DeckDefinition : ScriptableObject
{
    public string deckName;

    [Header("Attribute allocation — a card is only legal if the deck meets its attribute requirements")]
    public int strength;
    public int intellect;
    public int wisdom;
    public int dexterity;

    [Header("Cards")]
    public List<Card> cards = new List<Card>();

    public int Count => cards != null ? cards.Count : 0;
}
