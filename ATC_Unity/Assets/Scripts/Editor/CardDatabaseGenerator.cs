#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Builds/refreshes Resources/CardDatabase.asset from every Card asset in the project. The list
/// is ordered by asset GUID so it is STABLE across rebuilds — the same card always lands on the
/// same index, which is what lets an id mean the same card on the host and the client.
///
/// Run this once now, and again any time you add or remove cards.
public static class CardDatabaseGenerator
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string AssetPath = "Assets/Resources/CardDatabase.asset";

    [MenuItem("ATC/Build Card Database")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var cards = AssetDatabase.FindAssets("t:Card")
            .OrderBy(guid => guid)
            .Select(guid => AssetDatabase.LoadAssetAtPath<Card>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(c => c != null)
            .ToList();

        var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(AssetPath);
        bool isNew = db == null;
        if (isNew) db = ScriptableObject.CreateInstance<CardDatabase>();

        db.cards = cards;

        if (isNew) AssetDatabase.CreateAsset(db, AssetPath);
        else EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CardDatabase] Built with {cards.Count} cards → {AssetPath}");
    }
}
#endif
