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

    // Enable for Player 2 so the fan arcs upward and rotations are mirrored.
    [SerializeField] private bool isFlipped = false;

    // Whether cards in this hand are visible to the local viewer.
    // Hot-seat: set true for local player, false for opponent.
    // Networked (later): set this to (Owner == localClient).
    [SerializeField] private bool showFaceUp = true;

    [SerializeField] private int maxHandSize = 10;

    public Player Owner { get; private set; }

    public List<GameObject> cardsInHand = new List<GameObject>();

    public bool IsHandFull => cardsInHand.Count >= maxHandSize;

    public void SetOwner(Player player)
    {
        Owner = player;
    }

    public void AddCardToHand(Card cardData)
    {
        GameObject newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        var display = newCard.GetComponent<CardDisplay>();
        display.cardData = cardData;
        display.SetFaceUp(showFaceUp);

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

        float flip = isFlipped ? -1f : 1f;
        Vector3 scale = Vector3.one * cardScale;

        if (cardCount == 1)
        {
            cardsInHand[0].GetComponent<CardMovement>().SetSlot(Vector3.zero, Quaternion.identity, scale);
            return;
        }

        // Shrink spacing when the fan would exceed maxHandWidth so cards overlap instead of running off-screen.
        float effectiveSpacing = cardSpacing;
        float totalWidth = (cardCount - 1) * cardSpacing;
        if (totalWidth > maxHandWidth)
            effectiveSpacing = maxHandWidth / (cardCount - 1);

        for (int i = 0; i < cardCount; i++)
        {
            float rotationAngle = flip * fanSpread * (i - (cardCount - 1) / 2f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationAngle);

            float horizontalOffset = effectiveSpacing * (i - (cardCount - 1) / 2f);
            float normalizedPosition = 2f * i / (cardCount - 1) - 1f;
            float verticalOffset = flip * verticalSpacing * (1 - normalizedPosition * normalizedPosition);

            Vector3 position = new Vector3(horizontalOffset, verticalOffset, 0f);
            cardsInHand[i].GetComponent<CardMovement>().SetSlot(position, rotation, scale);
        }
    }
}
