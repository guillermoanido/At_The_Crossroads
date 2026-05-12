using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

        RebuildList();
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
        ClearSpawned();
        if (root != null) root.SetActive(false);
        activeDeck = null;
        selectedIndex = -1;
    }

    public void MoveLeft()
    {
        if (selectedIndex <= 0) return;
        Swap(selectedIndex, selectedIndex - 1);
        selectedIndex--;
        RebuildList();
    }

    public void MoveRight()
    {
        if (selectedIndex < 0 || selectedIndex >= currentOrder.Count - 1) return;
        Swap(selectedIndex, selectedIndex + 1);
        selectedIndex++;
        RebuildList();
    }

    public void SelectIndex(int index)
    {
        if (index < 0 || index >= currentOrder.Count) return;
        selectedIndex = index;
        ApplySelectionHighlight();
    }

    private void Swap(int a, int b)
        => (currentOrder[a], currentOrder[b]) = (currentOrder[b], currentOrder[a]);

    private void RebuildList()
    {
        ClearSpawned();
        for (int i = 0; i < currentOrder.Count; i++)
            spawned.Add(SpawnEntry(i, currentOrder[i]));
        ApplySelectionHighlight();
    }

    private GameObject SpawnEntry(int index, Card card)
    {
        var clone = Instantiate(cardPrefab, listContainer);
        clone.transform.localScale = Vector3.one * cardScale;

        var display = clone.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardData = card;
            display.SetFaceUp(true);
        }

        DisableGameplayInteractions(clone);
        clone.AddComponent<ScryEntryClick>().Init(this, index);
        return clone;
    }

    private static void DisableGameplayInteractions(GameObject clone)
    {
        var move = clone.GetComponent<CardMovement>();   if (move != null) move.enabled = false;
        var drag = clone.GetComponent<DragUIObject>();   if (drag != null) drag.enabled = false;
        var actions = clone.GetComponent<CardBoardActions>(); if (actions != null) actions.enabled = false;
    }

    private void ApplySelectionHighlight()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            float scale = (i == selectedIndex) ? cardScale * selectedScaleBoost : cardScale;
            spawned[i].transform.localScale = Vector3.one * scale;
        }
    }

    private void ClearSpawned()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}

public class ScryEntryClick : MonoBehaviour, IPointerClickHandler
{
    private ScryPanel panel;
    private int index;

    public void Init(ScryPanel owningPanel, int entryIndex)
    {
        panel = owningPanel;
        index = entryIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (panel != null) panel.SelectIndex(index);
    }
}

public class ScryButton : MonoBehaviour
{
    [SerializeField] private DeckManager deck;
    [SerializeField] private int count = 3;

    public void Trigger()
    {
        if (ScryPanel.Instance != null) ScryPanel.Instance.Open(deck, count);
    }
}
