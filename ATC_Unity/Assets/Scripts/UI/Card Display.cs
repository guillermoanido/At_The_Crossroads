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




    void Start()
    {
        UpdateCardDisplay();
    }

    public void UpdateCardDisplay()
    {
        if (cardData != null)
        {
            cardNameText.text = cardData.cardName;
            cardEffectText.text = cardData.effectDescription;
            speedText.text = cardData.speedType.ToString();
            costText.text = cardData.energyCost.ToString();
        }
    }

}
