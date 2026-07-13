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
        Weapon
    }

    public enum SpeedType
    {
        Reflex,
        Channel
    }

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
                case CardType.Condition:
                    return true;
                default:
                    return false;
            }
        }
    }

    public bool IsEquipment
        => cardType == CardType.Weapon || cardType == CardType.Accesory || cardType == CardType.Armour;

    public CardAbility FirstActivated()
    {
        if (abilities == null) return null;
        foreach (var a in abilities)
            if (a != null && a.trigger == Trigger.Activated)
                return a;
        return null;
    }
}
