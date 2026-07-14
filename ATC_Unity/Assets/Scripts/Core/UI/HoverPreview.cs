using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardDisplay))]
public class HoverPreview : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        var display = GetComponent<CardDisplay>();
        if (CardPreview.Instance != null && display != null && display.cardData != null)
            CardPreview.Instance.Show(display.cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }
}
