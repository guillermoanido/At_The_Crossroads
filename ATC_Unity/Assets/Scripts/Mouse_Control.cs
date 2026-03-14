using UnityEngine;
using UnityEngine.InputSystem;

public class Mouse_Control : MonoBehaviour
{
    private Transform dragging = null;
    private Vector3 offset;
    private Vector3 originalPosition;

    private void Update()
    {
        Vector3 mouseWorldPosition = GetMouseWorldPosition();

        HandleMousePress(mouseWorldPosition);
        HandleMouseRelease();
        HandleDragging(mouseWorldPosition);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0)
        );

        Debug.Log("Mouse Screen: " + mouseScreenPosition + " | Mouse World: " + mouseWorldPosition);
        return mouseWorldPosition;
    }

    private void HandleMousePress(Vector3 mouseWorldPosition)
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Debug.Log("LEFT CLICK PRESSED at: " + mouseWorldPosition);

        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPosition, Vector2.zero);

        if (hit)
        {
            dragging = hit.transform;
            originalPosition = dragging.position;
            offset = dragging.position - mouseWorldPosition;

            Debug.Log("Started dragging: " + dragging.name + " | Original position: " + originalPosition);
        }
        else
        {
            Debug.Log("No object hit at this position");
        }
    }

    private void HandleMouseRelease()
    {
        if (Mouse.current.leftButton.wasReleasedThisFrame && dragging != null)
        {
            Debug.Log("LEFT CLICK RELEASED. Returning " + dragging.name + " to " + originalPosition);

            dragging.position = originalPosition;
            dragging = null;

            Debug.Log("Drag ended");
        }
    }

    private void HandleDragging(Vector3 mouseWorldPosition)
    {
        if (dragging != null && Mouse.current.leftButton.isPressed)
        {
            dragging.position = mouseWorldPosition + offset;

            Debug.Log("Dragging " + dragging.name + " to: " + dragging.position);
        }
    }
}
