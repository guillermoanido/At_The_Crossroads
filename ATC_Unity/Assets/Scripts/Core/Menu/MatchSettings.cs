using UnityEngine;

/// What the menu decided, carried into the gameplay scene. Static rather than a DontDestroyOnLoad
/// object so nothing has to be wired up and a scene loaded directly in the editor still works —
/// the mode is simply None and the gameplay scene behaves exactly as it always did.
public static class MatchSettings
{
    public enum Mode { None, Host, Join }

    private const string DeckKey = "atc.deck.selected";

    /// Set by the menu just before it loads the gameplay scene; consumed once by ATCNetworkManager
    /// so a later manual reload doesn't silently reconnect.
    public static Mode PendingMode { get; private set; } = Mode.None;

    /// Host address the player typed in. Ignored when hosting.
    public static string Address { get; private set; } = "localhost";

    /// The deck this player brings to the match. Null means "whatever the scene already had",
    /// which keeps the old Resources-loaded behaviour working.
    public static DeckDefinition SelectedDeck { get; private set; }

    public static void HostWith(DeckDefinition deck)
    {
        SelectDeck(deck);
        PendingMode = Mode.Host;
    }

    public static void JoinWith(DeckDefinition deck, string address)
    {
        SelectDeck(deck);
        Address = string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim();
        PendingMode = Mode.Join;
    }

    /// Remembers the choice across sessions so the menu comes back to the same deck.
    public static void SelectDeck(DeckDefinition deck)
    {
        SelectedDeck = deck;
        if (deck == null) return;

        PlayerPrefs.SetString(DeckKey, deck.name);
        PlayerPrefs.Save();
    }

    /// Re-select whatever was chosen last time, if it still exists.
    public static void RestoreSelectedDeck()
    {
        if (SelectedDeck != null) return;

        string saved = PlayerPrefs.GetString(DeckKey, null);
        SelectedDeck = string.IsNullOrEmpty(saved)
            ? DeckLibrary.First()
            : DeckLibrary.ByName(saved) ?? DeckLibrary.First();
    }

    /// Read once by ATCNetworkManager as the gameplay scene loads.
    public static Mode ConsumePendingMode()
    {
        var mode = PendingMode;
        PendingMode = Mode.None;
        return mode;
    }
}
