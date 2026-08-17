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

    [Header("Frames")]
    [Tooltip("Frame for cards that stay on the battlefield once played — weapons, armour, talents, auras, conditions.")]
    public Sprite permanentFrame;

    [Tooltip("Frame for cards that resolve and go to the discard pile — attacks, spells, skills, miracles, consumables.")]
    public Sprite transientFrame;

    [Header("Face-down")]
    public Sprite cardBackSprite;
    public GameObject faceContent;

    [Header("Live values")]
    [Tooltip("Seconds between rechecking the numbers this card shows. Costs and damage change from effects in play, so the face has to keep up — but not every frame.")]
    [SerializeField] private float liveValueInterval = 0.2f;

    [SerializeField] private Color discountedCostColour = new Color(0.55f, 1f, 0.6f);
    [SerializeField] private Color raisedCostColour = new Color(1f, 0.55f, 0.45f);

    public bool IsFaceUp { get; private set; } = true;

    private Sprite frontSprite;
    private bool capturedFront;
    private float nextLiveValueCheck;
    private int shownCost = int.MinValue;
    private string shownEffectText;

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
        if (cardImage != null) cardImage.sprite = FrameFor(cardData);
        if (cardData == null) return;

        if (cardNameText != null) cardNameText.text = Localization.CardName(cardData);
        if (speedText != null) speedText.text = cardData.speedType.ToString();

        // Force the live numbers to redraw — Render is also what runs after a zone change.
        shownCost = int.MinValue;
        shownEffectText = null;
        RefreshLiveValues();
    }

    /// The border art a card wears: one frame for things that stay on the battlefield, another for
    /// things that resolve and leave. Falls back to whatever the prefab shipped with, so a card
    /// still renders before the frames are assigned.
    private Sprite FrameFor(Card card)
    {
        if (card == null) return capturedFront ? frontSprite : null;

        var frame = card.IsPermanent ? permanentFrame : transientFrame;
        if (frame != null) return frame;

        return capturedFront ? frontSprite : null;
    }

    // A card's real numbers move around: a Talent discounts it, a Tax raises it, a Strike charge
    // boosts the weapon's damage, "X + 1" counts something that changes as you play. Poll on a slow
    // timer and only touch the text when a number actually changed.
    private void Update()
    {
        if (!IsFaceUp || cardData == null) return;
        if (Time.unscaledTime < nextLiveValueCheck) return;

        nextLiveValueCheck = Time.unscaledTime + Mathf.Max(0.05f, liveValueInterval);
        RefreshLiveValues();
    }

    private void RefreshLiveValues()
    {
        if (cardData == null) return;
        var owner = Targetable.OwnerOf(gameObject);

        UpdateCost(owner);
        UpdateEffectText(owner);
    }

    private void UpdateCost(Player owner)
    {
        if (costText == null) return;

        int cost = CardValues.Cost(owner, cardData);
        if (cost == shownCost) return;
        shownCost = cost;

        costText.text = cost.ToString();
        costText.color = cost == cardData.energyCost ? Color.white
                       : cost < cardData.energyCost ? discountedCostColour
                       : raisedCostColour;
    }

    private void UpdateEffectText(Player owner)
    {
        if (cardEffectText == null) return;

        string printed = Localization.CardText(cardData);
        string live = CardValues.LiveSummary(gameObject, cardData, owner);
        string full = string.IsNullOrEmpty(live) ? printed : printed + "\n" + live;

        if (full == shownEffectText) return;
        shownEffectText = full;
        cardEffectText.text = full;
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
