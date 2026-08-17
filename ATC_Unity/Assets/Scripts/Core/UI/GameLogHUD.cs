using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Puts GameLog on screen: a standing instruction across the bottom, and a fading feed of what
/// just happened in the corner.
///
/// It builds its own UI at runtime and adds itself where it is needed, so no scene wiring is
/// required and it works in every scene a match can run in.
[DisallowMultipleComponent]
public class GameLogHUD : MonoBehaviour
{
    private const float FeedHoldSeconds = 6f;
    private const float FeedFadeSeconds = 1.5f;

    private static GameLogHUD instance;

    [Tooltip("Shows what the game is waiting for. Leave empty and one is built at runtime.")]
    [SerializeField] private TMP_Text instructionLabel;

    [Tooltip("Optional backing plate behind the instruction, hidden while there is nothing to say.")]
    [SerializeField] private Image instructionBackground;

    [Tooltip("Shows what just happened. Leave empty and one is built at runtime.")]
    [SerializeField] private TMP_Text feedLabel;

    [Tooltip("Lists what is waiting on the stack. Leave empty and one is built at runtime.")]
    [SerializeField] private TMP_Text stackLabel;

    /// Called by anything that logs, so the HUD exists as soon as there is something to show.
    /// A GameLogHUD placed in the scene by hand always wins — that is how you restyle it.
    public static void Ensure()
    {
        if (instance != null) return;

        instance = FindFirstObjectByType<GameLogHUD>();
        if (instance != null) { instance.BuildMissingParts(); return; }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;   // no UI in this scene; the console still has everything

        // Created WITH a RectTransform: adding one afterwards to an object that already has a
        // plain Transform is not something Unity handles cleanly.
        var go = new GameObject("Game Log HUD", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        instance = go.AddComponent<GameLogHUD>();
        instance.BuildMissingParts();
    }

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnEnable()
    {
        GameLog.Changed += Refresh;
        GameStack.Changed += Refresh;
        BuildMissingParts();
    }

    private void OnDisable()
    {
        GameLog.Changed -= Refresh;
        GameStack.Changed -= Refresh;
    }

    /// Builds only the pieces that were not wired in the Inspector, so a partly-authored HUD keeps
    /// whatever you gave it and gets the rest for free.
    private void BuildMissingParts()
    {
        if (instructionLabel == null || feedLabel == null || stackLabel == null) Build();
        Refresh();
    }

    private void Build()
    {
        var self = GetComponent<RectTransform>();
        if (self != null) Stretch(self);
        if (GetComponent<CanvasGroup>() == null)
            gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;   // never eat a click

        if (instructionLabel == null) BuildInstruction();
        if (feedLabel == null) BuildFeed();
        if (stackLabel == null) BuildStack();

        transform.SetAsLastSibling();
    }

    private void BuildInstruction()
    {
        // A banner across the bottom, where the eye already is for the hand.
        var banner = NewRect("Instruction", transform);
        banner.anchorMin = new Vector2(0.5f, 0f);
        banner.anchorMax = new Vector2(0.5f, 0f);
        banner.pivot = new Vector2(0.5f, 0f);
        banner.sizeDelta = new Vector2(900f, 54f);
        banner.anchoredPosition = new Vector2(0f, 96f);

        instructionBackground = banner.gameObject.AddComponent<Image>();
        instructionBackground.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);

        instructionLabel = NewLabel(banner, 24f, TextAlignmentOptions.Center);
        instructionLabel.color = new Color(1f, 0.88f, 0.35f);
        instructionLabel.fontStyle = FontStyles.Bold;
    }

    private void BuildFeed()
    {
        // Top-left, out of the way of the board.
        var feed = NewRect("Feed", transform);
        feed.anchorMin = new Vector2(0f, 1f);
        feed.anchorMax = new Vector2(0f, 1f);
        feed.pivot = new Vector2(0f, 1f);
        feed.sizeDelta = new Vector2(520f, 200f);
        feed.anchoredPosition = new Vector2(24f, -24f);

        feedLabel = NewLabel(feed, 18f, TextAlignmentOptions.TopLeft);
        feedLabel.color = new Color(1f, 1f, 1f, 0.9f);
    }

    private void BuildStack()
    {
        // Top-right: what is queued, and who the game is waiting on.
        var stack = NewRect("Stack", transform);
        stack.anchorMin = stack.anchorMax = new Vector2(1f, 1f);
        stack.pivot = new Vector2(1f, 1f);
        stack.sizeDelta = new Vector2(420f, 260f);
        stack.anchoredPosition = new Vector2(-24f, -24f);

        stackLabel = NewLabel(stack, 18f, TextAlignmentOptions.TopRight);
        stackLabel.color = new Color(0.85f, 0.9f, 1f);
    }

    private void Update()
    {
        // The feed fades on its own so it doesn't sit over the board forever.
        if (feedLabel == null) return;

        float age = GameLog.SecondsSinceLastLine;
        float alpha = age <= FeedHoldSeconds
            ? 1f
            : Mathf.Clamp01(1f - (age - FeedHoldSeconds) / FeedFadeSeconds);

        var colour = feedLabel.color;
        if (!Mathf.Approximately(colour.a, alpha * 0.9f))
        {
            colour.a = alpha * 0.9f;
            feedLabel.color = colour;
        }
    }

    private void Refresh()
    {
        if (instructionLabel == null) return;

        string instruction = GameLog.Instruction;
        bool waiting = !string.IsNullOrEmpty(instruction);

        instructionLabel.text = instruction ?? string.Empty;
        if (instructionBackground != null) instructionBackground.enabled = waiting;

        var text = new StringBuilder();
        foreach (var line in GameLog.Feed) text.AppendLine(line);
        feedLabel.text = text.ToString();

        RefreshStack();
    }

    // The stack is the single most confusing thing about a game with a response window: a card that
    // "did nothing" is usually just sitting here, waiting for somebody to pass.
    private void RefreshStack()
    {
        if (stackLabel == null) return;

        var stack = GameStack.Instance;
        if (stack == null || stack.IsEmpty)
        {
            stackLabel.text = string.Empty;
            return;
        }

        var text = new StringBuilder();
        text.AppendLine($"<b>{Localization.T("stack.title")}</b>");

        // The TOP of the stack resolves first, so show it first.
        var items = stack.Items;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var card = items[i]?.sourceCardData;
            if (card != null) text.AppendLine($"{items.Count - i}.  {Localization.CardName(card)}");
        }

        text.AppendLine(stack.IsResolving
            ? Localization.T("stack.resolving")
            : Localization.T("stack.waiting"));

        stackLabel.text = text.ToString();
    }

    #region UI helpers

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text NewLabel(Transform parent, float size, TextAlignmentOptions alignment)
    {
        var rect = NewRect("Text", parent);
        Stretch(rect);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.7f;
        label.fontSizeMax = size;
        label.margin = new Vector4(12f, 6f, 12f, 6f);
        return label;
    }

    #endregion
}
