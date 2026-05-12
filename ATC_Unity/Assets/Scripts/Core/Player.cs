using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public HandManager handManager;
    public DeckManager deckManager;
    public PlayArea playArea;

    [Header("Zones")]
    public CardZone discardZone;
    public CardZone weaponZone;
    public CardZone armourZone;
    public CardZone shieldZone;
    public CardZone equipmentZone;
    public CardZone accessoryZone;
    public CardZone talentZone;
    public CardZone auraZone;
    public CardZone exileZone;

    [Header("Stats")]
    [SerializeField] private int maxHp = 30;
    [SerializeField] private int maxStamina = 3;

    public int CurrentHp { get; private set; }
    public int MaxHp => maxHp;
    public int Stamina { get; private set; }
    public int MaxStamina => maxStamina;

    private void Awake()
    {
        handManager.SetOwner(this);
        Stamina = maxStamina;
        CurrentHp = maxHp;
    }

    public void DrawCard() => deckManager.DrawCard(handManager);

    public void ResetStamina() => Stamina = maxStamina;

    public void AdjustHp(int delta) => CurrentHp = Mathf.Clamp(CurrentHp + delta, 0, maxHp);

    public void AdjustStamina(int delta) => Stamina = Mathf.Max(0, Stamina + delta);

    public bool SpendStamina(int amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    public void SendToDiscard(GameObject cardGO) => MoveCardToZone(cardGO, discardZone);

    public void SendToExile(GameObject cardGO) => MoveCardToZone(cardGO, exileZone);

    public void ReturnToHand(GameObject cardGO)
    {
        if (cardGO == null) return;
        if (handManager.cardsInHand.Contains(cardGO)) return;

        var data = cardGO.GetComponent<CardDisplay>()?.cardData;
        if (data == null) return;

        RemoveFromAnyZone(cardGO);
        Destroy(cardGO);
        handManager.AddCardToHand(data);
    }

    public void MoveCardToZone(GameObject cardGO, CardZone destination)
    {
        if (destination == null || cardGO == null) return;

        if (handManager.cardsInHand.Contains(cardGO))
            handManager.RemoveCardFromHand(cardGO);
        else
            RemoveFromAnyZone(cardGO);

        destination.AddCard(cardGO);
        FreezeCardInteractions(cardGO);
        SyncBoardActionsForZone(cardGO, destination);
    }

    public bool TryPlayCard(GameObject cardGO, Card cardData)
    {
        if (!CanPlay(cardData, out string reason))
        {
            Debug.Log($"[Play] {name} cannot play {cardData.cardName}: {reason}");
            return false;
        }

        var zone = ZoneFor(cardData.cardType);
        SpendStamina(cardData.energyCost);
        handManager.RemoveCardFromHand(cardGO);
        zone.AddCard(cardGO);
        FreezeCardInteractions(cardGO);
        SyncBoardActionsForZone(cardGO, zone);

        Debug.Log($"[Play] {name} played {cardData.cardName} → {zone.name}");
        return true;
    }

    private bool CanPlay(Card card, out string reason)
    {
        if (!SpeedAllowedThisPhase(card.speedType, out reason)) return false;

        if (Stamina < card.energyCost)
        {
            reason = $"Not enough stamina ({Stamina}/{card.energyCost})";
            return false;
        }

        var zone = ZoneFor(card.cardType);
        if (zone == null) { reason = $"No zone configured for {card.cardType}"; return false; }
        if (zone.IsFull)  { reason = $"{zone.name} is full"; return false; }

        reason = null;
        return true;
    }

    private bool SpeedAllowedThisPhase(Card.SpeedType speed, out string reason)
    {
        reason = null;
        if (speed != Card.SpeedType.Channel) return true;

        if (!GameManager.Instance.IsActivePlayer(this))
        {
            reason = "Channel cards require your turn";
            return false;
        }

        var phase = GameManager.Instance.CurrentPhase;
        if (phase != GameManager.GamePhase.Main1 && phase != GameManager.GamePhase.Main2)
        {
            reason = $"Channel cards require Main1/Main2 (current: {phase})";
            return false;
        }
        return true;
    }

    private CardZone ZoneFor(Card.CardType type)
    {
        switch (type)
        {
            case Card.CardType.Weapon:    return weaponZone;
            case Card.CardType.Armour:    return armourZone;
            case Card.CardType.Shield:    return shieldZone;
            case Card.CardType.Equipment: return equipmentZone;
            case Card.CardType.Accesory:  return accessoryZone;
            case Card.CardType.Talent:    return talentZone;
            case Card.CardType.Aura:      return auraZone;
            default:                      return discardZone;
        }
    }

    private void RemoveFromAnyZone(GameObject cardGO)
    {
        foreach (var zone in AllZones())
        {
            if (zone != null && zone.Cards.Contains(cardGO))
            {
                zone.RemoveCard(cardGO);
                return;
            }
        }
    }

    private static void FreezeCardInteractions(GameObject cardGO)
    {
        var movement = cardGO.GetComponent<CardMovement>();
        if (movement != null) movement.enabled = false;
        var drag = cardGO.GetComponent<DragUIObject>();
        if (drag != null) drag.enabled = false;
    }

    private void SyncBoardActionsForZone(GameObject cardGO, CardZone destination)
    {
        var actions = cardGO.GetComponent<CardBoardActions>();
        if (actions == null) return;
        actions.enabled = destination != discardZone && destination != exileZone;
    }

    private IEnumerable<CardZone> AllZones()
    {
        yield return weaponZone;
        yield return armourZone;
        yield return shieldZone;
        yield return equipmentZone;
        yield return accessoryZone;
        yield return talentZone;
        yield return auraZone;
        yield return discardZone;
        yield return exileZone;
    }
}
