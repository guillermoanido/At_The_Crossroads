using UnityEngine;

public class Card : ScriptableObject 
{
    public string cardName;
    public CardType cardType;

    public int energyCost;

    public int speed;
    public int damage;

    public int strRequired;
    public int intRequired;
    public int wisRequired;
    public int dexRequired;

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
    }



}
