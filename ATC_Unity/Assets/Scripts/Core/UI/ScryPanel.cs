using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ScryPanel : MonoBehaviour
{
    public static ScryPanel Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("Fallback size used only if there is no GameManager; normally driven by GameManager.popupCardScale so all popups share one control.")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float cardScale = 0.6f;

    [Tooltip("Multiplier applied to the selected entry's scale so it's visually distinguished.")]
    [SerializeField] private float selectedScaleBoost = 1.15f;

    [Tooltip("Optional in-panel slider to resize the scry cards live. Auto-wired on Awake.")]
    [SerializeField] private Slider scaleSlider;

    private float Scale => GameManager.Instance != null ? GameManager.Instance.popupCardScale : cardScale;

    [Header("Order Number")]
    [Tooltip("Font size of the '1, 2, 3…' order badge stamped on each scry card, in the card's own canvas units.")]
    [SerializeField] private float orderLabelFontSize = 120f;
    [SerializeField] private Color orderLabelColor = new Color(1f, 0.95f, 0.4f);

    private DeckManager activeDeck;
    private readonly List<Card> currentOrder = new List<Card>();
    private readonly List<GameObject> spawned = new List<GameObject>();
    private int selectedIndex = -1;

    private void Awake()
    {
        Instance = this;
        WireScaleSlider();
        Close();
    }

    private void Update()
    {
        if (spawned.Count > 0) ApplySelectionHighlight();
    }

    private void WireScaleSlider()
    {
        if (scaleSlider == null) return;
        scaleSlider.minValue = 0.2f;
        scaleSlider.maxValue = 1.5f;
        scaleSlider.SetValueWithoutNotify(Scale);
        scaleSlider.onValueChanged.AddListener(SetCardScale);
    }

    public void SetCardScale(float value)
    {
        value = Mathf.Clamp(value, 0.2f, 1.5f);
        if (GameManager.Instance != null) GameManager.Instance.popupCardScale = value;
        else cardScale = value;
        ApplySelectionHighlight();
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
        clone.transform.localScale = Vector3.one * Scale;

        var display = clone.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardData = card;
            display.SetFaceUp(true);
        }

        AddOrderLabel(clone, index + 1, display);
        DisableGameplayInteractions(clone);
        clone.AddComponent<ScryEntryClick>().Init(this, index);
        return clone;
    }

    private void AddOrderLabel(GameObject clone, int order, CardDisplay display)
    {
        var canvas = clone.GetComponentInChildren<Canvas>(true);
        Transform parent = canvas != null ? canvas.transform : clone.transform;

        var labelGO = new GameObject($"Order {order}", typeof(RectTransform));
        var rect = labelGO.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(240f, 240f);
        rect.localScale = Vector3.one;

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = order.ToString();
        label.fontSize = orderLabelFontSize;
        label.color = orderLabelColor;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        if (display != null && display.cardNameText != null)
            label.font = display.cardNameText.font;

        labelGO.transform.SetAsLastSibling();
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
            float scale = (i == selectedIndex) ? Scale * selectedScaleBoost : Scale;
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
