using UnityEngine;

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
