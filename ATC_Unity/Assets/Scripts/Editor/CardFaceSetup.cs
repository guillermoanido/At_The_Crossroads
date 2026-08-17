#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Turns the card prefab's two copies of the card canvas into the two switchable designs:
/// one laid out for the permanent frame, one for the transient frame.
///
/// It only names them, adds a CardFace to each and fills in the references. Your layout — where the
/// text sits, how big it is, what art each uses — is never touched, so this is safe to re-run after
/// you have restyled either design.
public static class CardFaceSetup
{
    private const string PrefabPath = "Assets/Prefabs/CardPrefab.prefab";
    private const string PermanentSprite = "Assets/Sprites/Channel_12.5x17.5.png";
    private const string TransientSprite = "Assets/Sprites/Reflex_x2_CFondo.png";

    private const string PermanentName = "Face Permanent";
    private const string TransientName = "Face Transient";

    [MenuItem("ATC/Build Two Card Faces")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var display = root.GetComponent<CardDisplay>();
            if (display == null)
            {
                Debug.LogError("[CardFaces] The card prefab has no CardDisplay.");
                return;
            }

            if (!FindCanvases(root, out var permanentRoot, out var transientRoot)) return;

            permanentRoot.name = PermanentName;
            transientRoot.name = TransientName;

            display.permanentFace = BuildFace(permanentRoot);
            display.transientFace = BuildFace(transientRoot);

            // Only one design is ever on screen; CardDisplay switches them as each card renders.
            permanentRoot.SetActive(true);
            transientRoot.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[CardFaces] '{PermanentName}' and '{TransientName}' wired up. " +
                      "Lay each one out to suit its frame — they are independent now.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The two designs are the prefab's two card-canvas children. Tell them apart by the frame art
    // already on each, falling back to first/second so a fresh duplicate still works.
    private static bool FindCanvases(GameObject root, out GameObject permanent, out GameObject transient)
    {
        permanent = transient = null;

        var canvases = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in root.transform)
            if (child.GetComponentInChildren<Image>(true) != null) canvases.Add(child.gameObject);

        if (canvases.Count < 2)
        {
            Debug.LogError($"[CardFaces] Expected two card canvases under the prefab, found {canvases.Count}. " +
                           "Duplicate the card canvas child first.");
            return false;
        }

        var permanentArt = AssetDatabase.LoadAssetAtPath<Sprite>(PermanentSprite);
        var transientArt = AssetDatabase.LoadAssetAtPath<Sprite>(TransientSprite);

        foreach (var canvas in canvases)
        {
            var art = FindImage(canvas, "Card Image");
            if (art == null) continue;

            if (art.sprite == permanentArt && permanent == null) permanent = canvas;
            else if (art.sprite == transientArt && transient == null) transient = canvas;
        }

        // Whatever wasn't identified by its art just takes the remaining slot.
        foreach (var canvas in canvases)
        {
            if (canvas == permanent || canvas == transient) continue;
            if (permanent == null) permanent = canvas;
            else if (transient == null) transient = canvas;
        }

        if (permanent == null || transient == null)
        {
            Debug.LogError("[CardFaces] Couldn't tell the two designs apart.");
            return false;
        }

        // Make sure each really is showing its own frame.
        SetSprite(permanent, permanentArt);
        SetSprite(transient, transientArt);
        return true;
    }

    private static void SetSprite(GameObject face, Sprite sprite)
    {
        var art = FindImage(face, "Card Image");
        if (art != null && sprite != null) art.sprite = sprite;
    }

    private static CardFace BuildFace(GameObject faceRoot)
    {
        var face = faceRoot.GetComponent<CardFace>();
        if (face == null) face = faceRoot.AddComponent<CardFace>();

        face.art        = FindImage(faceRoot, "Card Image");
        face.nameText   = FindText(faceRoot, "Name Text");
        face.effectText = FindText(faceRoot, "Effect Text");
        face.speedText  = FindText(faceRoot, "Speed Value");
        face.costText   = FindText(faceRoot, "Cost Value");
        face.speedIcon  = FindImage(faceRoot, "Speed Image");
        face.costIcon   = FindImage(faceRoot, "Cost Image");

        WarnIfMissing(faceRoot.name, "Card Image", face.art);
        WarnIfMissing(faceRoot.name, "Name Text", face.nameText);
        WarnIfMissing(faceRoot.name, "Effect Text", face.effectText);
        return face;
    }

    private static void WarnIfMissing(string faceName, string childName, Object found)
    {
        if (found == null) Debug.LogWarning($"[CardFaces] '{faceName}' has no child called '{childName}'.");
    }

    private static Image FindImage(GameObject root, string childName)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            if (image.gameObject.name == childName) return image;
        return null;
    }

    private static TMP_Text FindText(GameObject root, string childName)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text.gameObject.name == childName) return text;
        return null;
    }
}
#endif
