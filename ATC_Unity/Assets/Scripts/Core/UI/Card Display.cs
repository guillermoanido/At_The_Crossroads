using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public Card cardData;

    public Image cardImage;
    public TMP_Text cardNameText;
    public TMP_Text cardEffectText;

    public TMP_Text speedText;
    public Image speedImage;

    public TMP_Text costText;
    public Image costImage;

    [Header("Face-down")]
    public Sprite cardBackSprite;
    public GameObject faceContent;

    public bool IsFaceUp { get; private set; } = true;

    private Sprite frontSprite;
    private bool capturedFront;

    private void Awake()
    {
        if (cardImage != null)
        {
            frontSprite = cardImage.sprite;
            capturedFront = true;
        }
    }

    private void Start()
    {
        Render();
    }

    public void SetFaceUp(bool faceUp)
    {
        IsFaceUp = faceUp;
        Render();
    }

    public void UpdateCardDisplay() => Render();

    private void Render()
    {
        if (!IsFaceUp)
        {
            ShowFaceDown();
            return;
        }
        ShowFaceUp();
    }

    private void ShowFaceDown()
    {
        if (faceContent != null) faceContent.SetActive(false);
        if (cardImage != null && cardBackSprite != null) cardImage.sprite = cardBackSprite;
    }

    private void ShowFaceUp()
    {
        if (faceContent != null) faceContent.SetActive(true);
        if (cardImage != null && capturedFront) cardImage.sprite = frontSprite;
        if (cardData == null) return;

        if (cardNameText != null) cardNameText.text = cardData.cardName;
        if (cardEffectText != null) cardEffectText.text = cardData.effectDescription;
        if (speedText != null) speedText.text = cardData.speedType.ToString();
        if (costText != null) costText.text = cardData.energyCost.ToString();
    }

    public static void DisableGameplayInteractions(GameObject clone)
    {
        var move = clone.GetComponent<CardMovement>();       if (move != null) move.enabled = false;
        var drag = clone.GetComponent<DragUIObject>();        if (drag != null) drag.enabled = false;
        var actions = clone.GetComponent<CardBoardActions>(); if (actions != null) actions.enabled = false;
    }
}
