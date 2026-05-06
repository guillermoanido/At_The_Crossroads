using System.Collections.Generic;
using UnityEngine;

// Generic destination for played cards: Discard, Weapon, Armour, Shield,
// Equipment, Accessory, Talent, Aura, etc. Limit-1 zones (Weapon/Armour/Shield)
// can be expanded at runtime via IncreaseMaxSlots when a buff card raises the cap.
public class CardZone : MonoBehaviour
{
    [Tooltip("0 = unlimited")]
    [SerializeField] private int maxSlots = 0;

    [Tooltip("Where cards are parented when placed in this zone. Defaults to this transform.")]
    [SerializeField] private Transform anchor;

    public List<GameObject> Cards { get; } = new List<GameObject>();

    public bool IsFull => maxSlots > 0 && Cards.Count >= maxSlots;

    public void AddCard(GameObject card)
    {
        var parent = anchor != null ? anchor : transform;
        card.transform.SetParent(parent, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;
        Cards.Add(card);
    }

    public void IncreaseMaxSlots(int delta)
    {
        if (maxSlots > 0) maxSlots += delta;
    }
}
