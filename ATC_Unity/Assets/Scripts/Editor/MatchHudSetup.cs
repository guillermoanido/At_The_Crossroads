#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Puts a real, editable Game Log HUD into the match scene.
///
/// GameLogHUD builds itself at runtime if nothing is there, which is handy but leaves nothing to
/// select in the editor. This creates the objects for you and wires them up, so the banner and the
/// event feed can be moved, recoloured and restyled like any other UI.
public static class MatchHudSetup
{
    private const string ScenePath = "Assets/Scenes/ATC.unity";
    private const string RootName = "Game Log HUD";

    [MenuItem("ATC/Setup Match HUD")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = FindMainCanvas();
        if (canvas == null)
        {
            Debug.LogError("[MatchHUD] No canvas in the match scene to attach the HUD to.");
            return;
        }

        var existing = canvas.transform.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var root = NewRect(RootName, canvas.transform);
        Stretch(root);
        root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;   // never eat a click

        var hud = root.gameObject.AddComponent<GameLogHUD>();
        var instruction = BuildInstruction(root, out Image plate);
        var feed = BuildFeed(root);

        var so = new SerializedObject(hud);
        so.FindProperty("instructionLabel").objectReferenceValue = instruction;
        so.FindProperty("instructionBackground").objectReferenceValue = plate;
        so.FindProperty("feedLabel").objectReferenceValue = feed;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetAsLastSibling();

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MatchHUD] '{RootName}' added under '{canvas.name}'. Select it to restyle the " +
                  "instruction banner and the event feed.");
    }

    // The board's own canvas, not the little phase-counter one — pick the biggest by child count.
    private static Canvas FindMainCanvas()
    {
        Canvas best = null;
        int bestChildren = -1;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.GetComponentInParent<CardDisplay>() != null) continue;   // a card's own canvas

            int children = canvas.GetComponentsInChildren<Transform>(true).Length;
            if (children <= bestChildren) continue;

            best = canvas;
            bestChildren = children;
        }
        return best;
    }

    private static TMP_Text BuildInstruction(Transform root, out Image plate)
    {
        var banner = NewRect("Instruction", root);
        banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0f);
        banner.pivot = new Vector2(0.5f, 0f);
        banner.sizeDelta = new Vector2(900f, 54f);
        banner.anchoredPosition = new Vector2(0f, 96f);

        plate = banner.gameObject.AddComponent<Image>();
        plate.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);
        plate.raycastTarget = false;

        var label = NewLabel(banner, 24f, TextAlignmentOptions.Center);
        label.color = new Color(1f, 0.88f, 0.35f);
        label.fontStyle = FontStyles.Bold;
        return label;
    }

    private static TMP_Text BuildFeed(Transform root)
    {
        var feed = NewRect("Feed", root);
        feed.anchorMin = feed.anchorMax = new Vector2(0f, 1f);
        feed.pivot = new Vector2(0f, 1f);
        feed.sizeDelta = new Vector2(520f, 200f);
        feed.anchoredPosition = new Vector2(24f, -24f);

        var label = NewLabel(feed, 18f, TextAlignmentOptions.TopLeft);
        label.color = new Color(1f, 1f, 1f, 0.9f);
        return label;
    }

    #region Building blocks

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
#endif
