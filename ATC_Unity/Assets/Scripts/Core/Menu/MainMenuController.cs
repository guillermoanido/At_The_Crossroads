using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Drives the main menu. Every button on the menu points at one of the public methods here.
///
/// The menu never touches the network itself — it records the player's choice in MatchSettings and
/// loads the gameplay scene, where ATCNetworkManager acts on it. That keeps all the Mirror code in
/// one place and means the gameplay scene still works when opened directly in the editor.
public class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Gameplay scene, loaded once the player has chosen to host or join.")]
    [SerializeField] private string matchScene = "ATC";

    [Tooltip("Deck building scene, opened by the deck button.")]
    [SerializeField] private string deckBuilderScene = "DeckBuilding";

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject deckPanel;

    [Header("Join")]
    [Tooltip("Where the joining player types the host's LAN address.")]
    [SerializeField] private TMP_InputField addressField;

    [Header("Deck selection")]
    [SerializeField] private TMP_Text deckNameLabel;
    [SerializeField] private TMP_Text deckSummaryLabel;

    [Header("Audio")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Tooltip("Optional. Music and SFX sliders only do anything when a mixer with exposed 'MusicVolume' and 'SfxVolume' parameters is assigned here.")]
    [SerializeField] private AudioMixer audioMixer;

    private void Start()
    {
        GameSettings.Load();
        if (audioMixer != null) GameSettings.UseMixer(audioMixer);

        MatchSettings.RestoreSelectedDeck();

        CloseAllPanels();
        BindAudioSliders();
        RefreshDeckLabels();

        if (addressField != null) addressField.text = MatchSettings.Address;
    }

    #region Match

    /// Host button. This player becomes the server; the other side joins their LAN address.
    public void HostMatch()
    {
        if (!HasPlayableDeck()) return;

        MatchSettings.HostWith(MatchSettings.SelectedDeck);
        SceneManager.LoadScene(matchScene);
    }

    /// Join button — opens the address prompt rather than connecting straight away.
    public void OpenJoin()
    {
        if (!HasPlayableDeck()) return;
        ShowOnly(joinPanel);
    }

    /// Confirm inside the join panel.
    public void JoinMatch()
    {
        if (!HasPlayableDeck()) return;

        string address = addressField != null ? addressField.text : null;
        MatchSettings.JoinWith(MatchSettings.SelectedDeck, address);
        SceneManager.LoadScene(matchScene);
    }

    private bool HasPlayableDeck()
    {
        var deck = MatchSettings.SelectedDeck;
        if (deck != null && deck.Count >= DeckRules.MinDeckSize) return true;

        Debug.LogWarning($"[Menu] Cannot start: {DeckLibrary.DisplayName(deck)} has " +
                         $"{(deck != null ? deck.Count : 0)}/{DeckRules.MinDeckSize} cards.");
        ShowOnly(deckPanel);
        return false;
    }

    #endregion

    #region Decks

    public void OpenDeckPanel() => ShowOnly(deckPanel);

    /// Cycle to the next ready-made deck. A proper list view replaces this once the builder exists.
    public void NextDeck() => StepDeck(+1);
    public void PreviousDeck() => StepDeck(-1);

    private void StepDeck(int direction)
    {
        var all = DeckLibrary.All;
        if (all.Count == 0) return;

        int next = (DeckLibrary.IndexOf(MatchSettings.SelectedDeck) + direction + all.Count) % all.Count;
        MatchSettings.SelectDeck(all[next]);
        RefreshDeckLabels();
    }

    /// Opens the deck building scene. Building a deck saves it into the same library the menu
    /// reads, so a custom deck shows up here alongside the ready-made ones.
    public void OpenDeckBuilder()
    {
        if (string.IsNullOrEmpty(deckBuilderScene)) return;
        SceneManager.LoadScene(deckBuilderScene);
    }

    private void RefreshDeckLabels()
    {
        var deck = MatchSettings.SelectedDeck;
        if (deckNameLabel != null) deckNameLabel.text = DeckLibrary.DisplayName(deck);
        if (deckSummaryLabel != null) deckSummaryLabel.text = DeckLibrary.Summary(deck);
    }

    #endregion

    #region Settings

    public void OpenSettings() => ShowOnly(settingsPanel);

    private void BindAudioSliders()
    {
        Bind(masterSlider, GameSettings.Master, GameSettings.SetMaster);
        Bind(musicSlider, GameSettings.Music, GameSettings.SetMusic);
        Bind(sfxSlider, GameSettings.Sfx, GameSettings.SetSfx);
    }

    private static void Bind(Slider slider, float current, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(current);
        slider.onValueChanged.RemoveListener(onChanged);
        slider.onValueChanged.AddListener(onChanged);
    }

    #endregion

    #region Panels & quit

    public void CloseAllPanels() => ShowOnly(null);

    private void ShowOnly(GameObject panel)
    {
        SetActive(settingsPanel, settingsPanel == panel);
        SetActive(joinPanel, joinPanel == panel);
        SetActive(deckPanel, deckPanel == panel);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }

    /// Exit button. Stopping play mode in the editor is the closest equivalent to quitting.
    public void QuitGame()
    {
        Debug.Log("[Menu] Quit requested.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion
}
