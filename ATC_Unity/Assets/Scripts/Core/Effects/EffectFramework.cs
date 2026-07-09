using System;
using UnityEngine;

#region Triggers & Events

// When an ability happens. One card can carry several abilities with different triggers.
public enum Trigger
{
    OnPlay,                 // fires the moment the card is played / enters play
    Activated,              // fires when the controller clicks the card while it's in play
    OnUpkeep,               // fires at the start of the controller's turn
    OnControllerTakeDamage, // fires while the controller is about to take damage (can reduce it)
    OnDestroyed,            // fires when the card leaves play for the discard
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

#endregion

#region Ability Data (authored on each Card)

// What an ability does. Instant kinds resolve synchronously; the Target* kinds pause for a pick.
public enum EffectKind
{
    None,
    DealDamage,           // amount → defender's TakeDamage (target chooses opponent/controller)
    GainBlock,            // amount → controller's Defense shield
    GainLife,             // amount → controller's HP
    DrawCards,            // amount cards → controller draws
    GainStamina,          // amount → controller's stamina
    Scry,                 // amount → opens the Scry panel on the controller's deck
    ReduceIncomingDamage, // amount → reduces the current DamageEvent (use with OnControllerTakeDamage)
    DestroyTargetCard,    // pick an opponent card in play → discard it
    ReturnTargetToHand,   // pick an opponent card in play → bounce it to their hand
    LoseStamina,          // amount → controller loses stamina (floored at 0)
    DestroyTargetEquipment,     // pick an opponent equipment (weapon/accessory/armour) in play → discard it
    DestroyAllOpponentEquipment,// discard all of the opponent's equipment in play (no target needed)
    OpponentDiscards,     // amount → opponent discards that many cards from hand
    TakeExtraTurn,        // controller takes another turn after this one
    ReturnTargetEquipmentToHand,// pick an opponent equipment in play → bounce it to their hand
}

public enum EffectTarget
{
    Opponent,
    Controller,
}

// A single thing a card does. Authored as a list on the Card asset — no per-effect files.
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
}

#endregion
