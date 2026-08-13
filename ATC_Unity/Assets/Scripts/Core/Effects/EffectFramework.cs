using System;
using UnityEngine;

#region Triggers & Events

/// WHEN an ability happens. Values are serialized as ints on every card asset, so new entries go
/// at the END — reordering would silently rewrite the whole card library.
public enum Trigger
{
    OnPlay,
    Activated,
    OnUpkeep,
    OnControllerTakeDamage,
    OnDestroyed,

    // Always on while the card is in play — never "fired", only asked about (e.g. a Talent that
    // makes your attacks cheaper).
    Static,

    // Fires on cards the CONTROLLER has in play, when that controller does the thing.
    OnControllerPlaysSpell,
    OnControllerDraws,

    // Fires when any player plays their SECOND card in a turn — the card's controller in the
    // context is whoever did it, so a global penalty can hit the right player (Vow of Penance).
    OnAnyPlayerPlaysSecondCard,
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

/// WHAT an ability does. Serialized as ints — append only, never reorder.
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

    // Static: while this card is in play, cards matching `costScope` cost `amount` less stamina
    // (never below 0). Was named ReduceStrikeCost — hence CostScope.StrikeCards being the 0 value,
    // so cards authored before the scope existed still mean what they meant.
    ReduceCost,
    IncreaseCost,

    // Burn: a stacking damage-over-time counter. At its owner's upkeep they take damage equal to
    // their Burn, then it ticks down by 1.
    ApplyBurn,
    RemoveAllBurnAndHeal,

    // Bleed: bites at the end of every turn, but only if the bleeding player took direct damage
    // during it. Does not decay.
    ApplyBleed,

    // A shield pool that, unlike Block, does NOT reset at upkeep. Block is spent first.
    GainDivineShield,

    RemoveAllDebuffs,
    DrawEntireDeck,
    RevealOpponentHand,

    ReturnTargetFromDiscardToHand,
    CastTargetSpellFromDiscard,
    DestroyTargetCondition,

    DestroyTargetOnStack,
    ReturnTargetOnStackToHand,
    RearrangeStack,

    // Turn-scoped rules changes, cleared when the turn ends.
    IncreaseNextOpponentCardCost,
    LockOpponentReflex,
    FreeCostsButNoDraw,

    // Queued for the controller's next upkeep.
    GainStaminaNextUpkeep,

    DestroySelf,
    WinIfDeckAndHandEmpty,

    // Static: every point of Block this player gains is adjusted by `amount` (negative reduces it).
    ModifyBlockGain,

    // Static: this player may play Channel cards at any time, as if they were Reflex.
    AllowChannelAtReflexSpeed,

    // Static: `amount` more cards fit in the zone matching `slotType`.
    ExtraZoneSlots,

    // Turn-scoped: the next card damage from a card is simply ignored.
    AvoidNextDirectDamage,

    // Turn-scoped: the controller's next card ignores its speed restriction.
    NextCardAtReflexSpeed,

    // Bait and Switch: take one of YOUR OWN equipment back, then redeploy one for free.
    ReturnOwnEquipmentToHand,
    NextEquipmentIsFree,

    // Sleight of Hand: copy an opponent's equipment into your own board, then destroy theirs.
    StealCopyOfOpponentEquipment,

    // Set-Up: put a card from hand aside; it comes back next turn and costs nothing.
    SetAsideCardForNextTurn,

    // Pickpocket: look at the opponent's hand and take one of the cards in it.
    TakeCardFromOpponentHand,
}

public enum EffectTarget
{
    Opponent,
    Controller,
}

/// Which cards a cost modifier applies to. StrikeCards is deliberately the 0 value so cards
/// authored before this field existed keep their original meaning.
public enum CostScope
{
    StrikeCards,
    Spells,
    Miracles,
    AllCards,
    Skills,
}

/// Where an ability's magnitude comes from. `amount` is always added on top, so "X + 1" is
/// amount = 1 with the matching source.
public enum AmountSource
{
    Fixed,
    SpellsInYourDiscard,
    SpellsYouPlayedThisTurn,
}

/// An extra chunk of magnitude that only counts when something is true — "deal 1 damage, +2 if you
/// played a Reflex card this turn".
public enum AmountCondition
{
    Always,
    IfYouPlayedAReflexCardThisTurn,
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

    [Tooltip("Counts something in play and adds it to Amount, for 'X + 1' abilities. Fixed = just Amount.")]
    public AmountSource amountSource = AmountSource.Fixed;

    [Tooltip("Extra magnitude that only applies when this condition holds.")]
    public AmountCondition bonusCondition = AmountCondition.Always;

    [Tooltip("How much the condition adds when it holds.")]
    public int conditionalBonus = 0;

    [Header("Cost modifiers only (effect = Reduce/IncreaseCost)")]
    [Tooltip("Which cards the modifier applies to. Target decides WHOSE cards: Controller = yours, Opponent = theirs.")]
    public CostScope costScope = CostScope.StrikeCards;

    [Header("Slot modifiers only (effect = ExtraZoneSlots)")]
    [Tooltip("Which kind of card gets more room in play.")]
    public Card.CardType slotType = Card.CardType.Weapon;

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

    public static bool IsOwnEquipmentInPlay(Targetable t, Player controller)
        => IsCardInPlay(t) && t.Owner == controller && t.Data != null && t.Data.IsEquipment;

    public static bool IsConditionInPlay(Targetable t)
        => IsCardInPlay(t) && t.Data != null && t.Data.cardType == Card.CardType.Condition;

    public static bool IsInOwnDiscard(Targetable t, Player controller)
        => t != null && t.Zone != null && t.Zone.Kind == CardZone.ZoneKind.Discard && t.Owner == controller;

    public static bool IsOwnSkillOrSpellInDiscard(Targetable t, Player controller)
        => IsInOwnDiscard(t, controller) && t.Data != null
        && (t.Data.cardType == Card.CardType.Skill || t.Data.cardType == Card.CardType.Spell);

    public static bool IsOwnSpellInDiscard(Targetable t, Player controller)
        => IsInOwnDiscard(t, controller) && t.Data != null && t.Data.cardType == Card.CardType.Spell;

    /// A card currently waiting on the stack — identified by the card object a stack item points at.
    public static bool IsOnStack(Targetable t)
        => t != null && GameStack.Instance != null && GameStack.Instance.HoldsCard(t.gameObject);

    public static bool IsInHandOf(Targetable t, Player player)
    {
        var hand = player != null ? player.handManager : null;
        return t != null && hand != null && hand.cardsInHand.Contains(t.gameObject);
    }

    /// A card already in `zone` — used when a full equipment slot has to be cleared to make room.
    public static bool IsInZone(Targetable t, CardZone zone)
        => t != null && zone != null && t.Zone == zone;
}

#endregion
