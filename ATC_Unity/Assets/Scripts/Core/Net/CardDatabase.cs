using System.Collections.Generic;
using UnityEngine;

/// A build-included, deterministic registry of every Card asset so the network layer can refer
/// to a card by a small, stable integer id that resolves to the SAME asset on the host and the
/// client. Cards are ScriptableObjects that aren't NetworkIdentities, so they can't be synced
/// directly — we sync ids and look the asset back up here.
///
/// Lives in a Resources folder so it (and everything it references) is included in the build and
/// loadable at runtime on both machines. Rebuild via the "ATC/Build Card Database" menu whenever
/// cards are added or removed.
[CreateAssetMenu(fileName = "CardDatabase", menuName = "ATC/Card Database")]
public class CardDatabase : ScriptableObject
{
    [Tooltip("Every card in the game, in a fixed order. The index is the network id. " +
             "Rebuild via ATC/Build Card Database — do not reorder by hand.")]
    public List<Card> cards = new List<Card>();

    private static CardDatabase instance;
    private Dictionary<Card, int> idByCard;

    public static CardDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<CardDatabase>("CardDatabase");
                if (instance == null)
                    Debug.LogError("[CardDatabase] No Resources/CardDatabase.asset found — run " +
                                   "the ATC → Build Card Database menu once so hands can sync.");
            }
            return instance;
        }
    }

    // Card -> id. Returns -1 for an unknown/null card.
    public int Id(Card card)
    {
        if (card == null) return -1;
        BuildIndex();
        return idByCard.TryGetValue(card, out int id) ? id : -1;
    }

    // id -> Card. Returns null for an out-of-range id (e.g. -1).
    public Card FromId(int id) => id >= 0 && id < cards.Count ? cards[id] : null;

    private void BuildIndex()
    {
        if (idByCard != null) return;
        idByCard = new Dictionary<Card, int>();
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null && !idByCard.ContainsKey(cards[i]))
                idByCard[cards[i]] = i;
    }
}
