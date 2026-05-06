using UnityEngine;

// MTG-Arena style preview: a single fixed-position card clone on the canvas
// that mirrors whatever card is currently being hovered. Place one of these
// in the scene and assign its display.
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
        Hide();
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
        display.SetFaceUp(true); // calls UpdateCardDisplay internally, after cardData is set
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
