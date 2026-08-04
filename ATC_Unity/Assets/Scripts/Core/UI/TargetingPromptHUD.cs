using UnityEngine;

/// Fallback on-screen prompt for the targeting step, added automatically by TargetingService when
/// no Prompt Label is wired in the scene. Without it a build would highlight the legal cards but
/// never say what the game is waiting for. Assign TargetingService's Prompt Label / Prompt Root to
/// replace this with proper UI — this component then never appears.
[DisallowMultipleComponent]
public class TargetingPromptHUD : MonoBehaviour
{
    private const int Height = 34;
    private const int Margin = 8;

    private GUIStyle style;

    private void OnGUI()
    {
        var service = TargetingService.Instance;
        if (service == null || !service.IsActive || string.IsNullOrEmpty(service.Prompt)) return;

        var box = new Rect(Margin, Screen.height - Height - Margin, Screen.width - Margin * 2, Height);
        GUI.Box(box, GUIContent.none);
        GUI.Label(box, service.Prompt, Style);
    }

    private GUIStyle Style
    {
        get
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = new Color(1f, 0.88f, 0.35f);
            }
            return style;
        }
    }
}
