using System.Collections;
using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    public virtual void ResolveImmediate(EffectContext ctx) { }

    public virtual IEnumerator Resolve(EffectContext ctx)
    {
        ResolveImmediate(ctx);
        yield break;
    }
}
