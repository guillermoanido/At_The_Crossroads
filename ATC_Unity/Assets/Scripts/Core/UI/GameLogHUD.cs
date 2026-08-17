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

    private TMP_Text instructionLabel;
    private Image instructionBackground;
    private TMP_Text feedLabel;

    /// Called by anything that logs, so the HUD exists as soon as there is something to show.
    public static void Ensure()
    {
        if (instance != null) return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;   // no UI in this scene; the console still has everything

        // Created WITH a RectTransform: adding one afterwards to an object that already has a
        // plain Transform is not something Unity handles cleanly.
        var go = new GameObject("Game Log HUD", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        instance = go.AddComponent<GameLogHUD>();
        instance.Build();
    }

    private void OnEnable() => GameLog.Changed += Refresh;
    private void OnDisable() => GameLog.Changed -= Refresh;

    private void Build()
    {
        Stretch(GetComponent<RectTransform>());
        gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;   // never eat a click

        // Instruction: a banner across the bottom, where the eye already is for the hand.
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

        // Feed: top-left, out of the way of the board.
        var feed = NewRect("Feed", transform);
        feed.anchorMin = new Vector2(0f, 1f);
        feed.anchorMax = new Vector2(0f, 1f);
        feed.pivot = new Vector2(0f, 1f);
        feed.sizeDelta = new Vector2(520f, 200f);
        feed.anchoredPosition = new Vector2(24f, -24f);

        feedLabel = NewLabel(feed, 18f, TextAlignmentOptions.TopLeft);
        feedLabel.color = new Color(1f, 1f, 1f, 0.9f);

        transform.SetAsLastSibling();
        Refresh();
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
