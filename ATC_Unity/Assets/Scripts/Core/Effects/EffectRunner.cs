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
        var abilities = Collect(card, trigger);

        // Activating a weapon that a Strike readied spends one charge: this activation resolves
        // with the Strike card's modifiers folded in.
        var tap = ctx.sourceCardGO != null ? ctx.sourceCardGO.GetComponent<CardTapState>() : null;
        StrikeBuff buff = null;
        if (trigger == Trigger.Activated && tap != null && tap.HasPendingStrike)
        {
            buff = tap.ConsumePendingStrike();
            abilities = ScaleAllForStrike(abilities, buff);
            Debug.Log($"[Strike] {card?.cardName} activates with a Strike charge " +
                      $"(x{buff.multiplier} {(buff.bonus >= 0 ? "+" : "-")}{Mathf.Abs(buff.bonus)}" +
                      $"{(buff.destroysWeapon ? ", destroys itself" : "")}).");
        }

        yield return RunSequence(abilities, ctx);

        if (buff == null) yield break;

        if (buff.destroysWeapon)
        {
            Debug.Log($"[Strike] {card?.cardName} is destroyed by the Strike that used it.");
            Targetable.OwnerOf(ctx.sourceCardGO)?.SendToDiscard(ctx.sourceCardGO);
        }
        else if (tap.HasPendingStrike)
        {
            // Another Strike is still queued (Double Strike), so ready the weapon again.
            tap.Untap();
        }
    }

    private static List<CardAbility> ScaleAllForStrike(List<CardAbility> abilities, StrikeBuff buff)
    {
        var scaled = new List<CardAbility>(abilities.Count);
        foreach (var a in abilities) scaled.Add(ScaleForStrike(a, buff.multiplier, buff.bonus));
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
        // "Strike. Strike." picks ONE weapon and uses it twice, so every Strike this card resolves
        // shares the same choice — the player is only asked once.
        var strikeTarget = new StrikeTarget();

        foreach (var a in abilities)
        {
            Debug.Log($"[Effect] {Describe(a, ctx)}");
            yield return ResolveOne(a, ctx, strikeTarget);

            if (ctx.aborted)
            {
                Debug.Log($"[Effect] {ctx.sourceCardData?.cardName} fizzles — a cost went unpaid.");
                ctx.aborted = false;
                yield break;
            }
        }
    }

    /// The weapon the first Strike picked, reused by any that follow on the same card.
    private class StrikeTarget { public Targetable Weapon; }

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

    private IEnumerator ResolveOne(CardAbility a, EffectContext ctx, StrikeTarget strikeTarget)
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
            case EffectKind.ReturnOwnEquipmentToHand:
                yield return PickTarget(ctx, t => TargetFilters.IsOwnEquipmentInPlay(t, ctx.controller),
                    "Choose one of your equipment to take back", t => t.Owner.ReturnToHand(t.gameObject));
                break;
            case EffectKind.StealCopyOfOpponentEquipment:
                yield return PickTarget(ctx, t => TargetFilters.IsOpponentEquipmentInPlay(t, ctx.controller),
                    "Choose an enemy equipment to copy", t => CopyThenDestroy(ctx, t));
                break;
            case EffectKind.SetAsideCardForNextTurn:
                yield return PickOwnHandCard(ctx.controller, "Choose a card to set aside for next turn",
                    t => SetAside(ctx.controller, t));
                break;
            case EffectKind.TakeCardFromOpponentHand:
                yield return DoPickpocket(ctx);
                break;
            case EffectKind.SacrificeEquipment:
                yield return DoSacrificeEquipment(ctx);
                break;
            case EffectKind.RearrangeStack:
                yield return DoRearrangeStack(ctx);
                break;
            case EffectKind.Scry:
                yield return DoScry(ctx, EffectiveAmount(a, ctx));
                break;
            case EffectKind.Strike:
                yield return DoStrike(ctx, a, strikeTarget);
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
                RevealHand(ctx.controller, ctx.opponent);
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
            case EffectKind.NextEquipmentIsFree:
                ctx.controller?.GrantFreeEquipment();
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

    // Lay the opponent's hand open for the player who earned the look, and only for them: their
    // cards turn face-up for the rest of the turn and then go back to being backs.
    private static void RevealHand(Player viewer, Player handOwner)
    {
        var hand = handOwner != null ? handOwner.handManager : null;
        if (hand == null || viewer == null) return;

        var viewerSeat = NetworkPlayerSeat.ForPlayer(viewer);
        if (viewerSeat != null && !viewerSeat.isLocalPlayer)
        {
            // The viewer is on the other machine, where that hand is only blank backs.
            viewerSeat.ServerRevealHandTo(handOwner);
            return;
        }

        hand.RevealUntilEndOfTurn();
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

    // Sleight of Hand: you end up with your own copy of their gear and they lose the original.
    private static void CopyThenDestroy(EffectContext ctx, Targetable target)
    {
        var stolen = target.Data;
        var thief = ctx.controller;
        if (stolen == null || thief == null) return;

        var destination = thief.ZoneForType(stolen.cardType);
        if (destination == null)
        {
            Debug.Log($"[Effect] {thief.name} has nowhere to put a copy of {stolen.cardName}.");
            return;
        }

        thief.PutCardInZone(stolen, destination);
        target.Owner?.SendToDiscard(target.gameObject);
        Debug.Log($"[Effect] {thief.name} copied {stolen.cardName} and destroyed the original.");
    }

    private static void SetAside(Player controller, Targetable target)
    {
        var card = target.Data;
        if (controller == null || card == null) return;

        controller.handManager.RemoveCardFromHand(target.gameObject);
        Destroy(target.gameObject);
        controller.SetAsideForNextTurn(card);
    }

    // Pickpocket: the thief must SEE the hand to choose from it, so the whole hand is revealed to
    // them (and only them) before they pick.
    private IEnumerator DoPickpocket(EffectContext ctx)
    {
        var victim = ctx.opponent;
        if (victim == null || victim.handManager == null || victim.handManager.cardsInHand.Count == 0)
        {
            Debug.Log("[Effect] Nothing to steal — the opponent's hand is empty.");
            yield break;
        }

        RevealHand(ctx.controller, victim);

        bool done = false;
        NetworkTargeting.Request(
            chooser: ctx.controller,
            filter: t => TargetFilters.IsInHandOf(t, victim),
            prompt: "Choose a card to take from your opponent's hand",
            onChosen: t => { ctx.controller.TakeIntoHand(t.gameObject); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }

    // Pick one of the CONTROLLER's own hand cards.
    private IEnumerator PickOwnHandCard(Player controller, string prompt, System.Action<Targetable> onChosen)
    {
        if (controller == null || controller.handManager == null) yield break;
        if (controller.handManager.cardsInHand.Count == 0) yield break;

        bool done = false;
        NetworkTargeting.Request(
            chooser: controller,
            filter: t => TargetFilters.IsInHandOf(t, controller),
            prompt: prompt,
            onChosen: t => { onChosen(t); done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);
    }

    // Discarding is a choice: the player losing the cards picks which ones go, one prompt per card.
    // NetworkTargeting sends the prompt to THAT player's machine, not the caster's.
    // Slingshot's cost. An Equipment on the battlefield is discarded; one already in the discard
    // is removed from the game. Declining, or having nothing to give, aborts the rest of the card.
    private IEnumerator DoSacrificeEquipment(EffectContext ctx)
    {
        var payer = ctx.controller;
        if (payer == null) { ctx.aborted = true; yield break; }

        var payable = TargetingService.Collect(t => TargetFilters.IsOwnEquipmentInPlayOrDiscard(t, payer));
        if (payable.Count == 0)
        {
            Debug.Log($"[Cost] {payer.name} has no Equipment to give up.");
            ctx.aborted = true;
            yield break;
        }

        Targetable chosen = null;
        bool done = false;
        NetworkTargeting.Request(
            chooser: payer,
            filter: t => TargetFilters.IsOwnEquipmentInPlayOrDiscard(t, payer),
            prompt: Localization.T("prompt.sacrifice_equipment"),
            onChosen: t => { chosen = t; done = true; },
            onCancel: () => done = true);

        yield return new WaitUntil(() => done);

        if (chosen == null)
        {
            ctx.aborted = true;
            yield break;
        }

        bool wasInDiscard = TargetFilters.IsInOwnDiscard(chosen, payer);
        if (wasInDiscard) payer.SendToExile(chosen.gameObject);
        else payer.SendToDiscard(chosen.gameObject);

        Debug.Log($"[Cost] {payer.name} gives up {chosen.Data?.cardName} " +
                  $"({(wasInDiscard ? "removed from the game" : "discarded")}).");
    }

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

    // Rearranging happens on the screen of whoever cast it, and the answer is applied by the host.
    private IEnumerator DoRearrangeStack(EffectContext ctx)
    {
        var stack = GameStack.Instance;
        var reorderer = ctx.controller;
        if (stack == null || reorderer == null || stack.Count < 2)
        {
            Debug.Log("[Stack] Nothing worth rearranging.");
            yield break;
        }

        var names = StackCardNames(stack);

        var seat = NetworkPlayerSeat.ForPlayer(reorderer);
        if (seat != null && !seat.isLocalPlayer)
        {
            bool answered = false;
            seat.ServerRequestStackReorder(names, () => answered = true);
            yield return new WaitUntil(() => answered);
            yield break;
        }

        bool done = false;
        StackReorderPanel.Show(names, chosen => { stack.ApplyOrder(ToStackOrder(chosen, names.Count)); done = true; });
        yield return new WaitUntil(() => done);
    }

    /// Stack items in RESOLUTION order — the top of the stack resolves first, so it is listed first.
    public static List<string> StackCardNames(GameStack stack)
    {
        var names = new List<string>();
        var items = stack.Items;
        for (int i = items.Count - 1; i >= 0; i--)
            names.Add(Localization.CardName(items[i]?.sourceCardData));
        return names;
    }

    /// Turn the panel's answer (resolution order) back into stack indices (bottom-up).
    public static int[] ToStackOrder(int[] resolutionOrder, int count)
    {
        if (resolutionOrder == null) return new int[0];

        var stackOrder = new int[resolutionOrder.Length];
        for (int i = 0; i < resolutionOrder.Length; i++)
        {
            // Both the display list and the result are reversed views of the same list.
            int displayIndex = resolutionOrder[resolutionOrder.Length - 1 - i];
            stackOrder[i] = count - 1 - displayIndex;
        }
        return stackOrder;
    }

    // Scry has to happen on the SCRYING player's screen. Effects resolve on the host, so a client's
    // Scry would otherwise open the panel on the host and let them sort the client's deck.
    private IEnumerator DoScry(EffectContext ctx, int count)
    {
        var scryer = ctx.controller;
        if (scryer == null || scryer.deckManager == null || count <= 0) yield break;

        var seat = NetworkPlayerSeat.ForPlayer(scryer);
        if (seat != null && !seat.isLocalPlayer)
        {
            bool done = false;
            seat.ServerRequestScry(count, () => done = true);
            yield return new WaitUntil(() => done);
            yield break;
        }

        if (scryer.scryPanel == null)
        {
            Debug.LogWarning($"[Scry] {scryer.name} has no Scry Panel assigned — the effect does nothing.");
            yield break;
        }

        var panel = scryer.scryPanel;
        panel.Open(scryer, count);
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

    // Strike: pick one of YOUR weapons and USE it — the weapon's own activated ability resolves
    // right now, for free, with this strike card's scaling folded in. The weapon untaps first, so
    // you can strike with it whether or not it already attacked. Fizzles with no weapon in play.
    private IEnumerator DoStrike(EffectContext ctx, CardAbility strike, StrikeTarget shared)
    {
        if (ctx.controller == null) yield break;

        // Only the first Strike on a card asks; the rest ready the same permanent again.
        if (shared.Weapon == null)
        {
            bool done = false;
            NetworkTargeting.Request(
                chooser: ctx.controller,
                filter: t => TargetFilters.IsOwnStrikeTargetInPlay(t, ctx.controller),
                prompt: Localization.T("prompt.strike"),
                onChosen: t => { shared.Weapon = t; done = true; },
                onCancel: () => done = true);

            yield return new WaitUntil(() => done);
        }

        if (shared.Weapon == null) yield break;   // cancelled, or nothing to ready

        // A Reflex Strike card (Backstab) passes its speed on, so the readied permanent can answer
        // out of turn. A Channel one leaves the permanent on its own printed speed.
        bool atReflex = ctx.sourceCardData != null
                     && ctx.sourceCardData.speedType == Card.SpeedType.Reflex;

        ArmStrike(shared.Weapon, strike, atReflex);
    }

    // Strike is an ENABLER, not an attack: it untaps the weapon (or shield) and leaves a charge on
    // it. The player then activates it themselves, and that activation carries the bonus. Because
    // activating taps it again, a Strike is effectively an extra use of that permanent this turn.
    private static void ArmStrike(Targetable weapon, CardAbility strike, bool atReflex)
    {
        if (weapon == null || weapon.Data == null) return;   // destroyed by an earlier strike

        var tap = weapon.GetComponent<CardTapState>();
        if (tap == null)
        {
            Debug.LogWarning($"[Strike] {weapon.Data.cardName} has no CardTapState — cannot be readied.");
            return;
        }

        tap.Untap();
        tap.QueueStrike(new StrikeBuff
        {
            multiplier = Mathf.Max(1, strike.strikeDamageMultiplier),
            bonus = strike.strikeBonusDamage,
            destroysWeapon = strike.strikeDestroysWeapon,
            grantsReflexActivation = atReflex,
        });

        Debug.Log($"[Strike] {weapon.Data.cardName} is readied — its next activation is buffed " +
                  $"(x{Mathf.Max(1, strike.strikeDamageMultiplier)} " +
                  $"{(strike.strikeBonusDamage >= 0 ? "+" : "-")}{Mathf.Abs(strike.strikeBonusDamage)}" +
                  $"{(atReflex ? ", usable at Reflex speed" : "")}).");
    }

    // A copy of the weapon's ability with the strike card's scaling applied. Only damage scales;
    // anything else the weapon does happens exactly as printed.
    private static CardAbility ScaleForStrike(CardAbility weaponAbility, int multiplier, int bonus)
    {
        if (weaponAbility.effect != EffectKind.DealDamage) return weaponAbility;

        return new CardAbility
        {
            trigger = weaponAbility.trigger,
            effect = weaponAbility.effect,
            amount = Mathf.Max(0, weaponAbility.amount * multiplier + bonus),
            target = weaponAbility.target,
            amountSource = weaponAbility.amountSource,
            bonusCondition = weaponAbility.bonusCondition,
            conditionalBonus = weaponAbility.conditionalBonus,
        };
    }
}
