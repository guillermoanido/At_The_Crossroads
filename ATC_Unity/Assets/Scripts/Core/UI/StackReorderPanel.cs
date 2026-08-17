using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Timewatch's panel: the cards waiting on the stack, with arrows to move them up and down before
/// they resolve.
///
/// The list is shown top-down in RESOLUTION order — whatever sits at the top happens first, which
/// is the opposite of the underlying list, so the conversion happens here rather than in the rules.
///
/// Built at runtime and opened on demand, so it needs no scene setup and works on either machine.
public class StackReorderPanel : MonoBehaviour
{
    private static StackReorderPanel instance;

    private GameObject panel;
    private Transform listRoot;
    private readonly List<GameObject> rows = new List<GameObject>();

    /// Entries in the order the player currently wants them to resolve. Each value is the card's
    /// index in the stack as it was handed to us.
    private readonly List<int> order = new List<int>();
    private readonly List<string> names = new List<string>();

    private Action<int[]> onConfirmed;

    /// Show `cardNames` (resolution order, first resolves first) and report the chosen order back.
    public static void Show(IList<string> cardNames, Action<int[]> confirmed)
    {
        if (cardNames == null || cardNames.Count == 0) { confirmed?.Invoke(new int[0]); return; }

        Ensure();
        if (instance == null) { confirmed?.Invoke(new int[0]); return; }
        instance.Open(cardNames, confirmed);
    }

    private static void Ensure()
    {
        if (instance != null) return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("Stack Reorder Panel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<StackReorderPanel>();
        instance.Build();
    }

    private void Open(IList<string> cardNames, Action<int[]> confirmed)
    {
        onConfirmed = confirmed;

        names.Clear();
        order.Clear();
        for (int i = 0; i < cardNames.Count; i++)
        {
            names.Add(cardNames[i]);
            order.Add(i);
        }

        panel.SetActive(true);
        transform.SetAsLastSibling();
        Rebuild();
    }

    private void Move(int row, int direction)
    {
        int target = row + direction;
        if (row < 0 || row >= order.Count || target < 0 || target >= order.Count) return;

        (order[row], order[target]) = (order[target], order[row]);
        Rebuild();
    }

    public void Confirm()
    {
        var answer = onConfirmed;
        var chosen = order.ToArray();

        onConfirmed = null;
        panel.SetActive(false);
        answer?.Invoke(chosen);
    }

    #region UI

    private void Build()
    {
        var self = GetComponent<RectTransform>();
        Stretch(self);

        var dim = NewRect("Dim", transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        panel = dim.gameObject;

        var box = NewRect("Box", dim);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(560f, 520f);
        box.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.1f, 0.98f);

        var title = Label(box, 28f);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, 0f, 220f, 500f, 44f);
        title.gameObject.AddComponent<LocalizedText>().SetKey("stack.reorder_title");

        var hint = Label(box, 17f);
        hint.color = new Color(1f, 1f, 1f, 0.6f);
        Place(hint.rectTransform, 0f, 184f, 500f, 26f);
        hint.gameObject.AddComponent<LocalizedText>().SetKey("stack.reorder_hint");

        var list = NewRect("List", box);
        list.anchorMin = new Vector2(0.5f, 0.5f);
        list.anchorMax = new Vector2(0.5f, 0.5f);
        list.sizeDelta = new Vector2(500f, 280f);
        list.anchoredPosition = new Vector2(0f, 20f);
        listRoot = list;

        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = false;
        layout.spacing = 4f;

        var confirm = Button(box, "stack.reorder_confirm", Confirm);
        Place(confirm.GetComponent<RectTransform>(), 0f, -200f, 300f, 50f);
        confirm.GetComponent<Image>().color = new Color(0.24f, 0.42f, 0.26f);

        panel.SetActive(false);
    }

    private void Rebuild()
    {
        foreach (var row in rows) if (row != null) Destroy(row);
        rows.Clear();

        for (int i = 0; i < order.Count; i++)
        {
            int row = i;

            var entry = NewRect($"Row {i}", listRoot);
            entry.sizeDelta = new Vector2(500f, 44f);
            entry.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f);

            var label = Label(entry, 19f);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(12f, 0f, 96f, 0f);
            label.text = $"{i + 1}.  {names[order[i]]}";

            var up = Button(entry, null, () => Move(row, -1));
            Place(up.GetComponent<RectTransform>(), 200f, 0f, 38f, 36f);
            SetLabel(up, "▲");

            var down = Button(entry, null, () => Move(row, +1));
            Place(down.GetComponent<RectTransform>(), 240f, 0f, 38f, 36f);
            SetLabel(down, "▼");

            rows.Add(entry.gameObject);
        }
    }

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
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
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
        var rect = NewRect((key ?? "Button") + " Button", parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.22f, 0.22f, 0.28f);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var label = Label(rect, 19f);
        Stretch(label.rectTransform);
        if (key != null) label.gameObject.AddComponent<LocalizedText>().SetKey(key);
        return button;
    }

    private static void SetLabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    #endregion
}
