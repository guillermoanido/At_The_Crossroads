using NUnit.Framework;
using System;
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

    private void Start()
    {

    }

    void Update()
    {
        UpdateHandVisuals();
    }

    public void AddCardToHand(Card cardData)
    {
        GameObject newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        newCard.GetComponent<CardDisplay>().cardData = cardData;

        UpdateHandVisuals();
    }

    private void UpdateHandVisuals()
    {
        int cardCount = cardsInHand.Count;
        if (cardCount == 1)
        {
            cardsInHand[0].transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            cardsInHand[0].transform.localPosition = new Vector3(0f, 0f, 0f);
            return;
        }

        for (int i = 0; i < cardCount; i++)
        {
            float rotationAngle = (fanSpread * (i - (cardCount-1) / 2f));
            cardsInHand[i].transform.localRotation = Quaternion.Euler(0f, 0f, rotationAngle);

            float horizontalOffset = (cardSpacing * (i - (cardCount - 1) / 2f));

            float normalizedPosition = (2f * i /(cardCount - 1) - 1f);  
            float verticalOffset = verticalSpacing * (1 - normalizedPosition * normalizedPosition);


            cardsInHand[i].transform.localPosition = new Vector3(horizontalOffset, verticalOffset, 0f);
        }
    }



}
