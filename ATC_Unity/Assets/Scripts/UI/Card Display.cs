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
        if (cardData == null) return;

        cardNameText.text = cardData.cardName;
        cardEffectText.text = cardData.effectDescription;
        speedText.text = cardData.speedType.ToString();
        costText.text = cardData.energyCost.ToString();
    }
}
