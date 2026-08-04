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


    public void FireAbilities(Card card, EffectContext ctx, Trigger trigger)
    {
        StartCoroutine(RunAbilities(card, ctx, trigger));
    }

    public IEnumerator RunAbilities(Card card, EffectContext ctx, Trigger trigger)
    {
        yield return RunSequence(Collect(card, trigger), ctx);
    }

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
        {
            Debug.Log($"[Effect] {Describe(a, ctx)}");
            yield return ResolveOne(a, ctx);
        }
    }

    // One readable line per ability so the console reads like a play-by-play: who is doing what,
    // to whom, and how big it is.
    private static string Describe(CardAbility a, EffectContext ctx)
    {
        string source = ctx.sourceCardData != null ? ctx.sourceCardData.cardName : "?";
        string controller = ctx.controller != null ? ctx.controller.name : "?";
        string target = a.target == EffectTarget.Opponent
            ? (ctx.opponent != null ? ctx.opponent.name : "opponent")
            : controller;

        return $"{controller} resolves {source}: {a.effect} {a.amount} → {target}";
    }

    private IEnumerator ResolveOne(CardAbility a, EffectContext ctx)
    {
        switch (a.effect)
        {
            case EffectKind.DestroyTargetCard:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentCardInPlay(t, ctx.controller),
                    "Choose an enemy card in play to destroy", t => t.Owner.SendToDiscard(t.gameObject));
                break;
            case EffectKind.DestroyTargetEquipment:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentEquipmentInPlay(t, ctx.controller),
                    "Choose an enemy equipment to destroy", t => t.Owner.SendToDiscard(t.gameObject));
                break;
            case EffectKind.ReturnTargetToHand:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentCardInPlay(t, ctx.controller),
                    "Choose an enemy card in play to return to its owner's hand", t => t.Owner.ReturnToHand(t.gameObject));
                break;
            case EffectKind.ReturnTargetEquipmentToHand:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentEquipmentInPlay(t, ctx.controller),
                    "Choose an enemy equipment to return to its owner's hand", t => t.Owner.ReturnToHand(t.gameObject));
                break;
            case EffectKind.Scry:
                yield return DoScry(ctx, a.amount);
                break;
            case EffectKind.Strike:
                yield return DoStrike(ctx, a);
                break;
            default:
                ApplyInstant(a, ctx);
                break;
        }
    }


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
                if (ctx.controller != null) ctx.controller.GainStamina(a.amount);
                break;
            case EffectKind.IncreaseMaxStamina:
                if (ctx.controller != null) ctx.controller.IncreaseMaxStamina(a.amount);
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


    // Pause the effect until its controller picks a card. NetworkTargeting decides WHICH machine
    // is asked: offline (and for the host's own player) that is this one, otherwise the prompt is
    // sent to the client that owns the card and the answer comes back over the wire.
    private IEnumerator PickTarget(EffectContext ctx, System.Predicate<Targetable> filter, string prompt,
                                   System.Action<Targetable> onChosen)
    {
        bool done = false;
        NetworkTargeting.Request(
            chooser: ctx.controller,
            filter: filter,
            prompt: prompt,
            onChosen: t => { if (t != null && t.Owner != null) onChosen(t); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }

    private IEnumerator DoScry(EffectContext ctx, int count)
    {
        if (ctx.controller == null || ctx.controller.scryPanel == null || ctx.controller.deckManager == null) yield break;

        var panel = ctx.controller.scryPanel;
        panel.Open(ctx.controller.deckManager, count);
        yield return new WaitUntil(() => !panel.IsOpen);
    }

    // Strike: pick one of YOUR weapons in play and swing with it. Fizzles if you have no weapon.
    private IEnumerator DoStrike(EffectContext ctx, CardAbility strike)
    {
        if (ctx.controller == null) yield break;

        yield return PickTarget(
            ctx,
            t => TargetFilters.IsOwnWeaponInPlay(t, ctx.controller),
            "Choose one of your weapons to strike with",
            weapon => ResolveStrike(ctx, strike, weapon));
    }

    // Striking runs the weapon's OWN activated ability — for free, and without tapping it, which is
    // the whole point of a strike card. So the weapon does everything it normally does (a Greatclub
    // deals its 4), with the strike card's damage multiplier and bonus folded in on top.
    private void ResolveStrike(EffectContext ctx, CardAbility strike, Targetable weapon)
    {
        var weaponContext = ctx.controller.BuildContext(weapon.gameObject, weapon.Data);
        bool struck = false;

        foreach (var ability in ActivatedAbilities(weapon.Data))
        {
            var scaled = ScaleForStrike(ability, strike);
            if (scaled.effect == EffectKind.DealDamage)
                Debug.Log($"[Strike] {ctx.controller.name} strikes with {weapon.Data?.cardName}: " +
                          $"{ability.amount} × {Mathf.Max(1, strike.strikeDamageMultiplier)} " +
                          $"{(strike.strikeBonusDamage >= 0 ? "+" : "-")} {Mathf.Abs(strike.strikeBonusDamage)} " +
                          $"= {scaled.amount} damage");

            ApplyInstant(scaled, weaponContext);
            struck = true;
        }

        if (!struck)
            Debug.Log($"[Strike] {weapon.Data?.cardName} has no activated ability to strike with.");

        var owner = weapon.Owner;
        if (strike.strikeDestroysWeapon && owner != null) owner.SendToDiscard(weapon.gameObject);
    }

    private static IEnumerable<CardAbility> ActivatedAbilities(Card weapon)
    {
        if (weapon == null || weapon.abilities == null) yield break;
        foreach (var ability in weapon.abilities)
            if (ability != null && ability.trigger == Trigger.Activated && ability.effect != EffectKind.None)
                yield return ability;
    }

    // A copy of the weapon's ability with the strike card's scaling applied. Only damage scales —
    // Hurl doubles it, Heavy Swing adds 2; everything else the weapon does happens as printed.
    private static CardAbility ScaleForStrike(CardAbility weaponAbility, CardAbility strike)
    {
        if (weaponAbility.effect != EffectKind.DealDamage) return weaponAbility;

        int multiplier = Mathf.Max(1, strike.strikeDamageMultiplier);
        return new CardAbility
        {
            trigger = weaponAbility.trigger,
            effect = weaponAbility.effect,
            amount = Mathf.Max(0, weaponAbility.amount * multiplier + strike.strikeBonusDamage),
            target = weaponAbility.target,
        };
    }
}
