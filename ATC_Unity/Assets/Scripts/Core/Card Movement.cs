using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardMovement : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum CardState { Idle, Hover, Drag }

    [SerializeField] private float selectScale = 1.1f;
    [Tooltip("How fast the hovered card straightens out (higher = snappier).")]
    [SerializeField] private float hoverRotationLerpSpeed = 10f;
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

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        slotScale = rectTransform.localScale;
    }

    public void Init(HandManager hand)
    {
        handManager = hand;
    }

    public void SetSlot(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        slotPosition = position;
        slotRotation = rotation;
        slotScale = scale;
        if (State == CardState.Idle) ApplySlot();
    }

    private void Update()
    {
        switch (State)
        {
            case CardState.Hover:
                TickHover();
                break;
            case CardState.Drag:
                TickDrag();
                break;
        }
    }

    private void TickHover()
    {
        glowEffect.SetActive(true);
        rectTransform.localScale = slotScale * selectScale;
        rectTransform.localRotation = Quaternion.Lerp(rectTransform.localRotation, Quaternion.identity, Time.deltaTime * hoverRotationLerpSpeed);
    }

    private void TickDrag()
    {
        rectTransform.localRotation = Quaternion.identity;
        if (!Mouse.current.leftButton.isPressed) ResolveDrop();
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
        if (!TryGetDropTarget(out PlayArea playArea))
        {
            ReturnToSlot();
            return;
        }

        Vector2 pointer = Mouse.current.position.ReadValue();
        if (!playArea.ContainsScreenPoint(pointer, pressCamera))
        {
            ReturnToSlot();
            return;
        }

        var owner = handManager.Owner;
        var card = GetComponent<CardDisplay>().cardData;
        if (owner.TryPlayCard(gameObject, card))
        {
            State = CardState.Idle;
            glowEffect.SetActive(false);
            return;
        }

        ReturnToSlot();
    }

    private bool TryGetDropTarget(out PlayArea playArea)
    {
        playArea = null;
        var owner = handManager.Owner;
        if (owner == null) return false;
        playArea = owner.playArea;
        return playArea != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (State != CardState.Idle) return;
        if (!OwnerCanInteractNow()) return;
        State = CardState.Hover;
    }

    private bool OwnerCanInteractNow()
    {
        // Guards clones that were never Init'd (e.g. the CardPreview overlay is itself a card
        // prefab instance with an un-owned CardMovement). No hand/owner ⇒ not interactive.
        if (handManager == null || GameManager.Instance == null) return false;
        if (GameManager.Instance.IsActivePlayer(handManager.Owner)) return true;
        var card = GetComponent<CardDisplay>().cardData;
        return card != null && card.speedType == Card.SpeedType.Reflex;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (State == CardState.Hover) ReturnToSlot();
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
            canvasRect, eventData.position, pressCamera, out Vector2 pointerLocal))
        {
            rectTransform.localPosition = dragCardOrigin + (Vector3)(pointerLocal - dragPointerOrigin);
        }
    }
}
