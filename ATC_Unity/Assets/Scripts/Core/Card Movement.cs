using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardMovement : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum CardState { Idle, Hover, Drag, Play }

    [SerializeField] private float selectScale = 1.1f;
    [SerializeField] private float playZoneY = 400f;
    [SerializeField] private Vector3 playPosition;
    [SerializeField] private GameObject glowEffect;
    [SerializeField] private GameObject playArrow;

    private RectTransform rectTransform;
    private Canvas canvas;
    private RectTransform canvasRect;
    private HandManager handManager;
    private Camera pressCamera;

    private Vector2 dragPointerOrigin;
    private Vector3 dragCardOrigin;
    private Vector3 slotPosition;
    private Quaternion slotRotation;
    private Vector3 slotScale;

    public CardState State { get; private set; } = CardState.Idle;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();
        slotScale = rectTransform.localScale;
    }

    public void Init(HandManager hm)
    {
        handManager = hm;
    }

    void Update()
    {
        float mouseY = Mouse.current.position.ReadValue().y;

        switch (State)
        {
            case CardState.Hover:
                glowEffect.SetActive(true);
                rectTransform.localScale = slotScale * selectScale;
                rectTransform.localRotation = Quaternion.Lerp(rectTransform.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                break;

            case CardState.Drag:
                rectTransform.localRotation = Quaternion.identity;
                if (mouseY > playZoneY)
                {
                    State = CardState.Play;
                    playArrow.SetActive(true);
                }
                else if (!Mouse.current.leftButton.isPressed)
                    ReturnToSlot();
                break;

            case CardState.Play:
                rectTransform.localPosition = playPosition;
                rectTransform.localRotation = Quaternion.identity;
                if (mouseY < playZoneY)
                {
                    State = CardState.Drag;
                    playArrow.SetActive(false);
                    dragCardOrigin = playPosition;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, Mouse.current.position.ReadValue(), pressCamera, out dragPointerOrigin);
                }
                else if (!Mouse.current.leftButton.isPressed)
                    PlayCard();
                break;
        }
    }

    // Called by HandManager whenever it recalculates the fan layout.
    // Applies immediately only when the card is idle; otherwise just stores the slot
    // so ReturnToSlot() can snap back to the right position after a drag.
    public void SetSlot(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        slotPosition = position;
        slotRotation = rotation;
        slotScale = scale;
        if (State == CardState.Idle)
            ApplySlot();
    }

    private void ApplySlot()
    {
        rectTransform.localPosition = slotPosition;
        rectTransform.localRotation = slotRotation;
        rectTransform.localScale = slotScale;
    }

    private void ReturnToSlot()
    {
        State = CardState.Idle;
        glowEffect.SetActive(false);
        playArrow.SetActive(false);
        ApplySlot();
    }

    private void PlayCard()
    {
        handManager.RemoveCardFromHand(gameObject);
        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (State == CardState.Idle)
            State = CardState.Hover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (State == CardState.Hover)
            ReturnToSlot();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (State != CardState.Hover) return;

        State = CardState.Drag;
        pressCamera = eventData.pressEventCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, pressCamera, out dragPointerOrigin);
        dragCardOrigin = rectTransform.localPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (State != CardState.Drag) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, pressCamera, out Vector2 localPos))
        {
            localPos /= canvas.scaleFactor;
            rectTransform.localPosition = dragCardOrigin + (Vector3)(localPos - dragPointerOrigin);
        }
    }
}
