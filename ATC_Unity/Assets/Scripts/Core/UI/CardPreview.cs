using UnityEngine;

public class CardPreview : MonoBehaviour
{
    public static CardPreview Instance { get; private set; }

    [Tooltip("The GameObject toggled on/off as the preview shows/hides. Usually the card clone itself.")]
    [SerializeField] private GameObject root;

    [Tooltip("CardDisplay on the preview clone — gets its cardData swapped on hover.")]
    [SerializeField] private CardDisplay display;

    private void Awake()
    {
        Instance = this;
        MakeNonBlocking();
        Hide();
    }

    // The preview must never receive pointer events. If it does, showing it under the cursor steals
    // the hover from the card that asked for it, which hides the preview, which hands the hover
    // back — a flicker loop that reads as the card display redrawing over and over.
    private void MakeNonBlocking()
    {
        if (root == null) return;

        var group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    public void Show(Card card)
    {
        if (card == null) { Hide(); return; }
        if (display == null)
        {
            Debug.LogWarning("[CardPreview] No CardDisplay assigned in Inspector.");
            return;
        }

        display.cardData = card;
        display.SetFaceUp(true);
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
