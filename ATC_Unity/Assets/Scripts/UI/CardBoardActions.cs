using UnityEngine;
using UnityEngine.EventSystems;

// Attach to the card prefab. Right-clicking any card (in hand or on the board)
// sends it to its owner's discard zone — playtest-friendly removal.
[RequireComponent(typeof(CardDisplay))]
public class CardBoardActions : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        var movement = GetComponent<CardMovement>();
        var owner = movement != null ? movement.Owner : null;
        if (owner == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.SendToDiscard(gameObject);
        }
        else if (eventData.button == PointerEventData.InputButton.Middle)
        {
            owner.SendToExile(gameObject);
        }
        else
        {
            return;
        }

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }
}
