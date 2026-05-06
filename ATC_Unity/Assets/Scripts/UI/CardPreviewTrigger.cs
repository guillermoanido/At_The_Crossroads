using UnityEngine;
using UnityEngine.EventSystems;

// Attach to the card prefab. Fires the big preview on hover for ANY face-up card —
// hand or played — independent of CardMovement (which gets disabled after play).
[RequireComponent(typeof(CardDisplay))]
public class CardPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardDisplay display;

    private void Awake()
    {
        display = GetComponent<CardDisplay>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardPreview.Instance == null || display == null || !display.IsFaceUp) return;
        CardPreview.Instance.Show(display.cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }
}
