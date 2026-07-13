using System.Collections.Generic;
using UnityEngine;

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
