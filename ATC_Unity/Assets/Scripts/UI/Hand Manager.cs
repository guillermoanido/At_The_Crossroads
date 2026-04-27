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

    public List<GameObject> cardsInHand = new List<GameObject>();

    public void AddCardToHand(Card cardData)
    {
        GameObject newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        newCard.GetComponent<CardDisplay>().cardData = cardData;
        newCard.GetComponent<CardMovement>().Init(this);

        UpdateHandVisuals();
    }

    public void RemoveCardFromHand(GameObject card)
    {
        cardsInHand.Remove(card);
        UpdateHandVisuals();
    }

    private void UpdateHandVisuals()
    {
        int cardCount = cardsInHand.Count;
        if (cardCount == 0) return;

        if (cardCount == 1)
        {
            cardsInHand[0].GetComponent<CardMovement>().SetSlot(Vector3.zero, Quaternion.identity, Vector3.one);
            return;
        }

        for (int i = 0; i < cardCount; i++)
        {
            float rotationAngle = fanSpread * (i - (cardCount - 1) / 2f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationAngle);

            float horizontalOffset = cardSpacing * (i - (cardCount - 1) / 2f);
            float normalizedPosition = 2f * i / (cardCount - 1) - 1f;
            float verticalOffset = verticalSpacing * (1 - normalizedPosition * normalizedPosition);

            Vector3 position = new Vector3(horizontalOffset, verticalOffset, 0f);
            cardsInHand[i].GetComponent<CardMovement>().SetSlot(position, rotation, Vector3.one);
        }
    }
}
