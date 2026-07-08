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

    // Your own hand is only previewable on your turn; opponent hand cards are face-down
    // (so IsFaceUp already blocks them). Cards on the board are previewable by anyone.
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
            /*var owner = GetOwner();
            var data = GetComponent<CardDisplay>()?.cardData;
            if (owner != null && data != null && data.FirstActivated() != null)
                owner.TryActivateCard(gameObject, data);   // pays cost, taps, resolves the ability
            else
                GetComponent<CardTapState>().Toggle();     // no activated ability → just a manual tap
            */return;
        }

        var owner = GetOwner();
        if (owner == null) return;

        // You can only send your OWN board cards away, and only while you hold priority.
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
