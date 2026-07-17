using System;
using UnityEngine;

#region Triggers & Events

public enum Trigger
{
    OnPlay,
    Activated,
    OnUpkeep,
    OnControllerTakeDamage,
    OnDestroyed,
}

public class DamageEvent
{
    public Player defender;
    public int amount;
    public GameObject sourceCardGO;
    public Card sourceCardData;
}

public class EffectContext
{
    public GameObject sourceCardGO;
    public Card sourceCardData;
    public Player controller;
    public Player opponent;
    public DamageEvent damage;
}

public class StackItem
{
    public Player controller;
    public GameObject sourceCardGO;
    public Card sourceCardData;
    public Trigger trigger;
}

#endregion

#region Ability Data (authored on each Card)

public enum EffectKind
{
    None,
    DealDamage,
    GainBlock,
    GainLife,
    DrawCards,
    GainStamina,
    Scry,
    ReduceIncomingDamage,
    DestroyTargetCard,
    ReturnTargetToHand,
    LoseStamina,
    DestroyTargetEquipment,
    DestroyAllOpponentEquipment,
    OpponentDiscards,
    TakeExtraTurn,
    ReturnTargetEquipmentToHand,
    IncreaseMaxStamina,
    Strike,
}

public enum EffectTarget
{
    Opponent,
    Controller,
}

[Serializable]
public class CardAbility
{
    [Tooltip("When this ability happens.")]
    public Trigger trigger = Trigger.OnPlay;

    [Tooltip("What it does.")]
    public EffectKind effect = EffectKind.DealDamage;

    [Tooltip("Magnitude — damage dealt, block/life gained, cards drawn, tiles scried, damage reduced. Target-picking effects ignore this.")]
    public int amount = 1;

    [Tooltip("Who a damage/reduce effect hits. Block/Life/Draw/Stamina/Scry always affect the controller.")]
    public EffectTarget target = EffectTarget.Opponent;

    [Header("Activated abilities only (trigger = Activated)")]
    [Tooltip("Speed the ability can be used at. Channel = your main phase only; Reflex = any time.")]
    public Card.SpeedType activationSpeed = Card.SpeedType.Channel;

    [Tooltip("Extra stamina spent to activate (the card's energyCost was already paid when it was played).")]
    public int activationCost = 0;

    [Tooltip("If true the card taps when activated and can't be used again until it untaps at your upkeep.")]
    public bool tapToActivate = true;

    [Header("Strike ability only (effect = Strike)")]
    [Tooltip("You pick one of YOUR weapons in play; it attacks for its damage WITHOUT tapping. Base damage is multiplied by this (Hurl = 2).")]
    public int strikeDamageMultiplier = 1;

    [Tooltip("Flat damage added after the multiplier (Heavy Swing = +2). Negative lowers it (Open Veins = -1).")]
    public int strikeBonusDamage = 0;

    [Tooltip("If true, the weapon used is destroyed (sent to discard) after the strike (Hurl).")]
    public bool strikeDestroysWeapon = false;
}

#endregion

#region Target Filters

public static class TargetFilters
{
    public static bool IsCardInPlay(Targetable t)
    {
        if (t == null || t.Zone == null) return false;
        return t.Zone.Kind != CardZone.ZoneKind.Discard
            && t.Zone.Kind != CardZone.ZoneKind.Exile;
    }

    public static bool IsOpponentCardInPlay(Targetable t, Player controller)
        => IsCardInPlay(t) && t.Owner != null && t.Owner != controller;

    public static bool IsOpponentEquipmentInPlay(Targetable t, Player controller)
        => IsOpponentCardInPlay(t, controller) && t.Data != null && t.Data.IsEquipment;

    public static bool IsOwnWeaponInPlay(Targetable t, Player controller)
        => IsCardInPlay(t) && t.Owner == controller && t.Data != null && t.Data.cardType == Card.CardType.Weapon;
}

#endregion
