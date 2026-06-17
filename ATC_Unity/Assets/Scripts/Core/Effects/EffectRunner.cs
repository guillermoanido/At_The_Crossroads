using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectRunner : MonoBehaviour
{
    public static EffectRunner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RunSequence(IList<CardEffect> effects, EffectContext ctx)
    {
        if (effects == null || effects.Count == 0) return;
        StartCoroutine(RunSequenceCoroutine(effects, ctx));
    }

    private IEnumerator RunSequenceCoroutine(IList<CardEffect> effects, EffectContext ctx)
    {
        foreach (var e in effects)
        {
            if (e == null) continue;
            yield return e.Resolve(ctx);
        }
    }
}
