using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardDisplay))]
[RequireComponent(typeof(CardTapState))]
[RequireComponent(typeof(Targetable))]
public class CardBoardActions : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardPreview.Instance == null) return;

        var display = GetComponent<CardDisplay>();
        if (display == null || !display.IsFaceUp || display.cardData == null) return;
        if (!IsViewableByActivePlayer()) return;

        CardPreview.Instance.Show(display.cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    private bool IsViewableByActivePlayer()
    {
        var owner = GetOwner();
        if (owner == null || owner.handManager == null) return true;

        bool isInOwnersHand = owner.handManager.cardsInHand.Contains(gameObject);
        if (isInOwnersHand)
            return GameManager.Instance != null && GameManager.Instance.IsActivePlayer(owner);

        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var targeting = TargetingService.Instance;
        if (targeting != null && targeting.IsActive)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                targeting.TryChoose(GetComponent<Targetable>());
            else if (eventData.button == PointerEventData.InputButton.Right)
                targeting.Cancel();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount >= 2)
            {
                var clicker = GetOwner();
                var card = GetComponent<CardDisplay>()?.cardData;
                if (clicker != null && card != null && card.FirstActivated() != null)
                    clicker.TryActivateCard(gameObject, card);
            }
            return;
        }

        var owner = GetOwner();
        if (owner == null) return;

        if (GameManager.Instance != null && !GameManager.Instance.IsControllingPlayer(owner)) return;

        if (eventData.button == PointerEventData.InputButton.Right)
            owner.SendToDiscard(gameObject);
        else if (eventData.button == PointerEventData.InputButton.Middle)
            owner.SendToExile(gameObject);
        else
            return;

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    private Player GetOwner()
    {
        var movement = GetComponent<CardMovement>();
        return movement != null ? movement.Owner : null;
    }
}
