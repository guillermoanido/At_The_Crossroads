using System;
using System.Collections;
using UnityEngine;

#region Triggers & Events

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
}

#endregion

#region CardEffect Base

public abstract class CardEffect : ScriptableObject
{
    public virtual void ResolveImmediate(EffectContext ctx) { }

    public virtual IEnumerator Resolve(EffectContext ctx)
    {
        ResolveImmediate(ctx);
        yield break;
    }
}

#endregion
