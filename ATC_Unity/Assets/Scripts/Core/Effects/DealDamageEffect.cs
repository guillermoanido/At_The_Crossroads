using UnityEngine;

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
