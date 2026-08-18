using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class Card : ScriptableObject
{
    public string cardName;
    public CardType cardType;
    public SpeedType speedType;

    [Tooltip("Stamina paid to play this card from hand.")]
    public int energyCost;

    [Header("Deck Building")]
    [Tooltip("Point cost to include this card in a deck. Used by deck building later; ignored during play.")]
    public int deckCost = 1;

    public int damageMin;
    public int damageMax;

    public int strRequired;
    public int intRequired;
    public int wisRequired;
    public int dexRequired;

    public string effectDescription;

    [Header("Effects")]
    [Tooltip("Everything this card does. OnPlay fires when played; Activated fires when you click it in play; the rest fire on their trigger.")]
    public List<CardAbility> abilities = new List<CardAbility>();

    // Serialized as ints on every card asset — append only, never reorder.
    public enum CardType
    {
        Accesory,
        Armour,
        Attack,
        Aura,
        Condition,
        Consumable,
        Equipment,
        Shield,
        Skill,
        Spell,
        Talent,
        Weapon,

        // The cleric's answer to the mage's Spell: resolves then goes to the discard pile.
        Miracle,
    }

    public enum SpeedType
    {
        Reflex,
        Channel
    }

    /// Anything that gets equipped and can therefore be targeted by equipment removal
    /// (Earthquake, Sunder, Disarm, Bait and Switch, Slingshot's cost).
    public bool IsEquipment
    {
        get
        {
            switch (cardType)
            {
                case CardType.Weapon:
                case CardType.Armour:
                case CardType.Accesory:
                case CardType.Shield:
                case CardType.Consumable:
                case CardType.Equipment:
                    return true;
                default:
                    return false;
            }
        }
    }

    public bool IsSpell => cardType == CardType.Spell;
    public bool IsMiracle => cardType == CardType.Miracle;

    /// True for cards that STAY on the battlefield once played. Everything else resolves and goes
    /// straight to the discard pile. Drives which frame the card art uses, and mirrors the zone
    /// routing in Player.ZoneFor — keep the two in step.
    public bool IsPermanent
    {
        get
        {
            switch (cardType)
            {
                case CardType.Weapon:
                case CardType.Armour:
                case CardType.Shield:
                case CardType.Equipment:
                case CardType.Accesory:
                case CardType.Talent:
                case CardType.Aura:
                case CardType.Condition:   // sits in the opponent's aura zone, but it does stay
                    return true;
                default:
                    return false;
            }
        }
    }

    /// Whether a cost modifier with this scope applies to this card.
    public bool MatchesCostScope(CostScope scope)
    {
        switch (scope)
        {
            case CostScope.StrikeCards: return GrantsStrike;
            case CostScope.Spells:      return IsSpell;
            case CostScope.Miracles:    return IsMiracle;
            case CostScope.Skills:      return cardType == CardType.Skill;
            case CostScope.AllCards:    return true;
            default:                    return false;
        }
    }

    // Combat cards (weapons/attacks) can only be activated during your Combat phase.
    public bool IsCombatCard
        => cardType == CardType.Weapon || cardType == CardType.Attack;

    /// True when playing this card lets you strike with a weapon (Light Swing, Heavy Swing, Hurl).
    /// Cost-reduction talents key off this rather than the card type, so an Attack that doesn't
    /// actually strike is not discounted.
    public bool GrantsStrike => HasEffect(EffectKind.Strike);

    public CardAbility FirstActivated()
    {
        if (abilities == null) return null;
        foreach (var a in abilities)
            if (a != null && a.trigger == Trigger.Activated)
                return a;
        return null;
    }

    /// Whether this card has anything to run for `trigger`. Matches EffectRunner's own filter, so
    /// callers can skip work the runner would find nothing to do for.
    public bool HasAbilityFor(Trigger trigger)
    {
        if (abilities == null) return false;
        foreach (var a in abilities)
            if (a != null && a.trigger == trigger && a.effect != EffectKind.None)
                return true;
        return false;
    }

    private bool HasEffect(EffectKind effect)
    {
        if (abilities == null) return false;
        foreach (var a in abilities)
            if (a != null && a.effect == effect)
                return true;
        return false;
    }
}
