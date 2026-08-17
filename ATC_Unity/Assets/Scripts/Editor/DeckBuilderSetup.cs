#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Builds the deck-builder screen: the card catalogue on the left, the deck you are assembling on
/// the right.
///
/// LAYOUT RULE — every element is anchored to the edge of the band it belongs to, and the scrolling
/// list simply fills whatever is left between the top and bottom bands. Nothing is positioned by an
/// offset from the panel's centre, which is what previously drew the attribute controls straight on
/// top of the deck list.
///
/// Safe to re-run: everything it makes lives under one root that is deleted and rebuilt.
public static class DeckBuilderSetup
{
    private const string ScenePath = "Assets/Scenes/DeckBuilding.unity";
    private const string CardPrefabPath = "Assets/Prefabs/CardPrefab.prefab";
    private const string RootName = "Deck Builder UI (generated)";

    // A card renders 200x300 at scale 1 (a 500x750 image inside a canvas scaled 0.4), so the grid
    // cell and the card scale have to be chosen together or the grid looks half empty.
    private const float CardScale = 0.7f;
    private static readonly Vector2 CardCell = new Vector2(150f, 220f);

    private const float Pad = 20f;

    private static readonly Color PanelColour   = new Color(0.06f, 0.06f, 0.09f, 0.96f);
    private static readonly Color SectionColour = new Color(1f, 1f, 1f, 0.045f);
    private static readonly Color ListColour    = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color ButtonColour  = new Color(0.22f, 0.22f, 0.28f);
    private static readonly Color AccentColour  = new Color(0.24f, 0.42f, 0.26f);

    [MenuItem("ATC/Setup Deck Builder Scene")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = EnsureCanvas();
        var old = canvas.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = Stretch(New(RootName, canvas.transform));
        var builder = Object.FindFirstObjectByType<DeckBuilderController>()
                      ?? new GameObject("Deck Builder").AddComponent<DeckBuilderController>();

        var catalogue = BuildCataloguePanel(root, builder);
        var deck = BuildDeckPanel(root, builder);
        BuildCardPreview(canvas.transform);

        Wire(builder, catalogue, deck);

        EditorUtility.SetDirty(builder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DeckBuilder] Scene rebuilt — catalogue left, deck right, nothing overlapping.");
    }

    #region Catalogue (left)

    private static Transform BuildCataloguePanel(Transform root, DeckBuilderController builder)
    {
        var panel = Stretch(New("Catalogue", root), 0f, 0.62f);
        Background(panel, PanelColour);

        float top = Pad;
        top = Header(panel, "builder.all_cards", top);
        top = FilterRow(panel, builder, top);
        top = Hint(panel, "builder.hint", top);

        return ScrollList(panel, "Card Grid", top, Pad, grid: true);
    }

    private static float FilterRow(RectTransform panel, DeckBuilderController builder, float top)
    {
        string[] keys = { "builder.filter_all", "builder.filter_str", "builder.filter_dex",
                          "builder.filter_int", "builder.filter_wis" };

        const float width = 96f, gap = 8f, height = 40f;
        float x = Pad;

        for (int i = 0; i < keys.Length; i++)
        {
            var button = Button(panel, keys[i], width, height);
            TopLeft(button.GetComponent<RectTransform>(), x, top);
            UnityEventTools.AddIntPersistentListener(button.onClick, builder.SetFilter, i);
            x += width + gap;
        }
        return top + height + 10f;
    }

    #endregion

    #region Deck (right)

    private class DeckPanel
    {
        public Transform List;
        public GameObject RowTemplate;
        public TMP_InputField NameField;
        public TMP_Text Count, Validation, Budget, Str, Dex, Int, Wis;
    }

    private static DeckPanel BuildDeckPanel(Transform root, DeckBuilderController builder)
    {
        var panel = Stretch(New("Deck", root), 0.62f, 1f);
        Background(panel, PanelColour);
        var result = new DeckPanel();

        // --- top band: title, name, count -----------------------------------------------------
        float top = Pad;
        top = Header(panel, "builder.your_deck", top);

        result.NameField = InputField(panel, top);
        top += 46f + 8f;

        result.Count = Label(panel, "", 22f);
        TopStretch(result.Count.rectTransform, top, 28f);
        top += 28f + 10f;

        // --- bottom band: buttons, validation, attributes ---------------------------------------
        float bottom = Pad;

        var save = Button(panel, "builder.save", 190f, 54f);
        BottomCentre(save.GetComponent<RectTransform>(), -100f, bottom);
        save.GetComponent<Image>().color = AccentColour;
        UnityEventTools.AddPersistentListener(save.onClick, builder.Save);

        var back = Button(panel, "menu.back", 190f, 54f);
        BottomCentre(back.GetComponent<RectTransform>(), 100f, bottom);
        UnityEventTools.AddPersistentListener(back.onClick, builder.BackToMenu);
        bottom += 54f + 10f;

        result.Validation = Label(panel, "", 17f);
        result.Validation.alignment = TextAlignmentOptions.Top;
        BottomStretch(result.Validation.rectTransform, bottom, 76f);
        bottom += 76f + 10f;

        bottom = AttributeBlock(panel, builder, result, bottom);

        // --- the list takes everything in between ------------------------------------------------
        var list = ScrollList(panel, "Deck List", top, bottom, grid: false);
        result.List = list;
        result.RowTemplate = RowTemplate(panel);
        return result;
    }

    // Budget line plus the four steppers, on their own tinted slab so they read as one control.
    private static float AttributeBlock(RectTransform panel, DeckBuilderController builder,
                                        DeckPanel result, float bottom)
    {
        const float rowHeight = 44f, gap = 6f;
        float height = 30f + 4 * (rowHeight + gap) + Pad;

        var block = New("Attributes", panel);
        BottomStretch(block, bottom, height);
        Background(block, SectionColour);

        float y = Pad * 0.5f;
        result.Wis = Stepper(block, "WIS", y, builder.AddWisdom); y += rowHeight + gap;
        result.Int = Stepper(block, "INT", y, builder.AddIntellect); y += rowHeight + gap;
        result.Dex = Stepper(block, "DEX", y, builder.AddDexterity); y += rowHeight + gap;
        result.Str = Stepper(block, "STR", y, builder.AddStrength); y += rowHeight + gap;

        result.Budget = Label(block, "", 20f);
        BottomStretch(result.Budget.rectTransform, y, 28f);

        return bottom + height + 10f;
    }

    private static TMP_Text Stepper(RectTransform parent, string prefix, float y,
                                    UnityEngine.Events.UnityAction<int> onChange)
    {
        var label = Label(parent, prefix + "  0", 22f);
        BottomStretch(label.rectTransform, y, 44f);

        var minus = Button(parent, null, 44f, 40f);
        SetLabel(minus, "-");
        BottomCentre(minus.GetComponent<RectTransform>(), -130f, y + 2f);
        UnityEventTools.AddIntPersistentListener(minus.onClick, onChange, -1);

        var plus = Button(parent, null, 44f, 40f);
        SetLabel(plus, "+");
        BottomCentre(plus.GetComponent<RectTransform>(), 130f, y + 2f);
        UnityEventTools.AddIntPersistentListener(plus.onClick, onChange, +1);

        return label;
    }

    // One "3x Card Name" row, kept inactive; the controller clones it per distinct card.
    private static GameObject RowTemplate(Transform panel)
    {
        var row = New("Row Template", panel);
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.sizeDelta = new Vector2(400f, 34f);
        Background(row, new Color(0.16f, 0.16f, 0.2f));

        var label = Label(row, "1x Card", 19f);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Left;
        label.margin = new Vector4(10f, 0f, 10f, 0f);

        row.gameObject.SetActive(false);
        return row.gameObject;
    }

    #endregion

    #region Scrolling list

    /// A scroll view filling the gap between `top` and `bottom` insets. The ScrollRect lives on the
    /// view itself, so dragging over the attribute controls below never scrolls the list.
    private static Transform ScrollList(RectTransform panel, string name, float top, float bottom, bool grid)
    {
        var view = Stretch(New(name, panel));
        view.offsetMin = new Vector2(Pad, bottom);
        view.offsetMax = new Vector2(-Pad, -top);
        Background(view, ListColour);

        var viewport = Stretch(New("Viewport", view));
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = New("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, content.offsetMax.y);

        if (grid)
        {
            var layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = CardCell;
            layout.spacing = new Vector2(10f, 10f);
            layout.padding = new RectOffset(10, 10, 10, 10);
        }
        else
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.spacing = 3f;
            layout.padding = new RectOffset(8, 8, 8, 8);
        }
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        return content;
    }

    #endregion

    #region Scene scaffolding

    private static Canvas EnsureCanvas()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        return canvas;
    }

    // Hovering a catalogue card enlarges it, as in a match. Sits on the left over the grid, and
    // CardPreview makes itself non-blocking so it can't steal the hover.
    private static void BuildCardPreview(Transform canvas)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (prefab == null) return;

        var holder = New("Card Preview", canvas);
        holder.anchorMin = holder.anchorMax = new Vector2(0.31f, 0.5f);
        holder.sizeDelta = new Vector2(400f, 560f);

        var clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localScale = Vector3.one * 1.4f;
        CardDisplay.DisableGameplayInteractions(clone);

        var group = clone.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var preview = holder.gameObject.AddComponent<CardPreview>();
        var so = new SerializedObject(preview);
        so.FindProperty("root").objectReferenceValue = clone;
        so.FindProperty("display").objectReferenceValue = clone.GetComponent<CardDisplay>();
        so.ApplyModifiedPropertiesWithoutUndo();

        holder.SetAsLastSibling();
    }

    private static void Wire(DeckBuilderController builder, Transform catalogue, DeckPanel deck)
    {
        var so = new SerializedObject(builder);
        so.FindProperty("catalogueContainer").objectReferenceValue = catalogue;
        so.FindProperty("cardPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        so.FindProperty("catalogueCardScale").floatValue = CardScale;
        so.FindProperty("deckListContainer").objectReferenceValue = deck.List;
        so.FindProperty("deckRowPrefab").objectReferenceValue = deck.RowTemplate;
        so.FindProperty("deckNameField").objectReferenceValue = deck.NameField;
        so.FindProperty("deckCountLabel").objectReferenceValue = deck.Count;
        so.FindProperty("validationLabel").objectReferenceValue = deck.Validation;
        so.FindProperty("attributeBudgetLabel").objectReferenceValue = deck.Budget;
        so.FindProperty("strengthLabel").objectReferenceValue = deck.Str;
        so.FindProperty("dexterityLabel").objectReferenceValue = deck.Dex;
        so.FindProperty("intellectLabel").objectReferenceValue = deck.Int;
        so.FindProperty("wisdomLabel").objectReferenceValue = deck.Wis;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region Building blocks

    private static RectTransform New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// Fill the parent, optionally only between two horizontal fractions.
    private static RectTransform Stretch(RectTransform rect, float xMin = 0f, float xMax = 1f)
    {
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    // Full-width strip `inset` down from the top of its parent.
    private static void TopStretch(RectTransform rect, float inset, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(Pad, -inset - height);
        rect.offsetMax = new Vector2(-Pad, -inset);
    }

    // Full-width strip `inset` up from the bottom of its parent.
    private static void BottomStretch(RectTransform rect, float inset, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(Pad, inset);
        rect.offsetMax = new Vector2(-Pad, inset + height);
    }

    private static void TopLeft(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private static void BottomCentre(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static void Background(RectTransform rect, Color colour)
        => rect.gameObject.AddComponent<Image>().color = colour;

    private static float Header(RectTransform panel, string key, float top)
    {
        var label = Localise(Label(panel, "", 30f), key);
        label.fontStyle = FontStyles.Bold;
        TopStretch(label.rectTransform, top, 40f);
        return top + 40f + 12f;
    }

    private static float Hint(RectTransform panel, string key, float top)
    {
        var label = Localise(Label(panel, "", 17f), key);
        label.color = new Color(1f, 1f, 1f, 0.55f);
        TopStretch(label.rectTransform, top, 24f);
        return top + 24f + 8f;
    }

    private static TMP_Text Label(Transform parent, string text, float size)
    {
        var rect = New("Label", parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(10f, size * 0.6f);
        label.fontSizeMax = size;
        return label;
    }

    private static TMP_Text Localise(TMP_Text label, string key)
    {
        label.gameObject.AddComponent<LocalizedText>().SetKey(key);
        return label;
    }

    private static TMP_InputField InputField(RectTransform panel, float top)
    {
        var rect = New("Deck Name", panel);
        TopStretch(rect, top, 46f);
        Background(rect, new Color(0.15f, 0.15f, 0.19f));

        var textArea = Stretch(New("Text", rect));
        var text = textArea.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 21f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.margin = new Vector4(12f, 0f, 12f, 0f);

        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = text;
        input.text = Localization.T("builder.default_name");
        return input;
    }

    private static Button Button(Transform parent, string key, float width, float height)
    {
        var rect = New((key ?? "Button") + " Button", parent);
        rect.sizeDelta = new Vector2(width, height);

        var image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColour;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = Label(rect, "", 20f);
        Stretch(label.rectTransform);
        if (key != null) Localise(label, key);
        return button;
    }

    private static void SetLabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    #endregion
}
#endif
