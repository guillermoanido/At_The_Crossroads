using System.Collections.Generic;
using UnityEngine;

/// Every deck the player can choose from. Ready-made decks live in Resources/Decks so they ship in
/// the build and can be looked up by name on either machine; decks the player builds themselves
/// will be saved alongside them later.
public static class DeckLibrary
{
    public const string ResourceFolder = "Decks";

    private static List<DeckDefinition> decks;

    public static IReadOnlyList<DeckDefinition> All
    {
        get
        {
            if (decks == null) Reload();
            return decks;
        }
    }

    public static void Reload()
    {
        // Ready-made decks ship as assets; decks the player built are rebuilt from their save files.
        decks = new List<DeckDefinition>(Resources.LoadAll<DeckDefinition>(ResourceFolder));
        decks.AddRange(CustomDeckStore.LoadAll());
        decks.Sort((a, b) => string.CompareOrdinal(DisplayName(a), DisplayName(b)));

        if (decks.Count == 0)
            Debug.LogWarning($"[Decks] No decks in Resources/{ResourceFolder} — run ATC ▸ Generate Starter Decks.");
    }

    public static DeckDefinition First() => All.Count > 0 ? All[0] : null;

    public static DeckDefinition ByName(string assetName)
    {
        foreach (var deck in All)
            if (deck != null && deck.name == assetName) return deck;
        return null;
    }

    /// Index of `deck` in the library, or 0 when it isn't in there.
    public static int IndexOf(DeckDefinition deck)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i] == deck) return i;
        return 0;
    }

    public static string DisplayName(DeckDefinition deck)
    {
        if (deck == null) return Localization.T("deck.none");
        return string.IsNullOrWhiteSpace(deck.deckName) ? deck.name : deck.deckName;
    }

    /// A one-line summary for the menu: card count and the attributes it was built around.
    public static string Summary(DeckDefinition deck)
    {
        if (deck == null) return Localization.T("menu.no_deck");

        var attributes = new List<string>();
        if (deck.strength > 0)  attributes.Add($"STR {deck.strength}");
        if (deck.intellect > 0) attributes.Add($"INT {deck.intellect}");
        if (deck.wisdom > 0)    attributes.Add($"WIS {deck.wisdom}");
        if (deck.dexterity > 0) attributes.Add($"DEX {deck.dexterity}");

        string stats = attributes.Count > 0 ? string.Join("  ", attributes) : Localization.T("deck.no_attributes");
        string legal = deck.Count >= DeckRules.MinDeckSize
            ? ""
            : $"  —  {Localization.T("deck.needs")} {DeckRules.MinDeckSize}";
        return $"{deck.Count} {Localization.T("deck.cards_word")}  |  {stats}{legal}";
    }
}
