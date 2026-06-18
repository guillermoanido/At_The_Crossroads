using System.Collections;
using UnityEngine;

#region Damage & Healing

[CreateAssetMenu(menuName = "Card Effects/Deal Damage", fileName = "DealDamage")]
public class DealDamageEffect : CardEffect
{
    public enum DamageTarget { Opponent, Controller }

    public DamageTarget target = DamageTarget.Opponent;
    public int amount = 1;

    public override void ResolveImmediate(EffectContext ctx)
    {
        if (amount <= 0) return;
        var defender = target == DamageTarget.Opponent ? ctx.opponent : ctx.controller;
        if (defender == null) return;
        defender.TakeDamage(amount, ctx.sourceCardGO, ctx.sourceCardData);
    }
}

[CreateAssetMenu(menuName = "Card Effects/Gain Life", fileName = "GainLife")]
public class GainLifeEffect : CardEffect
{
    public int amount = 1;

    public override void ResolveImmediate(EffectContext ctx)
    {
        if (amount <= 0 || ctx.controller == null) return;
        ctx.controller.AdjustHp(amount);
    }
}

[CreateAssetMenu(menuName = "Card Effects/Reduce Incoming Damage", fileName = "ReduceIncomingDamage")]
public class ReduceIncomingDamageEffect : CardEffect
{
    public int amount = 1;

    public override void ResolveImmediate(EffectContext ctx)
    {
        if (ctx.damage == null) return;
        ctx.damage.amount = Mathf.Max(0, ctx.damage.amount - amount);
    }
}

#endregion

#region Targeted Removal

[CreateAssetMenu(menuName = "Card Effects/Destroy Target Card", fileName = "DestroyTargetCard")]
public class DestroyTargetCardEffect : CardEffect
{
    public override IEnumerator Resolve(EffectContext ctx)
    {
        if (TargetingService.Instance == null) yield break;

        bool done = false;
        TargetingService.Instance.Request(
            filter: t => TargetFilters.IsOpponentCardInPlay(t, ctx.controller),
            onChosen: t => { t.Owner.SendToDiscard(t.gameObject); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }
}

[CreateAssetMenu(menuName = "Card Effects/Return Target To Opponent's Hand", fileName = "ReturnToOpponentHand")]
public class ReturnToOpponentHandEffect : CardEffect
{
    public override IEnumerator Resolve(EffectContext ctx)
    {
        if (TargetingService.Instance == null) yield break;

        bool done = false;
        TargetingService.Instance.Request(
            filter: t => TargetFilters.IsOpponentCardInPlay(t, ctx.controller),
            onChosen: t => { t.Owner.ReturnToHand(t.gameObject); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }
}

#endregion
