#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Builds one ready-made deck per class into Resources/Decks, so the menu has something to offer
/// before the deck builder exists. Each deck is filled to the minimum legal size by repeating its
/// class's cards, and its attributes are set high enough that every card in it is legal.
///
/// Re-runnable: existing deck assets are updated in place, so a deck the player has selected keeps
/// working. Custom decks the player builds later live in the same folder and are left alone.
public static class StarterDeckGenerator
{
    private const string Folder = "Assets/Resources/Decks";

    [MenuItem("ATC/Generate Starter Decks")]
    public static void Generate()
    {
        EnsureFolder();

        int made = 0;
        made += Build("Starter Warrior", "The Bulwark", c => c.strRequired > 0);
        made += Build("Starter Rogue",   "Quick Hands", c => c.dexRequired > 0);
        made += Build("Starter Mage",    "Arcane Study", c => c.intRequired > 0);
        made += Build("Starter Cleric",  "The Faithful", c => c.wisRequired > 0);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Decks] {made} starter deck(s) written to {Folder}. " +
                  "They appear in the main menu automatically.");
    }

    private static int Build(string assetName, string displayName, System.Predicate<Card> belongs)
    {
        var pool = CardsMatching(belongs);
        if (pool.Count == 0)
        {
            Debug.LogWarning($"[Decks] No cards found for '{displayName}' — run ATC ▸ Generate Card Library first.");
            return 0;
        }

        string path = $"{Folder}/{assetName}.asset";
        var deck = AssetDatabase.LoadAssetAtPath<DeckDefinition>(path);
        bool isNew = deck == null;
        if (isNew) deck = ScriptableObject.CreateInstance<DeckDefinition>();

        deck.deckName = displayName;
        deck.cards = FillToLegalSize(pool);
        SetAttributesToCover(deck);

        if (isNew) AssetDatabase.CreateAsset(deck, path);
        else EditorUtility.SetDirty(deck);
        return 1;
    }

    private static List<Card> CardsMatching(System.Predicate<Card> belongs)
    {
        var pool = new List<Card>();
        foreach (var guid in AssetDatabase.FindAssets("t:Card", new[] { "Assets/Card Data" }))
        {
            var card = AssetDatabase.LoadAssetAtPath<Card>(AssetDatabase.GUIDToAssetPath(guid));
            if (card != null && belongs(card)) pool.Add(card);
        }
        pool.Sort((a, b) => string.CompareOrdinal(a.cardName, b.cardName));
        return pool;
    }

    // Cycle through the class's cards until the deck is legal, so every card appears a similar
    // number of times rather than stacking duplicates of whatever came first.
    private static List<Card> FillToLegalSize(List<Card> pool)
    {
        var cards = new List<Card>();
        for (int i = 0; cards.Count < DeckRules.MinDeckSize; i++)
            cards.Add(pool[i % pool.Count]);
        return cards;
    }

    // A starter deck should never be illegal, so give it exactly the attributes its cards demand.
    private static void SetAttributesToCover(DeckDefinition deck)
    {
        int str = 0, intel = 0, wis = 0, dex = 0;
        foreach (var card in deck.cards)
        {
            if (card == null) continue;
            str   = Mathf.Max(str, card.strRequired);
            intel = Mathf.Max(intel, card.intRequired);
            wis   = Mathf.Max(wis, card.wisRequired);
            dex   = Mathf.Max(dex, card.dexRequired);
        }
        deck.strength = str;
        deck.intellect = intel;
        deck.wisdom = wis;
        deck.dexterity = dex;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources", "Decks");
    }
}
#endif
