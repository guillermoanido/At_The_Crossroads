using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardZone : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("0 = unlimited")]
    [SerializeField] private int maxSlots = 0;

    [Tooltip("Where cards are parented when placed in this zone. Defaults to this transform.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Per-card offset for stacking. Set (0,0) for true overlap, e.g. (3,-3) for a visible diagonal stack like a discard pile.")]
    [SerializeField] private Vector2 stackOffset = Vector2.zero;

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
        float scale = GameManager.Instance != null ? GameManager.Instance.ZoneCardScale : 1f;
        card.transform.localPosition = (Vector3)(stackOffset * Cards.Count);
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one * scale;
        Cards.Add(card);
    }

    public void RemoveCard(GameObject card)
    {
        Cards.Remove(card);
        // Re-snap remaining cards to their stack offsets so the pile stays tidy after a removal from the middle.
        for (int i = 0; i < Cards.Count; i++)
            Cards[i].transform.localPosition = (Vector3)(stackOffset * i);
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
}
