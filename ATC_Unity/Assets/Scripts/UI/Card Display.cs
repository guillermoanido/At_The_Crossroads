using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class CardDisplay: MonoBehaviour
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
    public GameObject faceContent; // parent of all front-side UI (name, effect, cost, speed, art)

    public bool IsFaceUp { get; private set; } = true;

    public void SetFaceUp(bool faceUp)
    {
        IsFaceUp = faceUp;
        UpdateCardDisplay();
    }

    void Start()
    {
        UpdateCardDisplay();
    }

    public void UpdateCardDisplay()
    {
        if (!IsFaceUp)
        {
            if (faceContent != null) faceContent.SetActive(false);
            if (cardImage != null && cardBackSprite != null) cardImage.sprite = cardBackSprite;
            return;
        }

        if (faceContent != null) faceContent.SetActive(true);

        if (cardData != null)
        {
            cardNameText.text = cardData.cardName;
            cardEffectText.text = cardData.effectDescription;
            speedText.text = cardData.speedType.ToString();
            costText.text = cardData.energyCost.ToString();
        }
    }

}
