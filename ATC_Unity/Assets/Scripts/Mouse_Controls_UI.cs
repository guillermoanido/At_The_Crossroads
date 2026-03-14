using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
public class Mouse_Controls_UI : MonoBehaviour
{
    public GraphicRaycaster raycaster;

    private RectTransform dragging = null;
    private Vector2 offset;
    private Vector2 originalPosition;

    void Update()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        HandleMousePress(mousePosition);
        HandleMouseRelease();
        HandleDragging(mousePosition);
    }

    void HandleMousePress(Vector2 mousePosition)
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        if (results.Count > 0)
        {
            dragging = results[0].gameObject.GetComponent<RectTransform>();

            originalPosition = dragging.anchoredPosition;
            offset = dragging.anchoredPosition - mousePosition;
        }
    }

    void HandleMouseRelease()
    {
        if (Mouse.current.leftButton.wasReleasedThisFrame && dragging != null)
        {
            dragging.anchoredPosition = originalPosition;
            dragging = null;
        }
    }

    void HandleDragging(Vector2 mousePosition)
    {
        if (dragging != null && Mouse.current.leftButton.isPressed)
        {
            dragging.position = mousePosition + offset;
        }
    }
}
