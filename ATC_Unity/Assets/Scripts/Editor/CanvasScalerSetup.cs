#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Makes every screen-space UI in the game scale with the window instead of being pinned to a
/// fixed pixel size.
///
/// A CanvasScaler left on ConstantPixelSize keeps its UI at the same pixel dimensions whatever the
/// window does, so shrinking the window crops the interface instead of scaling it. Switching to
/// ScaleWithScreenSize against a 1920x1080 reference makes the whole UI grow and shrink with the
/// window, which is what a resizable build needs.
///
/// MatchWidthOrHeight is set to 0.5 rather than 0: matching width alone keeps the horizontal
/// layout but lets a short, wide window push content off the top and bottom. Splitting the
/// difference keeps everything on screen at any aspect ratio.
public static class CanvasScalerSetup
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float Match = 0.5f;

    [MenuItem("ATC/Make UI Resizable")]
    public static void FixAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[UI] Cancelled — nothing changed.");
            return;
        }

        string activeScene = EditorSceneManager.GetActiveScene().path;
        var scenePaths = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && !string.IsNullOrEmpty(s.path)) scenePaths.Add(s.path);

        if (scenePaths.Count == 0)
        {
            Debug.LogWarning("[UI] No scenes in Build Settings — nothing to do.");
            return;
        }

        int scenesChanged = 0, canvasesChanged = 0;

        foreach (var path in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int fixedHere = FixOpenScene();

            if (fixedHere > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesChanged++;
                canvasesChanged += fixedHere;
            }
            Debug.Log($"[UI] {System.IO.Path.GetFileNameWithoutExtension(path)}: {fixedHere} canvas(es) updated.");
        }

        if (!string.IsNullOrEmpty(activeScene)) EditorSceneManager.OpenScene(activeScene, OpenSceneMode.Single);

        Debug.Log($"[UI] Done — {canvasesChanged} canvas(es) across {scenesChanged} scene(s) now scale with the window " +
                  $"({ReferenceWidth}x{ReferenceHeight}, match {Match}).");
    }

    /// Same fix, but only on whatever is open right now. Handy while iterating on one scene.
    [MenuItem("ATC/Make UI Resizable (Open Scene Only)")]
    public static void FixCurrentScene()
    {
        int n = FixOpenScene();
        if (n > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[UI] {n} canvas(es) updated in the open scene — save it to keep the change.");
        }
        else Debug.Log("[UI] Every canvas in the open scene already scales correctly.");
    }

    private static int FixOpenScene()
    {
        int changed = 0;

        foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var canvas = scaler.GetComponent<Canvas>();

            // World-space canvases size themselves in world units; the scaler's screen settings do
            // nothing there, so leave them exactly as authored.
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) continue;

            bool needsChange = scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize
                            || !Mathf.Approximately(scaler.referenceResolution.x, ReferenceWidth)
                            || !Mathf.Approximately(scaler.referenceResolution.y, ReferenceHeight)
                            || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                            || !Mathf.Approximately(scaler.matchWidthOrHeight, Match);

            if (!needsChange) continue;

            Undo.RecordObject(scaler, "Make UI Resizable");

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Match;

            EditorUtility.SetDirty(scaler);
            changed++;

            Debug.Log($"[UI] '{scaler.gameObject.name}' now scales with the window.");
        }

        return changed;
    }
}
#endif
