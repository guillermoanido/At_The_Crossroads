using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardMovement : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum CardState { Idle, Hover, Drag }

    [SerializeField] private float selectScale = 1.1f;
    [SerializeField] private GameObject glowEffect;

    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private HandManager handManager;
    private Camera pressCamera;

    private Vector2 dragPointerOrigin;
    private Vector3 dragCardOrigin;
    private Vector3 slotPosition;
    private Quaternion slotRotation;
    private Vector3 slotScale;

    public CardState State { get; private set; } = CardState.Idle;
    public Player Owner => handManager != null ? handManager.Owner : null;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        slotScale = rectTransform.localScale;
    }

    public void Init(HandManager hm)
    {
        handManager = hm;
    }

    void Update()
    {
        switch (State)
        {
            case CardState.Hover:
                glowEffect.SetActive(true);
                rectTransform.localScale = slotScale * selectScale;
                rectTransform.localRotation = Quaternion.Lerp(rectTransform.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                break;

            case CardState.Drag:
                rectTransform.localRotation = Quaternion.identity;
                if (!Mouse.current.leftButton.isPressed)
                    ResolveDrop();
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
        ApplySlot();
    }

    private void ResolveDrop()
    {
        var owner = handManager.Owner;
        if (owner == null)
        {
            Debug.LogWarning("[Drop] Card has no owner — HandManager.Owner not set.");
            ReturnToSlot();
            return;
        }
        if (owner.playArea == null)
        {
            Debug.LogWarning($"[Drop] {owner.name} has no PlayArea assigned in the Inspector.");
            ReturnToSlot();
            return;
        }

        var screenPos = Mouse.current.position.ReadValue();
        if (!owner.playArea.ContainsScreenPoint(screenPos, pressCamera))
        {
            Debug.Log("[Drop] Released outside PlayArea — returning to hand.");
            ReturnToSlot();
            return;
        }

        var card = GetComponent<CardDisplay>().cardData;
        if (owner.TryPlayCard(gameObject, card))
        {
            State = CardState.Idle;
            glowEffect.SetActive(false);
            return;
        }

        ReturnToSlot();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (State == CardState.Idle && GameManager.Instance.IsActivePlayer(handManager.Owner))
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
            rectTransform.localPosition = dragCardOrigin + (Vector3)(localPos - dragPointerOrigin);
        }
    }
}
