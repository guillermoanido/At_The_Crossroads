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
        // Hide only the front-face details, NOT `faceContent`: in the prefab faceContent is the
        // whole Card Canvas and the card image lives inside it, so disabling it would hide the
        // back too and the card would render blank. Instead we blank the details and swap the
        // (still-visible) card image to the back sprite.
        SetFaceDetailsActive(false);
        if (cardImage != null && cardBackSprite != null) cardImage.sprite = cardBackSprite;
    }

    private void ShowFaceUp()
    {
        SetFaceDetailsActive(true);
        if (cardImage != null && capturedFront) cardImage.sprite = frontSprite;
        if (cardData == null) return;

        if (cardNameText != null) cardNameText.text = cardData.cardName;
        if (cardEffectText != null) cardEffectText.text = cardData.effectDescription;
        if (speedText != null) speedText.text = cardData.speedType.ToString();
        if (costText != null) costText.text = EffectiveCost().ToString();
    }

    // Show what the card costs its owner right now, not its printed cost — a Talent in play can
    // discount it. Player.RefreshHandCosts re-renders the hand whenever the board changes.
    private int EffectiveCost()
    {
        if (cardData == null) return 0;

        var movement = GetComponent<CardMovement>();
        var owner = movement != null ? movement.Owner : null;
        return owner != null ? owner.StaminaCostOf(cardData) : cardData.energyCost;
    }

    // Toggle the front-only elements (name, effect, speed, cost). Used to reveal just the back
    // sprite when a card is face-down, since the card image itself is the shared front/back art.
    private void SetFaceDetailsActive(bool active)
    {
        ToggleObject(cardNameText, active);
        ToggleObject(cardEffectText, active);
        ToggleObject(speedText, active);
        ToggleObject(speedImage, active);
        ToggleObject(costText, active);
        ToggleObject(costImage, active);
    }

    private static void ToggleObject(Component component, bool active)
    {
        if (component != null) component.gameObject.SetActive(active);
    }

    public static void DisableGameplayInteractions(GameObject clone)
    {
        var move = clone.GetComponent<CardMovement>();       if (move != null) move.enabled = false;
        var actions = clone.GetComponent<CardBoardActions>(); if (actions != null) actions.enabled = false;
    }
}
