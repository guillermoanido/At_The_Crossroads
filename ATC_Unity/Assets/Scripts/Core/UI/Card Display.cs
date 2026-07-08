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

    // The prefab's default front sprite, captured before anything can swap in the card back,
    // so flipping face-up again always restores the front art.
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
}
