using System;
using UnityEngine;

public enum Trigger
{
    OnPlay,
    OnUpkeep,
    OnControllerTakeDamage,
    OnDestroyed
}

[Serializable]
public class TriggeredEffect
{
    public Trigger trigger;
    public CardEffect effect;
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
}
