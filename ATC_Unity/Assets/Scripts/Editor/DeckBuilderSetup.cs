#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Builds the whole deck-builder screen into the DeckBuilding scene: card catalogue on the left,
/// the deck you are assembling on the right, filters, attribute steppers and save/back.
///
/// Safe to re-run — everything it makes lives under one root that is deleted and rebuilt.
public static class DeckBuilderSetup
{
    private const string ScenePath = "Assets/Scenes/DeckBuilding.unity";
    private const string CardPrefabPath = "Assets/Prefabs/CardPrefab.prefab";
    private const string RootName = "Deck Builder UI (generated)";

    private static readonly Color PanelColour  = new Color(0.06f, 0.06f, 0.09f, 0.96f);
    private static readonly Color ButtonColour = new Color(0.22f, 0.22f, 0.28f);

    [MenuItem("ATC/Setup Deck Builder Scene")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = EnsureCanvas();
        var old = canvas.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = NewRect(RootName, canvas.transform, Vector2.zero, Vector2.one);
        var builder = new GameObject("Deck Builder").AddComponent<DeckBuilderController>();

        var catalogue = BuildCataloguePanel(root, builder);
        var deckPanel = BuildDeckPanel(root, builder);
        var preview = BuildCardPreview(canvas.transform);

        Wire(builder, catalogue, deckPanel, preview);

        EditorUtility.SetDirty(builder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DeckBuilder] Scene built. Open it and press Play, or reach it from the menu's deck panel.");
    }

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

    // Hovering a card in the catalogue enlarges it, exactly like in the match — HoverPreview needs
    // a CardPreview in the scene to talk to.
    private static CardPreview BuildCardPreview(Transform canvas)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (prefab == null) return null;

        // Sits over the catalogue's right edge rather than dead centre, so it never covers the
        // card the cursor is on. CardPreview also makes itself non-blocking, belt and braces.
        var holder = NewRect("Card Preview", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        holder.sizeDelta = new Vector2(400f, 560f);
        holder.anchoredPosition = new Vector2(-140f, 0f);

        var clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localScale = Vector3.one * 1.1f;
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
        return preview;
    }

    #endregion

    #region Panels

    private class Catalogue { public Transform Grid; }

    private static Catalogue BuildCataloguePanel(Transform root, DeckBuilderController builder)
    {
        var panel = NewRect("Catalogue", root, new Vector2(0f, 0f), new Vector2(0.62f, 1f));
        Background(panel, PanelColour);

        Header(panel, "ALL CARDS", -34f);

        // Filter row — the index matches DeckBuilderController.Filter.
        string[] filters = { "ALL", "STR", "DEX", "INT", "WIS" };
        for (int i = 0; i < filters.Length; i++)
        {
            int index = i;
            var button = AddButton(panel, filters[i], new Vector2(0f, 1f),
                                   new Vector2(70f + i * 110f, -86f), 100f, 38f);
            UnityEventTools.AddIntPersistentListener(button.onClick, builder.SetFilter, index);
        }

        var viewport = NewRect("Viewport", panel, new Vector2(0f, 0f), new Vector2(1f, 1f));
        viewport.offsetMin = new Vector2(20f, 20f);
        viewport.offsetMax = new Vector2(-20f, -120f);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = NewRect("Grid", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150f, 210f);
        grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(12, 12, 12, 12);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;

        return new Catalogue { Grid = content };
    }

    private class DeckPanel
    {
        public Transform List;
        public GameObject RowTemplate;
        public TMP_InputField NameField;
        public TMP_Text Count, Validation, Budget, Str, Dex, Int, Wis;
    }

    private static DeckPanel BuildDeckPanel(Transform root, DeckBuilderController builder)
    {
        var panel = NewRect("Deck", root, new Vector2(0.62f, 0f), new Vector2(1f, 1f));
        Background(panel, PanelColour);
        var result = new DeckPanel();

        Header(panel, "YOUR DECK", -34f);
        result.NameField = AddInput(panel, -80f);
        result.Count = AddLabel(panel, "0 / 40 cards", -124f, 22f);

        // Deck list, with an inactive row used as the template the controller clones.
        var viewport = NewRect("Viewport", panel, new Vector2(0f, 0f), new Vector2(1f, 1f));
        viewport.offsetMin = new Vector2(20f, 330f);
        viewport.offsetMax = new Vector2(-20f, -150f);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = NewRect("List", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.spacing = 3f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;

        result.List = content;
        result.RowTemplate = BuildRowTemplate(panel);

        // Attribute steppers.
        result.Budget = AddLabel(panel, "0 / 20 points", 300f, 22f);
        result.Str = AddStepper(panel, "STR", 250f, builder.AddStrength);
        result.Dex = AddStepper(panel, "DEX", 200f, builder.AddDexterity);
        result.Int = AddStepper(panel, "INT", 150f, builder.AddIntellect);
        result.Wis = AddStepper(panel, "WIS", 100f, builder.AddWisdom);

        result.Validation = AddLabel(panel, "", 40f, 17f);
        result.Validation.alignment = TextAlignmentOptions.Top;
        result.Validation.rectTransform.sizeDelta = new Vector2(420f, 80f);

        AddButton(panel, "SAVE DECK", new Vector2(0.5f, 0f), new Vector2(-110f, 40f), 200f, 50f)
            .onClick.AddPersistent(builder.Save);
        AddButton(panel, "BACK", new Vector2(0.5f, 0f), new Vector2(110f, 40f), 200f, 50f)
            .onClick.AddPersistent(builder.BackToMenu);

        return result;
    }

    // A single "3x Card Name" row, left inactive so it is never shown; the controller clones it.
    private static GameObject BuildRowTemplate(Transform panel)
    {
        var row = NewRect("Row Template", panel, new Vector2(0f, 1f), new Vector2(0f, 1f));
        row.sizeDelta = new Vector2(420f, 34f);

        var image = row.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.2f);

        var label = AddLabel(row, "1x Card", 0f, 19f);
        label.alignment = TextAlignmentOptions.Left;
        label.margin = new Vector4(10f, 0f, 10f, 0f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        row.gameObject.SetActive(false);
        return row.gameObject;
    }

    private static void Wire(DeckBuilderController builder, Catalogue catalogue, DeckPanel deck, CardPreview preview)
    {
        var so = new SerializedObject(builder);
        so.FindProperty("catalogueContainer").objectReferenceValue = catalogue.Grid;
        so.FindProperty("cardPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
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

    #region UI building blocks

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void Background(RectTransform rect, Color colour)
        => rect.gameObject.AddComponent<Image>().color = colour;

    private static TMP_Text Header(Transform parent, string text, float y)
    {
        var label = AddLabel(parent, text, y, 30f);
        label.fontStyle = FontStyles.Bold;
        label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        return label;
    }

    private static TMP_Text AddLabel(Transform parent, string text, float y, float size)
    {
        var rect = NewRect("Label", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.sizeDelta = new Vector2(440f, 40f);
        rect.anchoredPosition = new Vector2(0f, y);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_InputField AddInput(Transform parent, float y)
    {
        var rect = NewRect("Deck Name", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        rect.sizeDelta = new Vector2(400f, 44f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.19f);

        var textArea = NewRect("Text", rect, Vector2.zero, Vector2.one);
        var text = textArea.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 21f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.margin = new Vector4(12f, 0f, 12f, 0f);

        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = text;
        input.text = "My Deck";
        return input;
    }

    private static TMP_Text AddStepper(Transform parent, string name, float y,
                                       UnityEngine.Events.UnityAction<int> onChange)
    {
        var label = AddLabel(parent, $"{name}  0", y, 22f);
        label.rectTransform.sizeDelta = new Vector2(180f, 40f);

        var minus = AddButton(parent, "-", new Vector2(0.5f, 0.5f), new Vector2(-140f, y), 46f, 40f);
        UnityEventTools.AddIntPersistentListener(minus.onClick, onChange, -1);

        var plus = AddButton(parent, "+", new Vector2(0.5f, 0.5f), new Vector2(140f, y), 46f, 40f);
        UnityEventTools.AddIntPersistentListener(plus.onClick, onChange, +1);

        return label;
    }

    private static Button AddButton(Transform parent, string text, Vector2 anchor, Vector2 position,
                                    float width, float height)
    {
        var rect = NewRect($"{text} Button", parent, anchor, anchor);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColour;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var labelRect = NewRect("Text", rect, Vector2.zero, Vector2.one);
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return button;
    }

    private static void AddPersistent(this Button.ButtonClickedEvent click, UnityEngine.Events.UnityAction action)
        => UnityEventTools.AddPersistentListener(click, action);

    #endregion
}
#endif
