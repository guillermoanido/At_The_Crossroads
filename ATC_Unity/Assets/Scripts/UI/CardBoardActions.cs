using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardDisplay))]
[RequireComponent(typeof(CardTapState))]
[RequireComponent(typeof(Targetable))]
public class CardBoardActions : MonoBehaviour, IPointerClickHandler
{
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
            GetComponent<CardTapState>().Toggle();
            return;
        }

        var owner = GetOwner();
        if (owner == null) return;

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
