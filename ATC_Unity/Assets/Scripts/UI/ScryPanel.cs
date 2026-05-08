using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Singleton popup. ScryButton.Trigger() opens this with a deck and count;
// the player clicks an entry to select it, uses Move Left / Move Right to
// reorder, then Confirm to write the new top-of-deck order back.
public class ScryPanel : MonoBehaviour
{
    public static ScryPanel Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject cardPrefab;
    [Range(0.2f, 1.5f)]
    [SerializeField] private float cardScale = 0.6f;
    [Tooltip("Multiplier applied to the selected entry's scale so it's visually distinguished.")]
    [SerializeField] private float selectedScaleBoost = 1.15f;

    private DeckManager activeDeck;
    private readonly List<Card> currentOrder = new List<Card>();
    private readonly List<GameObject> spawned = new List<GameObject>();
    private int selectedIndex = -1;

    private void Awake()
    {
        Instance = this;
        Close();
    }

    public void Open(DeckManager deck, int count)
    {
        if (deck == null || count <= 0) return;
        activeDeck = deck;
        currentOrder.Clear();
        currentOrder.AddRange(deck.PeekTop(count));
        selectedIndex = currentOrder.Count > 0 ? 0 : -1;
        Refresh();
        if (root != null) root.SetActive(true);
        Debug.Log($"[Scry] {deck.name} → showing top {currentOrder.Count}.");
    }

    public void Confirm()
    {
        if (activeDeck != null) activeDeck.SetTopOrder(currentOrder);
        Debug.Log($"[Scry] Order applied to {(activeDeck != null ? activeDeck.name : "<null>")}.");
        Close();
    }

    public void Close()
    {
        Clear();
        if (root != null) root.SetActive(false);
        activeDeck = null;
        selectedIndex = -1;
    }

    public void MoveLeft()
    {
        if (selectedIndex <= 0) return;
        (currentOrder[selectedIndex - 1], currentOrder[selectedIndex]) = (currentOrder[selectedIndex], currentOrder[selectedIndex - 1]);
        selectedIndex--;
        Refresh();
    }

    public void MoveRight()
    {
        if (selectedIndex < 0 || selectedIndex >= currentOrder.Count - 1) return;
        (currentOrder[selectedIndex + 1], currentOrder[selectedIndex]) = (currentOrder[selectedIndex], currentOrder[selectedIndex + 1]);
        selectedIndex++;
        Refresh();
    }

    public void SelectIndex(int index)
    {
        if (index < 0 || index >= currentOrder.Count) return;
        selectedIndex = index;
        ApplySelectionVisuals();
    }

    private void Refresh()
    {
        Clear();
        for (int i = 0; i < currentOrder.Count; i++)
        {
            var clone = Instantiate(cardPrefab, listContainer);
            clone.transform.localScale = Vector3.one * cardScale;

            var display = clone.GetComponent<CardDisplay>();
            if (display != null)
            {
                display.cardData = currentOrder[i];
                display.SetFaceUp(true);
            }

            // Disable normal card interactions on the scry clones.
            var move = clone.GetComponent<CardMovement>(); if (move != null) move.enabled = false;
            var drag = clone.GetComponent<DragUIObject>(); if (drag != null) drag.enabled = false;
            var actions = clone.GetComponent<CardBoardActions>(); if (actions != null) actions.enabled = false;

            var entry = clone.AddComponent<ScryEntryClick>();
            entry.Init(this, i);

            spawned.Add(clone);
        }
        ApplySelectionVisuals();
    }

    private void ApplySelectionVisuals()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            float scale = (i == selectedIndex) ? cardScale * selectedScaleBoost : cardScale;
            spawned[i].transform.localScale = Vector3.one * scale;
        }
    }

    private void Clear()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}

// Per-entry click handler — selects this entry in the panel.
public class ScryEntryClick : MonoBehaviour, IPointerClickHandler
{
    private ScryPanel panel;
    private int index;

    public void Init(ScryPanel p, int i) { panel = p; index = i; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (panel != null) panel.SelectIndex(index);
    }
}

// Attach to a UI button. Wire OnClick → ScryButton.Trigger() to open the panel
// with this deck/count combination. Multiple buttons can fire different counts.
public class ScryButton : MonoBehaviour
{
    [SerializeField] private DeckManager deck;
    [SerializeField] private int count = 3;

    public void Trigger()
    {
        if (ScryPanel.Instance != null) ScryPanel.Instance.Open(deck, count);
    }
}
