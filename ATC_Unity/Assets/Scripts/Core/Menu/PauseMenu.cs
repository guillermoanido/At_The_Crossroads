using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// The Escape menu: resume, switch language, adjust volume, or leave for the main menu.
///
/// It installs itself into every scene automatically and survives scene loads, so there is never a
/// screen you can get stuck on — which was the problem in the deck builder, where the only way out
/// was its own BACK button.
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    private const string MenuScene = "Main Menu";

    private static PauseMenu instance;

    private GameObject panel;
    private TMP_Text volumeLabel;
    private Slider volumeSlider;

    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    // Runs once per play session, before any scene's Start. No prefab, no wiring, every scene.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var go = new GameObject("Pause Menu");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseMenu>();
    }

    private void Start()
    {
        Localization.Load();
        GameSettings.Load();
        Build();
        Close();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        // Esc is also "cancel targeting". If a card is being picked, let that win — the player is
        // answering a prompt, not asking to leave.
        var targeting = TargetingService.Instance;
        if (!IsOpen && targeting != null && targeting.IsActive) return;

        Toggle();
    }

    #region Actions

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);
        RefreshVolume();
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void SetLanguage(int language)
    {
        Localization.Set((Language)Mathf.Clamp(language, 0, 1));
        RefreshVolume();
    }

    /// Leave whatever is happening and go back to the main menu. A match is disconnected first —
    /// walking away from a live host would strand the other player.
    public void ReturnToMenu()
    {
        Close();
        Disconnect();
        GameLog.Clear();
        SceneManager.LoadScene(MenuScene);
    }

    private static void Disconnect()
    {
        if (NetworkServer.active && NetworkClient.isConnected) NetworkManager.singleton?.StopHost();
        else if (NetworkClient.isConnected) NetworkManager.singleton?.StopClient();
        else if (NetworkServer.active) NetworkManager.singleton?.StopServer();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RefreshVolume()
    {
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.Master);
        if (volumeLabel != null)
            volumeLabel.text = $"{Localization.T("settings.master")}  {Mathf.RoundToInt(GameSettings.Master * 100f)}%";
    }

    #endregion

    #region UI

    private void Build()
    {
        var canvasGO = new GameObject("Pause Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // above everything the scene draws

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Full-screen dimmer, so the menu reads as modal.
        var dim = NewRect("Dim", canvasGO.transform);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        panel = dim.gameObject;

        var box = NewRect("Box", dim);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(520f, 560f);
        box.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.1f, 0.98f);

        var title = Label(box, 32f);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, 0f, 220f, 460f, 46f);
        title.gameObject.AddComponent<LocalizedText>().SetKey("pause.title");

        volumeLabel = Label(box, 21f);
        Place(volumeLabel.rectTransform, 0f, 150f, 460f, 32f);

        volumeSlider = VolumeSlider(box);
        Place(volumeSlider.GetComponent<RectTransform>(), 0f, 108f, 360f, 22f);

        var language = Label(box, 21f);
        Place(language.rectTransform, 0f, 56f, 460f, 30f);
        language.gameObject.AddComponent<LocalizedText>().SetKey("settings.language");

        Place(Button(box, "settings.english", () => SetLanguage((int)Language.English)).GetComponent<RectTransform>(),
              -110f, 6f, 200f, 46f);
        Place(Button(box, "settings.spanish", () => SetLanguage((int)Language.Spanish)).GetComponent<RectTransform>(),
              110f, 6f, 200f, 46f);

        Place(Button(box, "pause.resume", Close).GetComponent<RectTransform>(), 0f, -70f, 420f, 52f);
        Place(Button(box, "pause.to_menu", ReturnToMenu).GetComponent<RectTransform>(), 0f, -132f, 420f, 52f);
        Place(Button(box, "menu.exit", QuitGame).GetComponent<RectTransform>(), 0f, -194f, 420f, 52f);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static TMP_Text Label(Transform parent, float size)
    {
        var rect = NewRect("Label", parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.6f;
        label.fontSizeMax = size;
        return label;
    }

    private static Button Button(Transform parent, string key, UnityEngine.Events.UnityAction action)
    {
        var rect = NewRect(key + " Button", parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.22f, 0.22f, 0.28f);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var label = Label(rect, 21f);
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label.gameObject.AddComponent<LocalizedText>().SetKey(key);
        return button;
    }

    private Slider VolumeSlider(Transform parent)
    {
        var rect = NewRect("Volume", parent);
        rect.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

        var slider = rect.gameObject.AddComponent<Slider>();
        var fillArea = NewRect("Fill Area", rect);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;

        var fill = NewRect("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.85f, 0.72f, 0.3f);

        slider.fillRect = fill;
        slider.targetGraphic = fillImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(v => { GameSettings.SetMaster(v); RefreshVolume(); });
        return slider;
    }

    #endregion
}
