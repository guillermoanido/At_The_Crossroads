using UnityEngine;


[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class Card : ScriptableObject 
{
    public string cardName;
    public CardType cardType;
    public SpeedType speedType;

    public int energyCost;

    public int damageMin;
    public int damageMax;

    public int strRequired;
    public int intRequired;
    public int wisRequired;
    public int dexRequired;

    public string effectDescription;

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

}
