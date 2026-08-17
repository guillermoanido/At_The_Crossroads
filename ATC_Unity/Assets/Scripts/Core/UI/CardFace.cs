using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// One complete card design: its frame art plus every piece of text laid out to suit that frame.
///
/// The two designs put their text in different places, so they cannot share one set of labels.
/// Put a CardFace on each design's root inside the card prefab and CardDisplay will switch between
/// them — which means both are normal, fully editable hierarchies you can lay out by eye.
public class CardFace : MonoBehaviour
{
    [Tooltip("The frame art for this design.")]
    public Image art;

    public TMP_Text nameText;
    public TMP_Text effectText;

    public TMP_Text speedText;
    public Image speedIcon;

    public TMP_Text costText;
    public Image costIcon;

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
