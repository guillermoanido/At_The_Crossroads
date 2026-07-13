using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    #region Configuration

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

    [Tooltip("Defense acts as a shield pool: it absorbs incoming damage point-for-point before HP is touched, and depletes as it soaks hits. Set the cap here (0 = no upper cap; cards/buttons can stack it freely).")]
    [SerializeField] private int maxDefense = 0;

    #endregion

    #region Properties

    public int CurrentHp { get; private set; }
    public int MaxHp => maxHp;
    public int Stamina { get; private set; }
    public int MaxStamina => maxStamina;
    public int Defense { get; private set; }
    public int MaxDefense => maxDefense;

    public Player Opponent => GameManager.Instance != null ? GameManager.Instance.Opponent(this) : null;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        handManager.SetOwner(this);
        ConfigureZoneKinds();
        Stamina = maxStamina;
        CurrentHp = maxHp;
        Defense = maxDefense;
    }

    private void ConfigureZoneKinds()
    {
        SetZoneKind(discardZone,   CardZone.ZoneKind.Discard);
        SetZoneKind(exileZone,     CardZone.ZoneKind.Exile);
        SetZoneKind(weaponZone,    CardZone.ZoneKind.Weapon);
        SetZoneKind(shieldZone,    CardZone.ZoneKind.Shield);
        SetZoneKind(armourZone,    CardZone.ZoneKind.Armour);
        SetZoneKind(equipmentZone, CardZone.ZoneKind.Equipment);
        SetZoneKind(accessoryZone, CardZone.ZoneKind.Accessory);
        SetZoneKind(talentZone,    CardZone.ZoneKind.Talent);
        SetZoneKind(auraZone,      CardZone.ZoneKind.Aura);
    }

    private static void SetZoneKind(CardZone zone, CardZone.ZoneKind kind)
    {
        if (zone != null) zone.SetKind(kind);
    }

    #endregion

    #region Stats

    public void AdjustHp(int delta) => CurrentHp = Mathf.Max(0, CurrentHp + delta);

    public void AdjustStamina(int delta) => Stamina = Mathf.Max(0, Stamina + delta);

    public void ResetStamina() => Stamina = maxStamina;

    public void AdjustDefense(int delta)
    {
        int next = Defense + delta;
        Defense = maxDefense > 0 ? Mathf.Clamp(next, 0, maxDefense) : Mathf.Max(0, next);
    }

    public bool SpendStamina(int amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    public void TakeDamage(int amount, GameObject sourceCardGO = null, Card sourceCardData = null)
    {
        if (amount <= 0) return;

        var dmg = new DamageEvent
        {
            defender = this,
            amount = amount,
            sourceCardGO = sourceCardGO,
            sourceCardData = sourceCardData
        };

        FireTriggersOnBoard(Trigger.OnControllerTakeDamage, dmg);
        if (dmg.amount <= 0) return;

        int absorbed = Mathf.Min(Defense, dmg.amount);
        if (absorbed > 0)
        {
            Defense -= absorbed;
            dmg.amount -= absorbed;
        }

        if (dmg.amount > 0) AdjustHp(-dmg.amount);
    }

    #endregion

    #region Turn

    public void DrawCard() => deckManager.DrawCard(handManager);

    public void ResolveUpkeep()
    {
        Defense = 0;
        ResetStamina();
        UntapBoard();
        FireTriggersOnBoard(Trigger.OnUpkeep, null);
    }

    private void UntapBoard()
    {
        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
                if (cardGO != null)
                    cardGO.GetComponent<CardTapState>()?.Untap();
        }
    }

    #endregion

    #region Playing Cards

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

        PushToStack(cardGO, cardData, Trigger.OnPlay);
        return true;
    }

    public bool TryActivateCard(GameObject cardGO, Card cardData)
    {
        if (cardGO == null || cardData == null) return false;

        var ability = cardData.FirstActivated();
        if (ability == null) return false;

        if (GameManager.Instance != null && !GameManager.Instance.IsControllingPlayer(this))
        {
            Debug.Log($"[Activate] {name} doesn't have priority.");
            return false;
        }

        var zone = cardGO.GetComponentInParent<CardZone>();
        if (zone == null || !IsBoardZone(zone))
        {
            Debug.Log($"[Activate] {cardData.cardName} must be in play to activate.");
            return false;
        }

        var tap = cardGO.GetComponent<CardTapState>();
        if (ability.tapToActivate && tap != null && tap.IsTapped)
        {
            Debug.Log($"[Activate] {cardData.cardName} is tapped — already used this turn.");
            return false;
        }

        if (!SpeedAllowedThisPhase(ability.activationSpeed, out string reason))
        {
            Debug.Log($"[Activate] {name} cannot activate {cardData.cardName}: {reason}");
            return false;
        }

        if (Stamina < ability.activationCost)
        {
            Debug.Log($"[Activate] {name} lacks stamina to activate {cardData.cardName} ({Stamina}/{ability.activationCost}).");
            return false;
        }

        SpendStamina(ability.activationCost);
        if (ability.tapToActivate && tap != null) tap.Tap();

        Debug.Log($"[Activate] {name} activated {cardData.cardName}.");
        PushToStack(cardGO, cardData, Trigger.Activated);
        return true;
    }

    private void PushToStack(GameObject cardGO, Card cardData, Trigger trigger)
    {
        if (GameStack.Instance != null)
            GameStack.Instance.Push(new StackItem { controller = this, sourceCardGO = cardGO, sourceCardData = cardData, trigger = trigger });
        else if (EffectRunner.Instance != null)
            EffectRunner.Instance.FireAbilities(cardData, MakeContext(cardGO, cardData), trigger);
    }

    public bool HasReflexResponse()
    {
        if (handManager != null)
        {
            foreach (var cardGO in handManager.cardsInHand)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data != null && data.speedType == Card.SpeedType.Reflex && Stamina >= data.energyCost)
                    return true;
            }
        }

        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                var ability = data != null ? data.FirstActivated() : null;
                if (ability == null || ability.activationSpeed != Card.SpeedType.Reflex) continue;
                if (Stamina < ability.activationCost) continue;
                var tap = cardGO.GetComponent<CardTapState>();
                bool alreadyTapped = ability.tapToActivate && tap != null && tap.IsTapped;
                if (!alreadyTapped) return true;
            }
        }

        return false;
    }

    private static bool IsBoardZone(CardZone zone)
        => zone.Kind != CardZone.ZoneKind.Discard && zone.Kind != CardZone.ZoneKind.Exile;

    private bool CanPlay(Card card, out string reason)
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsControllingPlayer(this))
        {
            reason = "You don't have priority";
            return false;
        }

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
            case Card.CardType.Condition: return auraZone;
            default:                      return discardZone;
        }
    }

    #endregion

    #region Zone Movement

    public void SendToDiscard(GameObject cardGO)
    {
        FireTriggersForDyingCard(cardGO, Trigger.OnDestroyed);
        MoveCardToZone(cardGO, discardZone);
    }

    public void SendToExile(GameObject cardGO) => MoveCardToZone(cardGO, exileZone);

    public void DestroyAllEquipment()
    {
        foreach (var zone in EquipmentZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in new List<GameObject>(zone.Cards))
                SendToDiscard(cardGO);
        }
    }

    public void DiscardFromHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var cards = handManager.cardsInHand;
            if (cards.Count == 0) return;
            SendToDiscard(cards[cards.Count - 1]);
        }
    }

    public void ReturnToHand(GameObject cardGO)
    {
        if (cardGO == null) return;
        if (handManager.cardsInHand.Contains(cardGO)) return;

        var data = cardGO.GetComponent<CardDisplay>()?.cardData;
        if (data == null) return;

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();

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

    private IEnumerable<CardZone> BoardZones()
    {
        yield return weaponZone;
        yield return armourZone;
        yield return shieldZone;
        yield return equipmentZone;
        yield return accessoryZone;
        yield return talentZone;
        yield return auraZone;
    }

    private IEnumerable<CardZone> EquipmentZones()
    {
        yield return weaponZone;
        yield return accessoryZone;
        yield return armourZone;
    }

    #endregion

    #region Effect Plumbing

    public EffectContext BuildContext(GameObject cardGO, Card cardData) => MakeContext(cardGO, cardData);

    private EffectContext MakeContext(GameObject cardGO, Card cardData) => new EffectContext
    {
        sourceCardGO = cardGO,
        sourceCardData = cardData,
        controller = this,
        opponent = Opponent
    };

    private void FireTriggersOnBoard(Trigger trigger, DamageEvent dmg)
    {
        if (EffectRunner.Instance == null) return;
        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            var snapshot = new List<GameObject>(zone.Cards);
            foreach (var cardGO in snapshot)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data == null) continue;

                var ctx = MakeContext(cardGO, data);
                ctx.damage = dmg;

                if (trigger == Trigger.OnControllerTakeDamage)
                    EffectRunner.Instance.FireAbilitiesImmediate(data, ctx, trigger);
                else
                    EffectRunner.Instance.FireAbilities(data, ctx, trigger);
            }
        }
    }

    private void FireTriggersForDyingCard(GameObject cardGO, Trigger trigger)
    {
        if (cardGO == null || EffectRunner.Instance == null) return;
        var data = cardGO.GetComponent<CardDisplay>()?.cardData;
        if (data == null) return;

        var zone = cardGO.GetComponentInParent<CardZone>();
        if (zone == null) return;
        if (zone.Kind == CardZone.ZoneKind.Discard || zone.Kind == CardZone.ZoneKind.Exile) return;

        EffectRunner.Instance.FireAbilities(data, MakeContext(cardGO, data), trigger);
    }

    #endregion

    #region Interaction Sync

    private static void FreezeCardInteractions(GameObject cardGO)
    {
        var movement = cardGO.GetComponent<CardMovement>();
        if (movement != null) movement.enabled = false;
        var drag = cardGO.GetComponent<DragUIObject>();
        if (drag != null) drag.enabled = false;

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    private void SyncBoardActionsForZone(GameObject cardGO, CardZone destination)
    {
        var actions = cardGO.GetComponent<CardBoardActions>();
        if (actions == null) return;
        actions.enabled = destination != discardZone && destination != exileZone;
    }

    #endregion
}
