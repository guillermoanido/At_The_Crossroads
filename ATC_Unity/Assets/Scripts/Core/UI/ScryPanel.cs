using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// The Scry keyword: look at the top X cards of your deck, bin any number of them, and the rest go
/// back on top in the same order you saw them. There is no reordering — clicking a card only toggles
/// whether it is being thrown away.
public class ScryPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("Base size of scry cards. The GameManager's Popup Card Scale multiplies this, so all popups share one live control while keeping their own baseline.")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float cardScale = 0.6f;

    [Tooltip("Scale multiplier applied to a card marked for the discard pile, so it visibly shrinks away.")]
    [SerializeField] private float discardedScale = 0.8f;

    [Tooltip("Tint applied to a card marked for the discard pile.")]
    [SerializeField] private Color discardedTint = new Color(0.45f, 0.45f, 0.5f);

    [Tooltip("Optional in-panel slider driving the shared Popup Card Scale (affects every popup). Auto-wired on Awake.")]
    [SerializeField] private Slider scaleSlider;

    private static float PopupMultiplier => GameManager.Instance != null ? GameManager.Instance.popupCardScale : 1f;
    private float Scale => cardScale * PopupMultiplier;

    [Header("Order Number")]
    [Tooltip("Font size of the '1, 2, 3…' order badge stamped on each scry card, in the card's own canvas units.")]
    [SerializeField] private float orderLabelFontSize = 120f;
    [SerializeField] private Color orderLabelColor = new Color(1f, 0.95f, 0.4f);

    private Player owner;
    private System.Action<int[]> remoteAnswer;
    private readonly List<Card> revealed = new List<Card>();
    private readonly HashSet<int> markedForDiscard = new HashSet<int>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        WireScaleSlider();
        Close();
    }

    private void Update()
    {
        if (spawned.Count > 0) ApplyEntryLooks();
    }

    private void WireScaleSlider()
    {
        if (scaleSlider == null) return;
        scaleSlider.minValue = 0.2f;
        scaleSlider.maxValue = 1.5f;
        scaleSlider.SetValueWithoutNotify(PopupMultiplier);
        scaleSlider.onValueChanged.AddListener(SetCardScale);
    }

    public void SetCardScale(float value)
    {
        value = Mathf.Clamp(value, 0.2f, 1.5f);
        if (GameManager.Instance != null) GameManager.Instance.popupCardScale = value;
        else cardScale = value;
        ApplyEntryLooks();
    }

    /// Scry driven from the host: the cards were sent to us, and our answer goes back over the wire
    /// rather than touching a deck this machine does not own.
    public void OpenRemote(List<Card> cards, System.Action<int[]> onConfirmed)
    {
        if (cards == null || cards.Count == 0) { onConfirmed?.Invoke(new int[0]); return; }

        owner = null;                 // the deck lives on the host
        remoteAnswer = onConfirmed;
        revealed.Clear();
        revealed.AddRange(cards);
        markedForDiscard.Clear();

        RebuildList();
        if (root != null) root.SetActive(true);
        Debug.Log($"[Scry] Looking at the top {revealed.Count} card(s) — click any to discard them.");
    }

    public void Open(Player scryingPlayer, int count)
    {
        var deck = scryingPlayer != null ? scryingPlayer.deckManager : null;
        if (deck == null || count <= 0) return;

        owner = scryingPlayer;
        revealed.Clear();
        revealed.AddRange(deck.PeekTop(count));
        markedForDiscard.Clear();

        RebuildList();
        if (root != null) root.SetActive(true);
        Debug.Log($"[Scry] {owner.name} looks at the top {revealed.Count} card(s) — click any to discard them.");
    }

    /// Marked cards go to the discard pile; everything else returns to the top of the deck in the
    /// order it was revealed.
    public void Confirm()
    {
        var kept = new List<Card>();
        var binned = new List<Card>();

        for (int i = 0; i < revealed.Count; i++)
        {
            if (markedForDiscard.Contains(i)) binned.Add(revealed[i]);
            else kept.Add(revealed[i]);
        }

        if (remoteAnswer != null)
        {
            // The deck is on the host: send back which cards to bin and let it do the work.
            var indices = new List<int>();
            for (int i = 0; i < revealed.Count; i++)
                if (markedForDiscard.Contains(i)) indices.Add(i);

            var answer = remoteAnswer;
            remoteAnswer = null;
            answer(indices.ToArray());
        }
        else if (owner != null && owner.deckManager != null)
        {
            owner.deckManager.ReplaceTop(revealed.Count, kept);
            foreach (var card in binned) owner.PutCardInDiscard(card);
        }

        Debug.Log($"[Scry] {(owner != null ? owner.name : "?")} kept {kept.Count} on top " +
                  $"and discarded {binned.Count}.");
        Close();
    }

    public void Close()
    {
        ClearSpawned();
        if (root != null) root.SetActive(false);
        revealed.Clear();
        markedForDiscard.Clear();
        owner = null;

        // Never leave the host waiting: closing without confirming counts as keeping everything.
        var pending = remoteAnswer;
        remoteAnswer = null;
        pending?.Invoke(new int[0]);
    }

    /// Clicking an entry toggles whether it is being thrown away.
    public void ToggleDiscard(int index)
    {
        if (index < 0 || index >= revealed.Count) return;
        if (!markedForDiscard.Remove(index)) markedForDiscard.Add(index);
        ApplyEntryLooks();
    }

    private void RebuildList()
    {
        ClearSpawned();
        for (int i = 0; i < revealed.Count; i++)
            spawned.Add(SpawnEntry(i, revealed[i]));
        ApplyEntryLooks();
    }

    private GameObject SpawnEntry(int index, Card card)
    {
        var clone = Instantiate(cardPrefab, listContainer);

        var display = clone.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardData = card;
            display.SetFaceUp(true);
        }

        AddOrderLabel(clone, index + 1, display);
        CardDisplay.DisableGameplayInteractions(clone);
        clone.AddComponent<ScryEntryClick>().Init(this, index);
        clone.AddComponent<HoverPreview>();
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

    // Cards heading for the discard pile shrink and grey out; the ones staying on top look normal,
    // numbered in the order they will be drawn.
    private void ApplyEntryLooks()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            bool binned = markedForDiscard.Contains(i);

            spawned[i].transform.localScale = Vector3.one * (binned ? Scale * discardedScale : Scale);

            var image = spawned[i].GetComponent<CardDisplay>()?.cardImage;
            if (image != null) image.color = binned ? discardedTint : Color.white;
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
        if (panel != null) panel.ToggleDiscard(index);
    }
}
