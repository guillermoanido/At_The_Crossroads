using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DiscardBrowser : MonoBehaviour
{
    public static DiscardBrowser Instance { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject cardPrefab;

    [Range(0.2f, 1.5f)]
    [SerializeField] private float cardScale = 1f;

    [Header("Grid (applied to a GridLayoutGroup on List Container if present)")]
    [SerializeField] private Vector2 cellSize = new Vector2(180f, 252f);
    [SerializeField] private Vector2 spacing = new Vector2(10f, 10f);

    private readonly List<GameObject> spawned = new List<GameObject>();
    private CardZone currentZone;
    private int lastCardCount = -1;

    private void Awake()
    {
        Instance = this;
        Close();
    }

    private void Update()
    {
        if (currentZone == null) return;

        ConfigureGridLayoutIfPresent();
        ApplyCardScaleToSpawned();

        if (currentZone.Cards.Count != lastCardCount) Rebuild();
    }

    public void Open(CardZone zone)
    {
        currentZone = zone;
        lastCardCount = -1;
        ConfigureGridLayoutIfPresent();
        Rebuild();
        if (root != null) root.SetActive(true);
    }

    public void Close()
    {
        Clear();
        if (root != null) root.SetActive(false);
        currentZone = null;
        lastCardCount = -1;
    }

    private void Rebuild()
    {
        Clear();
        if (currentZone != null)
        {
            SpawnEntriesNewestFirst(currentZone);
            lastCardCount = currentZone.Cards.Count;
        }
    }

    private void ConfigureGridLayoutIfPresent()
    {
        if (listContainer == null) return;
        var grid = listContainer.GetComponent<GridLayoutGroup>();
        if (grid == null) return;
        grid.cellSize = cellSize;
        grid.spacing = spacing;
    }

    private void ApplyCardScaleToSpawned()
    {
        Vector3 target = Vector3.one * cardScale;
        foreach (var go in spawned)
        {
            if (go == null) continue;
            if (go.transform.localScale != target) go.transform.localScale = target;
        }
    }

    private void SpawnEntriesNewestFirst(CardZone zone)
    {
        for (int i = zone.Cards.Count - 1; i >= 0; i--)
        {
            var source = zone.Cards[i];
            if (source == null) continue;
            var sourceDisplay = source.GetComponent<CardDisplay>();
            if (sourceDisplay == null || sourceDisplay.cardData == null) continue;

            var entry = SpawnEntry(sourceDisplay.cardData);
            entry.AddComponent<DiscardBrowserEntry>().Init(this, zone, source);
            spawned.Add(entry);
        }
    }

    private GameObject SpawnEntry(Card data)
    {
        var clone = Instantiate(cardPrefab, listContainer);
        clone.transform.localScale = Vector3.one * cardScale;

        var display = clone.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardData = data;
            display.SetFaceUp(true);
        }

        DisableGameplayInteractions(clone);
        return clone;
    }

    private static void DisableGameplayInteractions(GameObject clone)
    {
        var move = clone.GetComponent<CardMovement>();   if (move != null) move.enabled = false;
        var drag = clone.GetComponent<DragUIObject>();   if (drag != null) drag.enabled = false;
        var actions = clone.GetComponent<CardBoardActions>(); if (actions != null) actions.enabled = false;
    }

    private void Clear()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}

public class DiscardBrowserEntry : MonoBehaviour, IPointerClickHandler
{
    private DiscardBrowser browser;
    private CardZone source;
    private GameObject sourceCard;

    public void Init(DiscardBrowser owningBrowser, CardZone sourceZone, GameObject originalCard)
    {
        browser = owningBrowser;
        source = sourceZone;
        sourceCard = originalCard;
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
