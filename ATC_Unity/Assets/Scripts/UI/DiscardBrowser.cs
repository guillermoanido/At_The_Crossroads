using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Singleton popup. Open(zone) instantiates a clone of every card in the zone
// (most-recent first) into listContainer. Set up listContainer with whatever
// Layout Group you prefer (Grid or Horizontal). Use the Close button to dismiss.
public class DiscardBrowser : MonoBehaviour
{
    public static DiscardBrowser Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject cardPrefab;
    [Range(0.2f, 1.5f)]
    [SerializeField] private float cardScale = 0.6f;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        Close();
    }

    public void Open(CardZone zone)
    {
        Clear();

        for (int i = zone.Cards.Count - 1; i >= 0; i--)
        {
            var src = zone.Cards[i];
            if (src == null) continue;
            var srcDisplay = src.GetComponent<CardDisplay>();
            if (srcDisplay == null || srcDisplay.cardData == null) continue;

            var clone = Instantiate(cardPrefab, listContainer);
            clone.transform.localScale = Vector3.one * cardScale;

            var display = clone.GetComponent<CardDisplay>();
            if (display != null)
            {
                display.cardData = srcDisplay.cardData;
                display.SetFaceUp(true);
            }

            // Disable interactions on browse-only clones.
            var move = clone.GetComponent<CardMovement>(); if (move != null) move.enabled = false;
            var drag = clone.GetComponent<DragUIObject>(); if (drag != null) drag.enabled = false;
            var actions = clone.GetComponent<CardBoardActions>(); if (actions != null) actions.enabled = false;
            // CardPreviewTrigger stays enabled so hovering a list entry still pops the big preview.

            // Right-click an entry to return its source card to hand.
            var entry = clone.AddComponent<DiscardBrowserEntry>();
            entry.Init(this, zone, src);

            spawned.Add(clone);
        }

        if (root != null) root.SetActive(true);
    }

    public void Close()
    {
        Clear();
        if (root != null) root.SetActive(false);
    }

    private void Clear()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}

// Component added to each browser entry. Right-click → return source card to hand.
public class DiscardBrowserEntry : MonoBehaviour, IPointerClickHandler
{
    private DiscardBrowser browser;
    private CardZone source;
    private GameObject sourceCard;

    public void Init(DiscardBrowser b, CardZone z, GameObject src)
    {
        browser = b;
        source = z;
        sourceCard = src;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (source == null || source.Owner == null || sourceCard == null) return;

        source.Owner.ReturnToHand(sourceCard);
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
        if (browser != null) browser.Close();
    }
}
