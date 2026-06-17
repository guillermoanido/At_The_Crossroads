using UnityEngine;

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
