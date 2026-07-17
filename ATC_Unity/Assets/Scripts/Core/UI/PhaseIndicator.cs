using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseIndicator : MonoBehaviour
{
    [Tooltip("The phase this indicator lights up for. (Upkeep happens at the very start of the Draw phase.)")]
    [SerializeField] private GameManager.GamePhase phase;

    [Tooltip("Image tinted for this indicator. Auto-found on this object or its children if left empty.")]
    [SerializeField] private Image image;

    [Tooltip("Optional label tinted alongside the image.")]
    [SerializeField] private TMP_Text label;

    [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private void Reset() => FindTargets();

    private void Awake()
    {
        FindTargets();
        if (image == null)
            Debug.LogWarning($"[PhaseIndicator] '{name}' has no Image to tint — add an Image (under a Canvas) on this object or a child.");
    }

    private void FindTargets()
    {
        if (image == null) image = GetComponentInChildren<Image>(true);
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        bool active = gm.CurrentPhase == phase;
        if (image != null) image.color = active ? activeColor : inactiveColor;
        if (label != null) label.color = active ? activeTextColor : inactiveTextColor;
    }
}
