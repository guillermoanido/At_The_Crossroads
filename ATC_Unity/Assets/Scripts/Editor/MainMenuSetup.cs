#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Wires the Main Menu scene up without touching its art. It finds the buttons that are already
/// there by name, builds the panels that are missing (settings, join, deck), and points every
/// button at MainMenuController.
///
/// Safe to re-run: generated panels live under one root that is rebuilt from scratch, and button
/// listeners are cleared before being re-added so nothing ends up wired twice.
public static class MainMenuSetup
{
    private const string ScenePath = "Assets/Scenes/Main Menu.unity";
    private const string GeneratedRootName = "Menu Panels (generated)";

    [MenuItem("ATC/Setup Main Menu Scene")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Menu] No Canvas in the Main Menu scene — nothing to attach the UI to.");
            return;
        }

        var controller = FindOrCreateController();
        var panels = BuildPanels(canvas.transform, controller);
        int wired = WireButtons(controller, panels);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[Menu] Main Menu ready — {wired} button(s) wired, panels rebuilt. " +
                  "Run ATC ▸ Generate Starter Decks if the deck list is empty.");
    }

    private static MainMenuController FindOrCreateController()
    {
        var existing = Object.FindFirstObjectByType<MainMenuController>();
        if (existing != null) return existing;

        var go = new GameObject("Main Menu Controller");
        return go.AddComponent<MainMenuController>();
    }

    #region Buttons

    // The scene's buttons are named for what they do ("Create Lobby", "Find Lobby", …), so match on
    // the object name and its label text and point each at the matching controller method.
    private static int WireButtons(MainMenuController controller, Panels panels)
    {
        int wired = 0;
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (IsInside(button.transform, panels.Root)) continue;   // generated buttons are wired already

            string key = (button.gameObject.name + " " + LabelOf(button)).ToLowerInvariant();

            if (Contains(key, "create lobby", "host"))          wired += Wire(button, controller.HostMatch, "menu.host");
            else if (Contains(key, "find lobby", "lobby finder", "join")) wired += Wire(button, controller.OpenJoin, "menu.join");
            else if (Contains(key, "deck"))                      wired += Wire(button, controller.OpenDeckPanel, "menu.deck");
            else if (Contains(key, "settings", "options"))       wired += Wire(button, controller.OpenSettings, "menu.settings");
            else if (Contains(key, "exit", "quit"))              wired += Wire(button, controller.QuitGame, "menu.exit");
            else Debug.Log($"[Menu] '{button.gameObject.name}' didn't match a known action — left alone.");
        }
        return wired;
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
            if (haystack.Contains(needle)) return true;
        return false;
    }

    private static string LabelOf(Button button)
    {
        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) return tmp.text;
        var text = button.GetComponentInChildren<Text>(true);
        return text != null ? text.text : string.Empty;
    }

    private static int Wire(Button button, UnityEngine.Events.UnityAction action, string labelKey)
    {
        // Bind the scene's own label too, so the player's art follows the language.
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.GetComponent<LocalizedText>() == null)
            label.gameObject.AddComponent<LocalizedText>().SetKey(labelKey);

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);

        UnityEventTools.AddPersistentListener(button.onClick, action);
        return 1;
    }

    private static bool IsInside(Transform child, Transform root)
        => root != null && child.IsChildOf(root);

    #endregion

    #region Panels

    private class Panels
    {
        public Transform Root;
        public GameObject Settings, Join, Deck;
        public Slider Master, Music, Sfx;
        public TMP_InputField Address;
        public TMP_Text DeckName, DeckSummary, Banner;
    }

    private static Panels BuildPanels(Transform canvas, MainMenuController controller)
    {
        var old = canvas.Find(GeneratedRootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = NewRect(GeneratedRootName, canvas, Vector2.zero, Vector2.one, Vector2.zero);
        var panels = new Panels { Root = root };

        // Always-visible reminder of the chosen deck, so "did I pick one?" is never a question.
        panels.Banner = AddLabel(root, "", 0f, 24f);
        panels.Banner.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        panels.Banner.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        panels.Banner.rectTransform.anchoredPosition = new Vector2(0f, -46f);

        BuildSettingsPanel(root, panels, controller);
        BuildJoinPanel(root, panels, controller);
        BuildDeckPanel(root, panels, controller);

        ApplyToController(controller, panels);
        return panels;
    }

    private static void BuildSettingsPanel(Transform root, Panels panels, MainMenuController controller)
    {
        var panel = NewPanel("Settings Panel", root, "settings.audio", 520f, 520f);
        panels.Settings = panel.gameObject;

        panels.Master = AddSlider(panel, "settings.master", 170f);
        panels.Music  = AddSlider(panel, "settings.music", 100f);
        panels.Sfx    = AddSlider(panel, "settings.sfx", 30f);

        Localize(AddLabel(panel, "", -50f, 24f), "settings.language");
        var english = AddButton(panel, "settings.english", new Vector2(-110f, -100f), null, 200f);
        var spanish = AddButton(panel, "settings.spanish", new Vector2(110f, -100f), null, 200f);
        UnityEventTools.AddIntPersistentListener(english.onClick, controller.SetLanguage, (int)Language.English);
        UnityEventTools.AddIntPersistentListener(spanish.onClick, controller.SetLanguage, (int)Language.Spanish);

        AddCloseButton(panel, controller);
    }

    // Bind a label to a translation key so it follows the language for the rest of its life.
    private static TMP_Text Localize(TMP_Text label, string key)
    {
        label.gameObject.AddComponent<LocalizedText>().SetKey(key);
        return label;
    }

    private static void BuildJoinPanel(Transform root, Panels panels, MainMenuController controller)
    {
        var panel = NewPanel("Join Panel", root, "join.title", 560f, 340f);
        panels.Join = panel.gameObject;

        Localize(AddLabel(panel, "", 80f, 22f), "join.address_hint");
        panels.Address = AddInput(panel, 30f);

        AddButton(panel, "menu.connect", new Vector2(-110f, -70f), controller.JoinMatch);
        AddCloseButton(panel, controller);
    }

    private static void BuildDeckPanel(Transform root, Panels panels, MainMenuController controller)
    {
        var panel = NewPanel("Deck Panel", root, "deck.title", 620f, 400f);
        panels.Deck = panel.gameObject;

        panels.DeckName = AddLabel(panel, "—", 90f, 34f);
        panels.DeckSummary = AddLabel(panel, "—", 50f, 20f);

        AddButton(panel, "<", new Vector2(-220f, 90f), controller.PreviousDeck, 60f);
        AddButton(panel, ">", new Vector2(220f, 90f), controller.NextDeck, 60f);

        // Browsing already selects, so this button exists to say so plainly and get out of the way.
        var use = AddButton(panel, "deck.use", new Vector2(0f, -10f), controller.ConfirmDeck, 260f);
        use.GetComponent<Image>().color = new Color(0.24f, 0.42f, 0.26f);

        AddButton(panel, "deck.build", new Vector2(0f, -70f), controller.OpenDeckBuilder, 260f);
        AddCloseButton(panel, controller);
    }

    private static void ApplyToController(MainMenuController controller, Panels panels)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("settingsPanel").objectReferenceValue = panels.Settings;
        so.FindProperty("joinPanel").objectReferenceValue = panels.Join;
        so.FindProperty("deckPanel").objectReferenceValue = panels.Deck;
        so.FindProperty("addressField").objectReferenceValue = panels.Address;
        so.FindProperty("deckNameLabel").objectReferenceValue = panels.DeckName;
        so.FindProperty("deckSummaryLabel").objectReferenceValue = panels.DeckSummary;
        so.FindProperty("selectedDeckBanner").objectReferenceValue = panels.Banner;
        so.FindProperty("masterSlider").objectReferenceValue = panels.Master;
        so.FindProperty("musicSlider").objectReferenceValue = panels.Music;
        so.FindProperty("sfxSlider").objectReferenceValue = panels.Sfx;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region UI building blocks

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        if (size != Vector2.zero) rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform NewPanel(string name, Transform parent, string title, float width, float height)
    {
        var rect = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(width, height));

        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.05f, 0.08f, 0.94f);

        var heading = Localize(AddLabel(rect, "", height / 2f - 40f, 28f), title);
        heading.fontStyle = FontStyles.Bold;

        rect.gameObject.SetActive(false);
        return rect;
    }

    private static TMP_Text AddLabel(Transform parent, string text, float y, float size)
    {
        var rect = NewRect("Label", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, 44f));
        rect.anchoredPosition = new Vector2(0f, y);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static Slider AddSlider(Transform parent, string labelKey, float y)
    {
        Localize(AddLabel(parent, "", y + 30f, 20f), labelKey);

        var rect = NewRect($"{labelKey} Slider", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 22f));
        rect.anchoredPosition = new Vector2(0f, y);

        var slider = rect.gameObject.AddComponent<Slider>();
        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.25f);

        var fillArea = NewRect("Fill Area", rect, Vector2.zero, Vector2.one, Vector2.zero);
        var fill = NewRect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.85f, 0.72f, 0.3f);

        slider.fillRect = fill;
        slider.targetGraphic = fillImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private static TMP_InputField AddInput(Transform parent, float y)
    {
        var rect = NewRect("Address Field", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, 46f));
        rect.anchoredPosition = new Vector2(0f, y);

        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.15f, 0.15f, 0.19f);

        var textArea = NewRect("Text", rect, Vector2.zero, Vector2.one, Vector2.zero);
        var text = textArea.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 22f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.margin = new Vector4(12f, 0f, 12f, 0f);

        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = text;
        input.text = "localhost";
        return input;
    }

    // `key` is a translation key; the label binds to it and follows the language.
    private static Button AddButton(Transform parent, string key, Vector2 position,
                                    UnityEngine.Events.UnityAction action, float width = 200f)
    {
        var rect = NewRect($"{key} Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(width, 52f));
        rect.anchoredPosition = position;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.22f, 0.22f, 0.28f);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var labelRect = NewRect("Text", rect, Vector2.zero, Vector2.one, Vector2.zero);
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        Localize(label, key);

        if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
        return button;
    }

    private static void AddCloseButton(RectTransform panel, MainMenuController controller)
        => AddButton(panel, "menu.back", new Vector2(0f, -panel.sizeDelta.y / 2f + 42f), controller.CloseAllPanels, 160f);

    #endregion
}
#endif
