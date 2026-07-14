using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class DebugTools : MonoBehaviour
{
    [Tooltip("Hover any card (hand or board) and press this key to send it to its owner's exile pile.")]
    [SerializeField] private Key exileKey = Key.E;

    [Tooltip("Hover any card and press this to send it to its owner's discard pile.")]
    [SerializeField] private Key discardKey = Key.Q;

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[exileKey].wasPressedThisFrame) RemoveHoveredCard(exile: true);
        else if (Keyboard.current[discardKey].wasPressedThisFrame) RemoveHoveredCard(exile: false);
    }

    private void RemoveHoveredCard(bool exile)
    {
        var card = CardUnderPointer();
        if (card == null) return;

        var owner = card.GetComponent<CardMovement>()?.Owner;
        if (owner == null) return;

        if (exile) owner.SendToExile(card);
        else owner.SendToDiscard(card);

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    private static GameObject CardUnderPointer()
    {
        if (EventSystem.current == null || Mouse.current == null) return null;

        var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);

        foreach (var hit in hits)
        {
            var movement = hit.gameObject.GetComponentInParent<CardMovement>();
            if (movement != null) return movement.gameObject;
        }
        return null;
    }
}
