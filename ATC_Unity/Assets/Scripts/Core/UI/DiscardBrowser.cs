using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DiscardBrowser : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("Base size of browsed cards. The GameManager's Popup Card Scale multiplies this, so all popups share one live control while keeping their own baseline.")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float cardScale = 1f;

    [Tooltip("Optional in-panel slider driving the shared Popup Card Scale (affects every popup). Auto-wired on Awake.")]
    [SerializeField] private Slider scaleSlider;

    private static float PopupMultiplier => GameManager.Instance != null ? GameManager.Instance.popupCardScale : 1f;
    private float Scale => cardScale * PopupMultiplier;

    [Header("Grid (applied to a GridLayoutGroup on List Container if present)")]
    [SerializeField] private Vector2 cellSize = new Vector2(180f, 252f);
    [SerializeField] private Vector2 spacing = new Vector2(10f, 10f);

    private readonly List<GameObject> spawned = new List<GameObject>();
    private CardZone currentZone;
    private const int UnknownCount = -1;
    private int lastCardCount = UnknownCount;

    private void Awake()
    {
        WireScaleSlider();
        Close();
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
        ApplyCardScaleToSpawned();
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
        lastCardCount = UnknownCount;
        ConfigureGridLayoutIfPresent();
        Rebuild();
        if (root != null) root.SetActive(true);
    }

    public void Close()
    {
        Clear();
        if (root != null) root.SetActive(false);
        currentZone = null;
        lastCardCount = UnknownCount;
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
        Vector3 target = Vector3.one * Scale;
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
        clone.transform.localScale = Vector3.one * Scale;

        var display = clone.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.cardData = data;
            display.SetFaceUp(true);
        }

        CardDisplay.DisableGameplayInteractions(clone);
        clone.AddComponent<HoverPreview>();
        return clone;
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
