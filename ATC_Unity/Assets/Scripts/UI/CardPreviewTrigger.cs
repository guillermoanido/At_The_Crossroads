using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardDisplay))]
public class CardPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardDisplay display;

    private void Awake() => display = GetComponent<CardDisplay>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardPreview.Instance == null || display == null || !display.IsFaceUp) return;
        if (!IsViewableByActivePlayer()) return;
        CardPreview.Instance.Show(display.cardData);
    }

    private bool IsViewableByActivePlayer()
    {
        var movement = GetComponent<CardMovement>();
        var owner = movement != null ? movement.Owner : null;
        if (owner == null) return true;

        bool isInOwnersHand = owner.handManager.cardsInHand.Contains(gameObject);
        if (isInOwnersHand) return GameManager.Instance.IsActivePlayer(owner);

        return true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }
}
