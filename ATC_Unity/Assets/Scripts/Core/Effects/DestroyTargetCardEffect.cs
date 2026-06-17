using System.Collections;
using UnityEngine;

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
