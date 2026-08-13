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
        // A weapon that was struck earlier this turn cashes the promise in on this activation.
        StrikeBonus bonus = default;
        bool struck = trigger == Trigger.Activated && StrikeCharge.TryTake(ctx.sourceCardGO, out bonus);

        var abilities = Collect(card, trigger);
        if (struck)
        {
            abilities = ApplyStrikeBonus(abilities, bonus);
            Debug.Log($"[Strike] {card?.cardName} activates with its Strike bonus " +
                      $"(×{bonus.DamageMultiplier} {(bonus.BonusDamage >= 0 ? "+" : "-")} {Mathf.Abs(bonus.BonusDamage)}).");
        }

        yield return RunSequence(abilities, ctx);

        if (struck && bonus.DestroysWeapon)
            Targetable.OwnerOf(ctx.sourceCardGO)?.SendToDiscard(ctx.sourceCardGO);
    }

    // Damage abilities get the strike scaling; everything else the weapon does is untouched.
    private static List<CardAbility> ApplyStrikeBonus(List<CardAbility> abilities, StrikeBonus bonus)
    {
        var scaled = new List<CardAbility>(abilities.Count);
        foreach (var ability in abilities)
        {
            if (ability.effect != EffectKind.DealDamage) { scaled.Add(ability); continue; }

            scaled.Add(new CardAbility
            {
                trigger = ability.trigger,
                effect = ability.effect,
                amount = bonus.Scale(ability.amount),
                target = ability.target,
                amountSource = ability.amountSource,
            });
        }
        return scaled;
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
            case EffectKind.DestroyTargetCondition:
                yield return PickTarget(ctx, TargetFilters.IsConditionInPlay,
                    "Choose a condition to destroy", t => t.Owner.SendToDiscard(t.gameObject));
                break;
            case EffectKind.ReturnTargetFromDiscardToHand:
                yield return PickTarget(ctx, t => TargetFilters.IsOwnSkillOrSpellInDiscard(t, ctx.controller),
                    "Choose a Skill or Spell in your discard to take back", t => t.Owner.ReturnToHand(t.gameObject));
                break;
            case EffectKind.CastTargetSpellFromDiscard:
                yield return PickTarget(ctx, t => TargetFilters.IsOwnSpellInDiscard(t, ctx.controller),
                    "Choose a Spell in your discard to cast", t => CastFromDiscard(ctx, t));
                break;
            case EffectKind.DestroyTargetOnStack:
                yield return PickTarget(ctx, TargetFilters.IsOnStack,
                    "Choose a card on the stack to destroy", t => CounterOnStack(t, toHand: false));
                break;
            case EffectKind.ReturnTargetOnStackToHand:
                yield return PickTarget(ctx, TargetFilters.IsOnStack,
                    "Choose a card on the stack to return to its owner's hand", t => CounterOnStack(t, toHand: true));
                break;
            case EffectKind.OpponentDiscards:
                yield return DoDiscard(ctx.opponent, EffectiveAmount(a, ctx));
                break;
            case EffectKind.Scry:
                yield return DoScry(ctx, EffectiveAmount(a, ctx));
                break;
            case EffectKind.Strike:
                yield return DoStrike(ctx, a);
                break;
            default:
                ApplyInstant(a, ctx);
                break;
        }
    }

    /// An ability's real magnitude: its authored `amount` plus whatever its AmountSource counts.
    /// "X + 1" is amount = 1 with the matching source.
    public static int EffectiveAmount(CardAbility a, EffectContext ctx)
    {
        int bonus = 0;
        switch (a.amountSource)
        {
            case AmountSource.SpellsInYourDiscard:
                bonus = CountSpellsInDiscard(ctx.controller);
                break;
            case AmountSource.SpellsYouPlayedThisTurn:
                bonus = ctx.controller != null ? ctx.controller.SpellsPlayedThisTurn : 0;
                break;
        }

        if (ConditionHolds(a.bonusCondition, ctx)) bonus += a.conditionalBonus;
        return a.amount + bonus;
    }

    private static bool ConditionHolds(AmountCondition condition, EffectContext ctx)
    {
        switch (condition)
        {
            case AmountCondition.IfYouPlayedAReflexCardThisTurn:
                return ctx.controller != null && ctx.controller.ReflexCardsPlayedThisTurn > 0;
            default:
                return false;
        }
    }

    private static int CountSpellsInDiscard(Player player)
    {
        var discard = player != null ? player.discardZone : null;
        if (discard == null) return 0;

        int spells = 0;
        foreach (var cardGO in discard.Cards)
        {
            var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
            if (data != null && data.IsSpell) spells++;
        }
        return spells;
    }

    // Countering: take the item off the stack so it never resolves, then dispose of the card. The
    // card object is already sitting in whatever zone it was played to, so move it from there.
    private static void CounterOnStack(Targetable target, bool toHand)
    {
        if (GameStack.Instance == null) return;

        var removed = GameStack.Instance.RemoveCard(target.gameObject);
        if (removed == null) return;

        var owner = removed.controller != null ? removed.controller : target.Owner;
        if (owner == null) return;

        Debug.Log($"[Stack] {removed.sourceCardData?.cardName} was countered — " +
                  $"{(toHand ? "returned to hand" : "destroyed")}.");

        if (toHand) owner.ReturnToHand(target.gameObject);
        else owner.SendToDiscard(target.gameObject);
    }

    // Casting out of the discard pile: pay the spell's current cost, run its OnPlay abilities, then
    // remove it from the game. Simplification of "you may cast it until end of turn" — you cast it
    // right now rather than gaining lasting permission.
    private void CastFromDiscard(EffectContext ctx, Targetable target)
    {
        var caster = ctx.controller;
        var spell = target.Data;
        if (caster == null || spell == null) return;

        int cost = caster.StaminaCostOf(spell);
        if (!caster.SpendStamina(cost))
        {
            Debug.Log($"[Effect] {caster.name} cannot afford {spell.cardName} from the discard ({cost}).");
            return;
        }

        Debug.Log($"[Effect] {caster.name} casts {spell.cardName} from the discard for {cost}, then exiles it.");
        FireAbilities(spell, caster.BuildContext(target.gameObject, spell), Trigger.OnPlay);
        caster.SendToExile(target.gameObject);
    }


    private void ApplyInstant(CardAbility a, EffectContext ctx)
    {
        int amount = EffectiveAmount(a, ctx);
        var aimed = a.target == EffectTarget.Opponent ? ctx.opponent : ctx.controller;

        switch (a.effect)
        {
            case EffectKind.DealDamage:
                if (aimed != null && amount > 0)
                    aimed.TakeDamage(amount, ctx.sourceCardGO, ctx.sourceCardData);
                break;
            case EffectKind.GainBlock:
                ctx.controller?.AdjustDefense(amount);
                break;
            case EffectKind.GainLife:
                ctx.controller?.AdjustHp(amount);
                break;
            case EffectKind.DrawCards:
                for (int i = 0; i < amount && ctx.controller != null; i++) ctx.controller.DrawCard();
                break;
            case EffectKind.DrawEntireDeck:
                ctx.controller?.DrawEntireDeck();
                break;
            case EffectKind.GainStamina:
                ctx.controller?.GainStamina(amount);
                break;
            case EffectKind.IncreaseMaxStamina:
                ctx.controller?.IncreaseMaxStamina(amount);
                break;
            case EffectKind.ReduceIncomingDamage:
                if (ctx.damage != null) ctx.damage.amount = Mathf.Max(0, ctx.damage.amount - amount);
                break;
            case EffectKind.LoseStamina:
                ctx.controller?.AdjustStamina(-amount);
                break;
            case EffectKind.DestroyAllOpponentEquipment:
                ctx.opponent?.DestroyAllEquipment();
                break;
            case EffectKind.OpponentDiscards:
                // Fallback only: the normal path is DoDiscard, which lets the discarding player
                // choose. This branch is reached from the synchronous damage-trigger path, which
                // cannot pause for a prompt, so it takes from the end of the hand.
                ctx.opponent?.DiscardFromHand(amount);
                break;
            case EffectKind.TakeExtraTurn:
                GameManager.Instance?.QueueExtraTurn();
                break;

            case EffectKind.ApplyBurn:
                aimed?.AddBurn(amount);
                break;
            case EffectKind.ApplyBleed:
                aimed?.AddBleed(amount);
                break;
            case EffectKind.RemoveAllBurnAndHeal:
            {
                int cleared = aimed != null ? aimed.ClearBurn() : 0;
                aimed?.AdjustHp(cleared);
                Debug.Log($"[Effect] {aimed?.name} shed {cleared} Burn and healed for it.");
                break;
            }
            case EffectKind.GainDivineShield:
                ctx.controller?.GainDivineShield(amount);
                break;
            case EffectKind.RemoveAllDebuffs:
                ctx.controller?.RemoveAllDebuffs();
                break;
            case EffectKind.RevealOpponentHand:
                RevealHand(ctx.opponent);
                break;

            case EffectKind.IncreaseNextOpponentCardCost:
                ctx.opponent?.AddNextCardSurcharge(amount);
                break;
            case EffectKind.LockOpponentReflex:
                ctx.opponent?.LockReflex();
                break;
            case EffectKind.FreeCostsButNoDraw:
                ctx.controller?.MakeCostsFreeButBlockDraws();
                break;
            case EffectKind.GainStaminaNextUpkeep:
                ctx.controller?.QueueUpkeepStamina(amount);
                break;

            case EffectKind.AvoidNextDirectDamage:
                ctx.controller?.AvoidNextDirectDamage();
                break;
            case EffectKind.NextCardAtReflexSpeed:
                ctx.controller?.GrantNextCardReflexSpeed();
                break;

            case EffectKind.DestroySelf:
                if (ctx.sourceCardGO != null)
                    Targetable.OwnerOf(ctx.sourceCardGO)?.SendToDiscard(ctx.sourceCardGO);
                break;
            case EffectKind.WinIfDeckAndHandEmpty:
                CheckAscension(ctx.controller);
                break;

            case EffectKind.RearrangeStack:
                // Needs a drag-to-reorder panel over GameStack's items; the rules hooks exist
                // (GameStack.RemoveCard / Push) but there is no UI to drive them yet.
                Debug.LogWarning("[Effect] Rearrange the stack is not implemented yet — Timewatch does nothing.");
                break;
        }
    }

    // No reveal UI yet, so the opponent's hand goes to the console. Whoever is reading the log is
    // the player who cast it; a proper panel is the obvious next step.
    private static void RevealHand(Player opponent)
    {
        var hand = opponent != null ? opponent.handManager : null;
        if (hand == null) return;

        var names = new List<string>();
        foreach (var cardGO in hand.cardsInHand)
        {
            var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
            names.Add(data != null ? data.cardName : "?");
        }
        Debug.Log($"[Reveal] {opponent.name}'s hand ({names.Count}): {string.Join(", ", names)}");
    }

    private static void CheckAscension(Player player)
    {
        if (player == null || GameManager.Instance == null) return;

        bool deckEmpty = player.deckManager == null || !player.deckManager.HasCards;
        bool handEmpty = player.handManager == null || player.handManager.cardsInHand.Count == 0;

        if (deckEmpty && handEmpty)
            GameManager.Instance.DeclareWinner(player, "Ascension with an empty deck and hand");
        else
            Debug.Log($"[Effect] Ascension fizzles — {player.name} still has cards.");
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

    // Discarding is a choice: the player losing the cards picks which ones go, one prompt per card.
    // NetworkTargeting sends the prompt to THAT player's machine, not the caster's.
    private IEnumerator DoDiscard(Player discarder, int count)
    {
        if (discarder == null || discarder.handManager == null) yield break;

        for (int i = 0; i < count; i++)
        {
            if (discarder.handManager.cardsInHand.Count == 0) yield break;

            bool done = false;
            NetworkTargeting.Request(
                chooser: discarder,
                filter: t => TargetFilters.IsInHandOf(t, discarder),
                prompt: $"Choose a card to discard ({count - i} left)",
                onChosen: t => { discarder.SendToDiscard(t.gameObject); done = true; },
                onCancel: () => done = true);

            yield return new WaitUntil(() => done);
        }
    }

    private IEnumerator DoScry(EffectContext ctx, int count)
    {
        if (ctx.controller == null || ctx.controller.scryPanel == null || ctx.controller.deckManager == null) yield break;

        var panel = ctx.controller.scryPanel;
        panel.Open(ctx.controller, count);
        yield return new WaitUntil(() => !panel.IsOpen);
    }

    /// A full equipment slot took another card — the owner chooses which one it replaces.
    public void RequestZoneReplacement(Player owner, CardZone zone)
        => StartCoroutine(ChooseReplacement(owner, zone));

    private IEnumerator ChooseReplacement(Player owner, CardZone zone)
    {
        bool done = false;
        NetworkTargeting.Request(
            chooser: owner,
            filter: t => TargetFilters.IsInZone(t, zone),
            prompt: $"{zone.name} is full — choose the card this one replaces",
            onChosen: t => { owner.SendToDiscard(t.gameObject); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }

    // Strike: pick one of YOUR weapons, untap it, and promise its next activation an extra effect.
    // The strike card itself deals no damage — you still have to activate the weapon to swing.
    // Fizzles if you have no weapon in play.
    private IEnumerator DoStrike(EffectContext ctx, CardAbility strike)
    {
        if (ctx.controller == null) yield break;

        yield return PickTarget(
            ctx,
            t => TargetFilters.IsOwnWeaponInPlay(t, ctx.controller),
            "Choose one of your weapons to Strike with — it untaps and its next activation is stronger",
            weapon => ChargeWeapon(ctx, strike, weapon));
    }

    private static void ChargeWeapon(EffectContext ctx, CardAbility strike, Targetable weapon)
    {
        weapon.GetComponent<CardTapState>()?.Untap();
        StrikeCharge.Apply(weapon.gameObject, strike);

        Debug.Log($"[Strike] {ctx.controller.name} strikes with {weapon.Data?.cardName}: untapped, " +
                  $"next activation ×{Mathf.Max(1, strike.strikeDamageMultiplier)} " +
                  $"{(strike.strikeBonusDamage >= 0 ? "+" : "-")} {Mathf.Abs(strike.strikeBonusDamage)}" +
                  $"{(strike.strikeDestroysWeapon ? ", then destroyed" : "")}.");
    }
}
