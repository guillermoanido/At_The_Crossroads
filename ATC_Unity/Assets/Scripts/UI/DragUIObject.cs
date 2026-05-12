using UnityEngine;
using UnityEngine.EventSystems;

public class DragUIObject : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    public float movementSensitivity = 1.0f;

    private RectTransform rectTransform;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Vector2 pointerStartLocal;
    private Vector3 panelStartLocal;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out pointerStartLocal);
        panelStartLocal = rectTransform.localPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerLocal))
            return;

        pointerLocal /= canvas.scaleFactor;
        Vector3 offset = (pointerLocal - pointerStartLocal) * movementSensitivity;
        rectTransform.localPosition = panelStartLocal + offset;
    }
}
