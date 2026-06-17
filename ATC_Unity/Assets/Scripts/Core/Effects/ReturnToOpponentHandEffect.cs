using System.Collections;
using UnityEngine;

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
