using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Saves and loads decks the player builds themselves.
///
/// Ready-made decks are ScriptableObject assets, but a deck built while the game is running cannot
/// be one — assets only exist at edit time. So a custom deck is stored as JSON (card ids from
/// CardDatabase, plus its attributes) in the player's data folder, and rebuilt into an in-memory
/// DeckDefinition on load. From there it is indistinguishable from a ready-made deck: the menu
/// lists it, and NetworkPlayerSeat sends it to the host as the same array of card ids.
public static class CustomDeckStore
{
    private const string FolderName = "Decks";
    private const string Extension = ".deck.json";

    [System.Serializable]
    private class DeckSaveData
    {
        public string deckName;
        public int strength, intellect, wisdom, dexterity;
        public int[] cardIds;
    }

    private static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

    /// Every custom deck on this machine, rebuilt as usable DeckDefinitions.
    public static List<DeckDefinition> LoadAll()
    {
        var decks = new List<DeckDefinition>();
        if (!Directory.Exists(Folder)) return decks;

        foreach (var file in Directory.GetFiles(Folder, "*" + Extension))
        {
            var deck = Load(file);
            if (deck != null) decks.Add(deck);
        }
        return decks;
    }

    public static void Save(DeckDefinition deck)
    {
        var db = CardDatabase.Instance;
        if (deck == null || db == null) return;

        Directory.CreateDirectory(Folder);

        var data = new DeckSaveData
        {
            deckName = deck.deckName,
            strength = deck.strength,
            intellect = deck.intellect,
            wisdom = deck.wisdom,
            dexterity = deck.dexterity,
            cardIds = new int[deck.Count],
        };
        for (int i = 0; i < data.cardIds.Length; i++) data.cardIds[i] = db.Id(deck.cards[i]);

        File.WriteAllText(PathFor(deck.deckName), JsonUtility.ToJson(data, true));
        Debug.Log($"[Decks] Saved custom deck '{deck.deckName}' ({deck.Count} cards).");
    }

    public static void Delete(string deckName)
    {
        string path = PathFor(deckName);
        if (File.Exists(path)) File.Delete(path);
    }

    private static DeckDefinition Load(string file)
    {
        var db = CardDatabase.Instance;
        if (db == null) return null;

        DeckSaveData data;
        try { data = JsonUtility.FromJson<DeckSaveData>(File.ReadAllText(file)); }
        catch (IOException e) { Debug.LogWarning($"[Decks] Couldn't read {file}: {e.Message}"); return null; }
        if (data == null) return null;

        var deck = ScriptableObject.CreateInstance<DeckDefinition>();
        deck.name = Path.GetFileName(file).Replace(Extension, string.Empty);
        deck.deckName = data.deckName;
        deck.strength = data.strength;
        deck.intellect = data.intellect;
        deck.wisdom = data.wisdom;
        deck.dexterity = data.dexterity;
        deck.cards = new List<Card>();

        foreach (int id in data.cardIds)
        {
            var card = db.FromId(id);
            if (card != null) deck.cards.Add(card);
        }
        return deck;
    }

    private static string PathFor(string deckName)
    {
        string safe = string.Join("_", deckName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(Folder, safe + Extension);
    }
}
