using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardZone : MonoBehaviour, IPointerClickHandler
{
    public enum ZoneKind { Other, Discard, Weapon, Shield, Armour, Equipment, Accessory, Talent, Aura, Exile }

    [Tooltip("Identifies this zone for the GameManager's per-zone scale sliders.")]
    [SerializeField] private ZoneKind kind = ZoneKind.Other;

    public ZoneKind Kind => kind;

    [Tooltip("0 = unlimited")]
    [SerializeField] private int maxSlots = 0;

    [Tooltip("Where cards are parented when placed in this zone. Defaults to this transform.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Per-card offset for stacking. Set (0,0) for true overlap, e.g. (3,-3) for a visible diagonal stack like a discard pile.")]
    [SerializeField] private Vector2 stackOffset = Vector2.zero;

    [Tooltip("Maximum horizontal extent the stack may occupy. 0 = unlimited. When the total exceeds this, the per-card x-offset compresses so all cards fit.")]
    [SerializeField] private float maxLayoutWidth = 0f;

    [Tooltip("Maximum vertical extent the stack may occupy. 0 = unlimited.")]
    [SerializeField] private float maxLayoutHeight = 0f;

    [Tooltip("If true, left-clicking the zone opens DiscardBrowser to inspect its cards. Set on Discard / Exile zones.")]
    [SerializeField] private bool browseOnClick = false;

    [Tooltip("The player who owns this zone. Used by DiscardBrowser to return cards to the right hand.")]
    [SerializeField] private Player owner;

    public Player Owner => owner;
    public List<GameObject> Cards { get; } = new List<GameObject>();
    public bool IsFull => maxSlots > 0 && Cards.Count >= maxSlots;

    public void AddCard(GameObject card)
    {
        var parent = anchor != null ? anchor : transform;
        card.transform.SetParent(parent, false);
        card.transform.localRotation = Quaternion.identity;
        Cards.Add(card);
        RefreshLayout();
    }

    public void RemoveCard(GameObject card)
    {
        Cards.Remove(card);
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        Vector3 scale = Vector3.one * ConfiguredCardScale();
        Vector2 offset = EffectiveStackOffset(Cards.Count);
        for (int i = 0; i < Cards.Count; i++)
        {
            var t = Cards[i].transform;
            t.localPosition = (Vector3)(offset * i);
            if (t.localScale != scale) t.localScale = scale;
        }
    }

    public void IncreaseMaxSlots(int delta)
    {
        if (maxSlots > 0) maxSlots += delta;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!browseOnClick) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (DiscardBrowser.Instance != null) DiscardBrowser.Instance.Open(this);
    }

    private Vector2 EffectiveStackOffset(int totalCards)
    {
        Vector2 offset = stackOffset;
        if (totalCards <= 1) return offset;

        offset.x = CompressAxis(offset.x, maxLayoutWidth, totalCards);
        offset.y = CompressAxis(offset.y, maxLayoutHeight, totalCards);
        return offset;
    }

    private static float CompressAxis(float axisOffset, float maxExtent, int totalCards)
    {
        if (maxExtent <= 0f || axisOffset == 0f) return axisOffset;
        float needed = Mathf.Abs(axisOffset) * (totalCards - 1);
        if (needed <= maxExtent) return axisOffset;
        return Mathf.Sign(axisOffset) * (maxExtent / (totalCards - 1));
    }

    private float ConfiguredCardScale()
        => GameManager.Instance != null ? GameManager.Instance.GetScaleForZone(kind) : 1f;
}
