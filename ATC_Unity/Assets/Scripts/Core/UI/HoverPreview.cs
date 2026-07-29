using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardDisplay))]
public class HoverPreview : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        var display = GetComponent<CardDisplay>();
        // Only preview FACE-UP cards. A face-down card is the opponent's (or a placeholder), so this
        // keeps the enlarged card display private to its owner — the opponent's faces never leak,
        // even on the host where their cards are real objects merely flipped face-down.
        if (CardPreview.Instance != null && display != null && display.IsFaceUp && display.cardData != null)
            CardPreview.Instance.Show(display.cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }
}
