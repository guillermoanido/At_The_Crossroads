using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    public DeckManager deckManager;

    public GameObject cardPrefab;
    public Transform handPosition;

    public float fanSpread = 5f;
    public float cardSpacing = 100f;
    public float verticalSpacing = 10f;

    [Header("Sizing")]
    [Tooltip("Visual scale of cards in this hand. 1 = prefab size.")]
    [Range(0.2f, 1.5f)] public float cardScale = 1f;

    [Tooltip("Total horizontal space the fan may occupy. When the fan would exceed this, spacing shrinks so cards overlap instead of running off-screen.")]
    public float maxHandWidth = 800f;

    [SerializeField] private bool isFlipped = false;
    [SerializeField] private bool showFaceUp = true;
    [SerializeField] private int maxHandSize = 10;

    public Player Owner { get; private set; }
    public List<GameObject> cardsInHand = new List<GameObject>();
    public bool IsHandFull => cardsInHand.Count >= maxHandSize;

    public void SetOwner(Player player) => Owner = player;

    public void AddCardToHand(Card cardData)
    {
        var newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        var display = newCard.GetComponent<CardDisplay>();
        display.cardData = cardData;
        display.SetFaceUp(showFaceUp);

        newCard.GetComponent<CardMovement>().Init(this);

        LayoutHand();
    }

    public void RemoveCardFromHand(GameObject card)
    {
        cardsInHand.Remove(card);
        LayoutHand();
    }

    public void RefreshLayout() => LayoutHand();

    private void LayoutHand()
    {
        int count = cardsInHand.Count;
        if (count == 0) return;

        Vector3 scale = Vector3.one * cardScale;

        if (count == 1)
        {
            cardsInHand[0].GetComponent<CardMovement>().SetSlot(Vector3.zero, Quaternion.identity, scale);
            return;
        }

        float spacing = EffectiveSpacing(count);
        for (int i = 0; i < count; i++)
        {
            var slot = SlotFor(i, count, spacing);
            cardsInHand[i].GetComponent<CardMovement>().SetSlot(slot.position, slot.rotation, scale);
        }
    }

    private float EffectiveSpacing(int count)
    {
        float total = (count - 1) * cardSpacing;
        return total > maxHandWidth ? maxHandWidth / (count - 1) : cardSpacing;
    }

    private (Vector3 position, Quaternion rotation) SlotFor(int index, int count, float spacing)
    {
        float flip = isFlipped ? -1f : 1f;
        float center = (count - 1) / 2f;

        float angle = flip * fanSpread * (index - center);
        var rotation = Quaternion.Euler(0f, 0f, angle);

        float x = spacing * (index - center);
        float normalized = 2f * index / (count - 1) - 1f;
        float y = flip * verticalSpacing * (1 - normalized * normalized);

        return (new Vector3(x, y, 0f), rotation);
    }
}
