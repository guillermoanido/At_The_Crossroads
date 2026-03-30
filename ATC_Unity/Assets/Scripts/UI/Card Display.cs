using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class CardDisplay: MonoBehaviour
{
    public Card cardData;

    public Image cardImage;
    public TMP_Text cardText;

    void Start()
    {
        UpdateCardDisplay();
    }

    public void UpdateCardDisplay()
    {
        if (cardData != null)
        {
            cardText.text = cardData.effectDescription;
        }
    }

}
