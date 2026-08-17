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

    private Sprite frame;
    private bool capturedFrame;

    /// This design's frame art, remembered from the prefab.
    ///
    /// It has to be cached: turning a card face-down replaces the art Image's sprite with the card
    /// back, so reading the live sprite after that returns the BACK, and the frame would be lost
    /// for good — which is exactly how face-down cards came back showing the back art.
    public Sprite Frame
    {
        get
        {
            CaptureFrame();
            return frame;
        }
    }

    private void Awake() => CaptureFrame();

    private void CaptureFrame()
    {
        if (capturedFrame || art == null) return;
        frame = art.sprite;
        capturedFrame = true;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
