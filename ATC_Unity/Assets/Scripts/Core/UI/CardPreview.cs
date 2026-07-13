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
        display.SetFaceUp(true);
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
