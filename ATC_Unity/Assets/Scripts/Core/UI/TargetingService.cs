using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Runs the "choose a card" step of an effect on THIS machine: highlight the legal cards, wait for
/// one to be clicked (CardBoardActions forwards the click) and hand it back to the effect. Esc, a
/// right-click on a card, or no legal target at all cancels.
///
/// The service is deliberately unaware of who is playing. Online, NetworkTargeting decides which
/// machine opens the request — the host for its own player, the remote client for theirs — so only
/// one screen ever shows a prompt.
public class TargetingService : MonoBehaviour
{
    public static TargetingService Instance { get; private set; }

    private const string DefaultPrompt = "Choose a target  —  Esc to cancel";

    [Tooltip("Optional label showing what is being targeted. Left empty, a plain on-screen banner is drawn instead so the prompt is never invisible in a build.")]
    [SerializeField] private TMP_Text promptLabel;

    [Tooltip("Optional object switched on alongside the prompt label.")]
    [SerializeField] private GameObject promptRoot;

    private readonly List<Targetable> validTargets = new List<Targetable>();
    private Action<Targetable> onChosen;
    private Action onCancel;
    private TargetingPromptHUD fallbackPrompt;

    public bool IsActive => onChosen != null;

    /// The player the open request belongs to — the AI watches this to answer its own prompts.
    public Player Requester { get; private set; }

    /// What the player is being asked to pick, or null when no request is open.
    public string Prompt { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ShowPrompt(null);
    }

    #region Opening a request

    /// Open a request over every Targetable in the scene that passes `filter`.
    public void Request(Predicate<Targetable> filter, Action<Targetable> chosen, Action cancelled = null,
                        Player requester = null, string prompt = null)
    {
        if (filter == null)
        {
            Reject("a filter is required", cancelled);
            return;
        }
        RequestFrom(Collect(filter), chosen, cancelled, requester, prompt);
    }

    /// Open a request over an explicit set of cards. Used online: the host has already decided
    /// which cards are legal, so the client only has to present them.
    public void RequestFrom(IReadOnlyList<Targetable> targets, Action<Targetable> chosen, Action cancelled = null,
                            Player requester = null, string prompt = null)
    {
        if (chosen == null)    { Reject("no onChosen callback", cancelled); return; }
        if (IsActive)          { Reject("another request is already open", cancelled); return; }
        if (targets == null)   { Reject("no valid targets", cancelled); return; }

        foreach (var target in targets)
        {
            if (target == null) continue;
            validTargets.Add(target);
            target.SetHighlight(true);
        }

        if (validTargets.Count == 0)
        {
            Clear();
            Reject("no valid targets", cancelled);
            return;
        }

        onChosen = chosen;
        onCancel = cancelled;
        Requester = requester;
        ShowPrompt(string.IsNullOrEmpty(prompt) ? DefaultPrompt : prompt);
        Debug.Log($"[Targeting] Awaiting choice — {validTargets.Count} valid target(s).");
    }

    /// Every Targetable currently in the scene that passes `filter`. Static so the server can build
    /// the legal set for a remote player without opening a request of its own.
    public static List<Targetable> Collect(Predicate<Targetable> filter)
    {
        var found = new List<Targetable>();
        if (filter == null) return found;

        foreach (var target in FindObjectsByType<Targetable>(FindObjectsSortMode.None))
            if (target != null && filter(target)) found.Add(target);

        return found;
    }

    private static void Reject(string reason, Action cancelled)
    {
        Debug.Log($"[Targeting] Request cancelled — {reason}.");
        cancelled?.Invoke();
    }

    #endregion

    #region Answering a request

    public bool TryChoose(Targetable target)
    {
        if (!IsActive || target == null) return false;
        if (!validTargets.Contains(target)) return false;

        var chosen = onChosen;
        Clear();
        chosen?.Invoke(target);
        return true;
    }

    /// Pick whatever is legal — the AI's answer, and a safe way to unblock a prompt.
    public bool ChooseFirstValid()
    {
        if (!IsActive) return false;

        foreach (var target in validTargets)
            if (target != null) return TryChoose(target);

        return false;
    }

    public void Cancel()
    {
        if (!IsActive) return;

        var cancelled = onCancel;
        Clear();
        Debug.Log("[Targeting] Cancelled.");
        cancelled?.Invoke();
    }

    private void Update()
    {
        if (!IsActive) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
    }

    private void Clear()
    {
        foreach (var target in validTargets)
            if (target != null) target.SetHighlight(false);

        validTargets.Clear();
        onChosen = null;
        onCancel = null;
        Requester = null;
        ShowPrompt(null);
    }

    #endregion

    #region Prompt

    private void ShowPrompt(string text)
    {
        Prompt = text;

        if (promptLabel != null) promptLabel.text = text ?? string.Empty;
        if (promptRoot != null) promptRoot.SetActive(!string.IsNullOrEmpty(text));
        if (promptLabel == null && promptRoot == null) EnsureFallbackPrompt();
    }

    // With no prompt UI wired in the scene the player would see highlighted cards and no
    // explanation, so fall back to a plain banner that works in a build without any setup.
    private void EnsureFallbackPrompt()
    {
        if (fallbackPrompt == null) fallbackPrompt = gameObject.AddComponent<TargetingPromptHUD>();
    }

    #endregion
}
