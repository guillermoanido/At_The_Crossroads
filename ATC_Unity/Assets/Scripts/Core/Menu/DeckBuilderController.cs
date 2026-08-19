using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// The deck builder: every card in the game on the left, the deck you are assembling on the right.
///
/// Left-click a card in the catalogue to add a copy, click a row in the deck list to remove one.
/// The deck is saved through CustomDeckStore (JSON in the player's data folder), because a deck
/// built while the game is running cannot be a ScriptableObject asset.
public class DeckBuilderController : MonoBehaviour
{
    // Appended, not reordered — the filter buttons pass these as ints from the Inspector.
    public enum Filter { All, Strength, Dexterity, Intellect, Wisdom, Mix, Colorless }

    [Header("Scenes")]
    [SerializeField] private string menuScene = "Main Menu";

    [Header("Catalogue")]
    [SerializeField] private Transform catalogueContainer;
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("Size of the card faces in the catalogue grid.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float catalogueCardScale = 0.42f;

    [Header("Deck")]
    [SerializeField] private Transform deckListContainer;
    [SerializeField] private GameObject deckRowPrefab;
    [SerializeField] private TMP_InputField deckNameField;
    [SerializeField] private TMP_Text deckCountLabel;
    [SerializeField] private TMP_Text validationLabel;

    [Header("Attributes")]
    [SerializeField] private TMP_Text attributeBudgetLabel;
    [SerializeField] private TMP_Text strengthLabel;
    [SerializeField] private TMP_Text dexterityLabel;
    [SerializeField] private TMP_Text intellectLabel;
    [SerializeField] private TMP_Text wisdomLabel;

    private readonly List<Card> deck = new List<Card>();
    private readonly List<GameObject> catalogueEntries = new List<GameObject>();
    private readonly List<GameObject> deckRows = new List<GameObject>();

    private int strength, dexterity, intellect, wisdom;
    private Filter filter = Filter.All;

    private void Start()
    {
        LoadStartingDeck();
        BuildCatalogue();
        RefreshEverything();
    }

    #region Catalogue

    // Every card in the game comes from CardDatabase — it already exists for networking and is the
    // one list guaranteed to be complete and identical on every machine.
    private void BuildCatalogue()
    {
        ClearSpawned(catalogueEntries);

        var db = CardDatabase.Instance;
        if (db == null || cardPrefab == null || catalogueContainer == null) return;

        foreach (var card in db.cards)
        {
            if (card == null || !PassesFilter(card)) continue;
            catalogueEntries.Add(CreateCatalogueEntry(card));
        }
    }

    private GameObject CreateCatalogueEntry(Card card)
    {
        var clone = Instantiate(cardPrefab, catalogueContainer);
        clone.transform.localScale = Vector3.one * catalogueCardScale;

        var display = clone.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardData = card;
            display.SetFaceUp(true);
        }

        CardDisplay.DisableGameplayInteractions(clone);
        clone.AddComponent<HoverPreview>();
        clone.AddComponent<DeckBuilderCardClick>().Init(this, card);
        return clone;
    }

    private bool PassesFilter(Card card)
    {
        switch (filter)
        {
            // An attribute filter shows everything that attribute could put in a deck, multi-class
            // cards included — you are browsing "what can a STR deck run", not "what is pure STR".
            case Filter.Strength:   return card.strRequired > 0;
            case Filter.Dexterity:  return card.dexRequired > 0;
            case Filter.Intellect:  return card.intRequired > 0;
            case Filter.Wisdom:     return card.wisRequired > 0;

            case Filter.Mix:        return card.IsMultiClass;
            case Filter.Colorless:  return card.IsColorless;
            default:                return true;
        }
    }

    /// Wired to the filter buttons. Takes an int so it can be set from the Inspector.
    public void SetFilter(int filterIndex)
    {
        filter = (Filter)Mathf.Clamp(filterIndex, 0, (int)Filter.Colorless);
        BuildCatalogue();
    }

    #endregion

    #region Building the deck

    public void AddCard(Card card)
    {
        if (card == null) return;

        if (CopiesOf(card) >= DeckRules.MaxCopiesPerCard)
        {
            Flash($"{card.cardName}: already at the {DeckRules.MaxCopiesPerCard}-copy limit.");
            return;
        }

        deck.Add(card);
        RefreshEverything();
    }

    public void RemoveCard(Card card)
    {
        if (card == null) return;
        if (deck.Remove(card)) RefreshEverything();
    }

    private int CopiesOf(Card card)
    {
        int count = 0;
        foreach (var c in deck)
            if (c == card) count++;
        return count;
    }

    // Distinct cards in the order they were first added, each with its copy count.
    private List<KeyValuePair<Card, int>> DeckByCard()
    {
        var ordered = new List<Card>();
        var counts = new Dictionary<Card, int>();

        foreach (var card in deck)
        {
            if (!counts.ContainsKey(card)) { counts[card] = 0; ordered.Add(card); }
            counts[card]++;
        }

        var result = new List<KeyValuePair<Card, int>>();
        foreach (var card in ordered) result.Add(new KeyValuePair<Card, int>(card, counts[card]));
        return result;
    }

    #endregion

    #region Attributes

    public void AddStrength(int delta)  => AdjustAttribute(ref strength, delta);
    public void AddDexterity(int delta) => AdjustAttribute(ref dexterity, delta);
    public void AddIntellect(int delta) => AdjustAttribute(ref intellect, delta);
    public void AddWisdom(int delta)    => AdjustAttribute(ref wisdom, delta);

    private void AdjustAttribute(ref int attribute, int delta)
    {
        int next = Mathf.Max(0, attribute + delta);
        int wouldSpend = SpentPoints() - attribute + next;

        if (wouldSpend > DeckRules.AttributePointBudget)
        {
            Flash($"Only {DeckRules.AttributePointBudget} attribute points to spend.");
            return;
        }

        attribute = next;
        RefreshEverything();
    }

    private int SpentPoints() => strength + dexterity + intellect + wisdom;

    #endregion

    #region Saving

    public void Save()
    {
        var definition = BuildDefinition();

        if (!DeckRules.Validate(definition, out var problems))
        {
            ShowProblems(problems);
            Debug.LogWarning($"[DeckBuilder] '{definition.deckName}' saved with {problems.Count} problem(s) — " +
                             "it won't be playable until they're fixed.");
        }

        CustomDeckStore.Save(definition);
        DeckLibrary.Reload();
        MatchSettings.SelectDeck(DeckLibrary.ByName(definition.name) ?? definition);
        Flash($"Saved '{definition.deckName}'.");
    }

    public void BackToMenu() => SceneManager.LoadScene(menuScene);

    private DeckDefinition BuildDefinition()
    {
        var definition = ScriptableObject.CreateInstance<DeckDefinition>();
        definition.deckName = DeckName();
        definition.name = definition.deckName;
        definition.strength = strength;
        definition.dexterity = dexterity;
        definition.intellect = intellect;
        definition.wisdom = wisdom;
        definition.cards = new List<Card>(deck);
        return definition;
    }

    private string DeckName()
    {
        string typed = deckNameField != null ? deckNameField.text : null;
        return string.IsNullOrWhiteSpace(typed) ? "My Deck" : typed.Trim();
    }

    // Pick up where the player left off if they came in with a deck selected.
    private void LoadStartingDeck()
    {
        var source = MatchSettings.SelectedDeck;
        if (source == null) return;

        deck.AddRange(source.cards);
        strength = source.strength;
        dexterity = source.dexterity;
        intellect = source.intellect;
        wisdom = source.wisdom;

        if (deckNameField != null) deckNameField.text = DeckLibrary.DisplayName(source);
    }

    #endregion

    #region Display

    private void RefreshEverything()
    {
        RebuildDeckList();
        RefreshCounters();
        RefreshValidation();
    }

    private void RebuildDeckList()
    {
        ClearSpawned(deckRows);
        if (deckListContainer == null || deckRowPrefab == null) return;

        foreach (var entry in DeckByCard())
        {
            var row = Instantiate(deckRowPrefab, deckListContainer);
            row.SetActive(true);

            var label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = $"{entry.Value}x  {entry.Key.cardName}";

            row.AddComponent<DeckBuilderCardClick>().Init(this, entry.Key, removes: true);
            deckRows.Add(row);
        }
    }

    private void RefreshCounters()
    {
        if (deckCountLabel != null)
        {
            bool enough = deck.Count >= DeckRules.MinDeckSize;
            deckCountLabel.text = $"{deck.Count} / {DeckRules.MinDeckSize} cards";
            deckCountLabel.color = enough ? Color.white : new Color(1f, 0.6f, 0.4f);
        }

        if (attributeBudgetLabel != null)
            attributeBudgetLabel.text = $"{SpentPoints()} / {DeckRules.AttributePointBudget} points";

        SetAttributeLabel(strengthLabel, "STR", strength);
        SetAttributeLabel(dexterityLabel, "DEX", dexterity);
        SetAttributeLabel(intellectLabel, "INT", intellect);
        SetAttributeLabel(wisdomLabel, "WIS", wisdom);
    }

    private static void SetAttributeLabel(TMP_Text label, string prefix, int value)
    {
        if (label != null) label.text = $"{prefix}  {value}";
    }

    private void RefreshValidation()
    {
        DeckRules.Validate(BuildDefinition(), out var problems);
        ShowProblems(problems);
    }

    private void ShowProblems(List<string> problems)
    {
        if (validationLabel == null) return;

        if (problems == null || problems.Count == 0)
        {
            validationLabel.text = "Deck is legal.";
            validationLabel.color = new Color(0.6f, 0.9f, 0.6f);
            return;
        }

        // Only the first few, so one bad attribute spread doesn't bury the panel.
        int shown = Mathf.Min(problems.Count, 4);
        var lines = problems.GetRange(0, shown);
        if (problems.Count > shown) lines.Add($"…and {problems.Count - shown} more");

        validationLabel.text = string.Join("\n", lines);
        validationLabel.color = new Color(1f, 0.65f, 0.45f);
    }

    private void Flash(string message)
    {
        Debug.Log($"[DeckBuilder] {message}");
        if (validationLabel != null)
        {
            validationLabel.text = message;
            validationLabel.color = new Color(1f, 0.85f, 0.45f);
        }
    }

    private static void ClearSpawned(List<GameObject> spawned)
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }

    #endregion
}

/// Click handler shared by catalogue cards (adds a copy) and deck rows (removes one).
public class DeckBuilderCardClick : MonoBehaviour, IPointerClickHandler
{
    private DeckBuilderController builder;
    private Card card;
    private bool removes;

    public void Init(DeckBuilderController owner, Card cardData, bool removes = false)
    {
        builder = owner;
        card = cardData;
        this.removes = removes;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (builder == null || card == null) return;

        // Right-click always removes, so a catalogue card can undo itself without hunting the list.
        bool remove = removes || eventData.button == PointerEventData.InputButton.Right;
        if (remove) builder.RemoveCard(card);
        else builder.AddCard(card);
    }
}
