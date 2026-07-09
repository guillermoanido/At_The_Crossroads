using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The single place that runs every card's effects. Each Card's `abilities` list says WHAT it does
// and WHEN (its trigger); this resolves those against the game state. Add a new card behaviour by
// adding one EffectKind case here — no per-effect asset files.
public class EffectRunner : MonoBehaviour
{
    public static EffectRunner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ---- Entry points --------------------------------------------------

    // Run every ability on `card` whose trigger matches, in order. Async: target-picking pauses here.
    public void FireAbilities(Card card, EffectContext ctx, Trigger trigger)
    {
        var matches = Collect(card, trigger);
        if (matches.Count > 0) StartCoroutine(RunSequence(matches, ctx));
    }

    // Synchronous variant for damage-modifying triggers (OnControllerTakeDamage) that must resolve
    // before HP is touched. Only instant effects are meaningful here; target-picking is skipped.
    public void FireAbilitiesImmediate(Card card, EffectContext ctx, Trigger trigger)
    {
        foreach (var a in Collect(card, trigger))
            ApplyInstant(a, ctx);
    }

    private static List<CardAbility> Collect(Card card, Trigger trigger)
    {
        var list = new List<CardAbility>();
        if (card == null || card.abilities == null) return list;
        foreach (var a in card.abilities)
            if (a != null && a.trigger == trigger && a.effect != EffectKind.None)
                list.Add(a);
        return list;
    }

    private IEnumerator RunSequence(List<CardAbility> abilities, EffectContext ctx)
    {
        foreach (var a in abilities)
            yield return ResolveOne(a, ctx);
    }

    private IEnumerator ResolveOne(CardAbility a, EffectContext ctx)
    {
        switch (a.effect)
        {
            case EffectKind.DestroyTargetCard:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentCardInPlay(t, ctx.controller), t => t.Owner.SendToDiscard(t.gameObject));
                break;
            case EffectKind.DestroyTargetEquipment:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentEquipmentInPlay(t, ctx.controller), t => t.Owner.SendToDiscard(t.gameObject));
                break;
            case EffectKind.ReturnTargetToHand:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentCardInPlay(t, ctx.controller), t => t.Owner.ReturnToHand(t.gameObject));
                break;
            case EffectKind.ReturnTargetEquipmentToHand:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentEquipmentInPlay(t, ctx.controller), t => t.Owner.ReturnToHand(t.gameObject));
                break;
            default:
                ApplyInstant(a, ctx);
                break;
        }
    }

    // ---- Instant effects (safe to call synchronously) ------------------

    private void ApplyInstant(CardAbility a, EffectContext ctx)
    {
        switch (a.effect)
        {
            case EffectKind.DealDamage:
            {
                var defender = a.target == EffectTarget.Opponent ? ctx.opponent : ctx.controller;
                if (defender != null && a.amount > 0)
                    defender.TakeDamage(a.amount, ctx.sourceCardGO, ctx.sourceCardData);
                break;
            }
            case EffectKind.GainBlock:
                if (ctx.controller != null) ctx.controller.AdjustDefense(a.amount);
                break;
            case EffectKind.GainLife:
                if (ctx.controller != null) ctx.controller.AdjustHp(a.amount);
                break;
            case EffectKind.DrawCards:
                for (int i = 0; i < a.amount && ctx.controller != null; i++) ctx.controller.DrawCard();
                break;
            case EffectKind.GainStamina:
                if (ctx.controller != null) ctx.controller.AdjustStamina(a.amount);
                break;
            case EffectKind.Scry:
                if (ScryPanel.Instance != null && ctx.controller != null && ctx.controller.deckManager != null)
                    ScryPanel.Instance.Open(ctx.controller.deckManager, a.amount);
                break;
            case EffectKind.ReduceIncomingDamage:
                if (ctx.damage != null) ctx.damage.amount = Mathf.Max(0, ctx.damage.amount - a.amount);
                break;
            case EffectKind.LoseStamina:
                if (ctx.controller != null) ctx.controller.AdjustStamina(-a.amount);
                break;
            case EffectKind.DestroyAllOpponentEquipment:
                if (ctx.opponent != null) ctx.opponent.DestroyAllEquipment();
                break;
            case EffectKind.OpponentDiscards:
                if (ctx.opponent != null) ctx.opponent.DiscardFromHand(a.amount);
                break;
            case EffectKind.TakeExtraTurn:
                if (GameManager.Instance != null) GameManager.Instance.QueueExtraTurn();
                break;
        }
    }

    // ---- Targeted effects (async — wait for the player to pick) --------

    private IEnumerator PickTarget(EffectContext ctx, System.Predicate<Targetable> filter, System.Action<Targetable> onChosen)
    {
        if (TargetingService.Instance == null) yield break;

        bool done = false;
        TargetingService.Instance.Request(
            filter: filter,
            onChosen: t => { if (t != null && t.Owner != null) onChosen(t); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }
}
