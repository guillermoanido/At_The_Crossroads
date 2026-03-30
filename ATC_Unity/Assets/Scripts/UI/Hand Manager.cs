using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour 
{
    public GameObject cardPrefab;
    public Transform handPosition;
    
    public float fanSpread = 5f;
    public float cardSpacing = 100f;
    public float verticalSpacing = 10f;

    public List<GameObject> cardsInHand = new List<GameObject>();

    private void Start()
    {
        AddCardToHand();
        AddCardToHand();
        AddCardToHand();
    }

    public void AddCardToHand()
    {
        GameObject newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        UpdateHandVisuals();
    }

    private void UpdateHandVisuals()
    {
        int cardCount = cardsInHand.Count;
        for (int i = 0; i < cardCount; i++)
        {
            float rotationAngle = (fanSpread * (i - (cardCount-1) / 2f));
            cardsInHand[i].transform.localRotation = Quaternion.Euler(0f, 0f, rotationAngle);

            float horizontalOffset = (cardSpacing * (i - (cardCount - 1) / 2f));
            float verticalOffset = (verticalSpacing * (i - (cardCount - 1) / 2f));
            cardsInHand[i].transform.localPosition = new Vector3(horizontalOffset, verticalOffset, 0f);
        }
    }

    private void Update()
    {
        UpdateHandVisuals();
    }


}
