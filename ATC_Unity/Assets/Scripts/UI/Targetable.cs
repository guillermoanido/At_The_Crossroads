using UnityEngine;

[RequireComponent(typeof(CardDisplay))]
public class Targetable : MonoBehaviour
{
    [SerializeField] private GameObject highlight;

    public Card Data => GetComponent<CardDisplay>().cardData;
    public Player Owner => GetComponent<CardMovement>()?.Owner;
    public CardZone Zone => GetComponentInParent<CardZone>();

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }
}
