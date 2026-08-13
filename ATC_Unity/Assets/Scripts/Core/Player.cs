using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    #region Configuration

    public HandManager handManager;
    public DeckManager deckManager;
    public PlayArea playArea;

    [Tooltip("This player's own scry panel, opened by their Scry effects.")]
    public ScryPanel scryPanel;

    [Header("Zones")]
    public CardZone discardZone;
    public CardZone weaponZone;
    public CardZone armourZone;
    public CardZone shieldZone;
    public CardZone equipmentZone;
    public CardZone accessoryZone;
    public CardZone talentZone;
    public CardZone auraZone;
    public CardZone exileZone;

    [Header("Stats")]
    [SerializeField] private int maxHp = 30;
    [SerializeField] private int maxStamina = 3;

    [Tooltip("Defense acts as a shield pool: it absorbs incoming damage point-for-point before HP is touched, and depletes as it soaks hits. Set the cap here (0 = no upper cap; cards/buttons can stack it freely).")]
    [SerializeField] private int maxDefense = 0;

    #endregion

    #region Properties

    public int CurrentHp { get; private set; }
    public int MaxHp => maxHp;
    public int Stamina { get; private set; }
    public int MaxStamina => maxStamina;
    public int Defense { get; private set; }
    public int MaxDefense => maxDefense;

    /// Stacking damage-over-time. At the end of every turn they take damage equal to Burn, then
    /// Burn is halved (rounded down) — so it fades fast but never quite vanishes on its own.
    public int Burn { get; private set; }

    /// Damage-over-time that only bites if the bleeding player actually took direct damage during
    /// the turn. Unlike Burn it does not decay.
    public int Bleed { get; private set; }

    /// A one-shot ward: it prevents up to this much damage from a SINGLE source and is then spent
    /// entirely, however little it actually stopped. Block is used first so the ward is saved for
    /// a hit that matters.
    public int DivineShield { get; private set; }

    // Set by any damage that came from a card, so end-of-turn Bleed knows whether it applies.
    // Status damage (Burn/Bleed itself) has no source card and does not count.
    private bool tookDirectDamageThisTurn;

    public int SpellsPlayedThisTurn { get; private set; }
    public int CardsPlayedThisTurn { get; private set; }

    public Player Opponent => GameManager.Instance != null ? GameManager.Instance.Opponent(this) : null;

    #endregion

    #region Turn-scoped effects (cleared when the turn ends)

    // Tithe: the next card this player plays costs this much more.
    private int nextCardSurcharge;

    // Silence: this player may not play Reflex cards for the rest of the turn.
    private bool reflexLocked;

    // Divine Intervention: everything is free but no cards may be drawn.
    private bool freeCostsNoDraw;

    // Bless: stamina handed over at this player's next upkeep.
    private int queuedUpkeepStamina;

    // Evasive Step: the next card damage aimed at this player is ignored outright.
    private bool avoidNextDirectDamage;

    // Flow State: the controller's next card ignores its speed restriction.
    private bool nextCardAtReflexSpeed;

    public int ReflexCardsPlayedThisTurn { get; private set; }

    public bool ReflexLocked => reflexLocked;
    public bool CanDraw => !freeCostsNoDraw;

    public void AvoidNextDirectDamage() => avoidNextDirectDamage = true;
    public void GrantNextCardReflexSpeed() => nextCardAtReflexSpeed = true;

    public void AddNextCardSurcharge(int amount) => nextCardSurcharge += Mathf.Max(0, amount);
    public void LockReflex() => reflexLocked = true;
    public void MakeCostsFreeButBlockDraws() => freeCostsNoDraw = true;
    public void QueueUpkeepStamina(int amount) => queuedUpkeepStamina += amount;

    /// Wiped by GameManager as each new turn begins, so "for the rest of this turn" really means
    /// this turn — whoever's turn it was played on.
    public void ClearTurnEffects()
    {
        nextCardSurcharge = 0;
        reflexLocked = false;
        freeCostsNoDraw = false;
        avoidNextDirectDamage = false;
        nextCardAtReflexSpeed = false;
        SpellsPlayedThisTurn = 0;
        CardsPlayedThisTurn = 0;
        ReflexCardsPlayedThisTurn = 0;
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        handManager.SetOwner(this);
        ConfigureZoneKinds();
        Stamina = maxStamina;
        CurrentHp = maxHp;
        Defense = maxDefense;

        // Cards in hand can get cheaper while they sit there (a discount Talent entering play), so
        // re-render the hand whenever this player's board changes.
        foreach (var zone in BoardZones())
            if (zone != null) zone.OnChanged += RefreshHandCosts;
    }

    private void OnDestroy()
    {
        foreach (var zone in BoardZones())
            if (zone != null) zone.OnChanged -= RefreshHandCosts;
    }

    // Both hands, not just this one: a Tax or Leyline entering MY board changes what the opponent
    // pays as well, so their card faces have to be re-rendered too.
    private void RefreshHandCosts()
    {
        RefreshHandOf(this);
        RefreshHandOf(Opponent);
    }

    private static void RefreshHandOf(Player player)
    {
        var hand = player != null ? player.handManager : null;
        if (hand == null) return;

        foreach (var cardGO in hand.cardsInHand)
            if (cardGO != null) cardGO.GetComponent<CardDisplay>()?.UpdateCardDisplay();
    }

    private void ConfigureZoneKinds()
    {
        SetZoneKind(discardZone,   CardZone.ZoneKind.Discard);
        SetZoneKind(exileZone,     CardZone.ZoneKind.Exile);
        SetZoneKind(weaponZone,    CardZone.ZoneKind.Weapon);
        SetZoneKind(shieldZone,    CardZone.ZoneKind.Shield);
        SetZoneKind(armourZone,    CardZone.ZoneKind.Armour);
        SetZoneKind(equipmentZone, CardZone.ZoneKind.Equipment);
        SetZoneKind(accessoryZone, CardZone.ZoneKind.Accessory);
        SetZoneKind(talentZone,    CardZone.ZoneKind.Talent);
        SetZoneKind(auraZone,      CardZone.ZoneKind.Aura);
    }

    private static void SetZoneKind(CardZone zone, CardZone.ZoneKind kind)
    {
        if (zone != null) zone.SetKind(kind);
    }

    #endregion

    #region Stats

    public void AdjustHp(int delta) => CurrentHp = Mathf.Max(0, CurrentHp + delta);

    public void AdjustStamina(int delta) => Stamina = Mathf.Max(0, Stamina + delta);

    // "For the turn" stamina (e.g. Second Wind): hands you extra stamina to spend now. It
    // naturally expires at your next upkeep, when ResetStamina refills to maxStamina — so it
    // does NOT raise your max, and may briefly push current above it.
    public void GainStamina(int amount) => AdjustStamina(amount);

    // Cards that raise your stamina pool. Bumps the max and hands you the same amount now so
    // it's usable this turn.
    public void IncreaseMaxStamina(int amount)
    {
        if (amount == 0) return;
        maxStamina = Mathf.Max(0, maxStamina + amount);
        if (amount > 0) Stamina += amount;
    }

    public void ResetStamina() => Stamina = maxStamina;

    // Client-side: overwrite stats from a synced snapshot (the host is authoritative). PlayerStatsUI
    // polls these each frame, so the UI updates automatically. maxHp / maxDefense are constant and
    // identical on both machines, so they don't need syncing.
    public void ClientApplyStats(int hp, int stamina, int maxStaminaValue, int defense,
                                 int burn, int bleed, int divineShield)
    {
        CurrentHp = hp;
        Stamina = stamina;
        maxStamina = maxStaminaValue;
        Defense = defense;
        Burn = burn;
        Bleed = bleed;
        DivineShield = divineShield;
    }

    /// Positive deltas are a Block GAIN and are adjusted by any static modifier in play (Defensive
    /// Stance adds, Broken Stance subtracts). Losing Block — spending it, or the end-of-turn wipe —
    /// is never modified.
    public void AdjustDefense(int delta)
    {
        if (delta > 0) delta = Mathf.Max(0, delta + StaticAmount(EffectKind.ModifyBlockGain));

        int next = Defense + delta;
        Defense = maxDefense > 0 ? Mathf.Clamp(next, 0, maxDefense) : Mathf.Max(0, next);
    }

    // Sum of a Static effect across both boards — the controller's own cards plus anything the
    // opponent has aimed at them (a Condition like Broken Stance sits on the victim's side, so it
    // is found by the Controller pass).
    private int StaticAmount(EffectKind effect)
    {
        int total = StaticsOnBoard(this, effect, EffectTarget.Controller);
        var other = Opponent;
        if (other != null) total += StaticsOnBoard(other, effect, EffectTarget.Opponent);
        return total;
    }

    private static int StaticsOnBoard(Player source, EffectKind effect, EffectTarget aimedAt)
    {
        int total = 0;
        foreach (var zone in source.BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data == null || data.abilities == null) continue;

                foreach (var ability in data.abilities)
                    if (ability != null && ability.trigger == Trigger.Static
                        && ability.effect == effect && ability.target == aimedAt)
                        total += ability.amount;
            }
        }
        return total;
    }

    /// How many cards fit in `zone`, counting any "you may equip one more" effects in play.
    public bool IsZoneFull(CardZone zone)
    {
        if (zone == null || zone.MaxSlots <= 0) return false;
        return zone.Cards.Count >= zone.MaxSlots + ExtraSlotsFor(zone.Kind);
    }

    private int ExtraSlotsFor(CardZone.ZoneKind kind)
    {
        int total = 0;
        foreach (var zoneOwned in BoardZones())
        {
            if (zoneOwned == null) continue;
            foreach (var cardGO in zoneOwned.Cards)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data == null || data.abilities == null) continue;

                foreach (var ability in data.abilities)
                    if (ability != null && ability.trigger == Trigger.Static
                        && ability.effect == EffectKind.ExtraZoneSlots
                        && ZoneKindFor(ability.slotType) == kind)
                        total += ability.amount;
            }
        }
        return total;
    }

    private static CardZone.ZoneKind ZoneKindFor(Card.CardType type)
    {
        switch (type)
        {
            case Card.CardType.Weapon:    return CardZone.ZoneKind.Weapon;
            case Card.CardType.Armour:    return CardZone.ZoneKind.Armour;
            case Card.CardType.Shield:    return CardZone.ZoneKind.Shield;
            case Card.CardType.Accesory:  return CardZone.ZoneKind.Accessory;
            case Card.CardType.Equipment: return CardZone.ZoneKind.Equipment;
            case Card.CardType.Talent:    return CardZone.ZoneKind.Talent;
            case Card.CardType.Aura:      return CardZone.ZoneKind.Aura;
            default:                      return CardZone.ZoneKind.Other;
        }
    }

    public bool SpendStamina(int amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    public void TakeDamage(int amount, GameObject sourceCardGO = null, Card sourceCardData = null)
    {
        if (amount <= 0) return;

        var dmg = new DamageEvent
        {
            defender = this,
            amount = amount,
            sourceCardGO = sourceCardGO,
            sourceCardData = sourceCardData
        };

        // Evasive Step soaks one whole card's worth of damage — status damage has no source card
        // and slips past it.
        if (sourceCardData != null && avoidNextDirectDamage)
        {
            avoidNextDirectDamage = false;
            Debug.Log($"[Damage] {name} avoids {amount} from {sourceCardData.cardName}.");
            return;
        }

        if (sourceCardData != null) tookDirectDamageThisTurn = true;

        int blockBefore = Defense;
        int shieldBefore = DivineShield;
        int hpBefore = CurrentHp;

        FireTriggersOnBoard(Trigger.OnControllerTakeDamage, dmg);
        int afterReduction = dmg.amount;

        // Block first, then the ward. Block is spent point-for-point and expires at end of turn, so
        // using it first avoids burning the whole Divine Shield on a hit Block could have eaten.
        // NOTE: the rules let the DEFENDER choose the order when several reductions apply; there is
        // no prompt for that yet, so this fixed order stands in for it.
        dmg.amount -= SpendBlock(dmg.amount);
        dmg.amount -= SpendDivineShield(dmg.amount);

        if (dmg.amount > 0) AdjustHp(-dmg.amount);

        LogDamage(amount, afterReduction, blockBefore, shieldBefore, hpBefore, sourceCardData);
        GameManager.Instance?.CheckForDefeat(this);
    }

    private int SpendBlock(int incoming)
    {
        int absorbed = Mathf.Max(0, Mathf.Min(Defense, incoming));
        Defense -= absorbed;
        return absorbed;
    }

    // The ward stops up to its value from this one source and is then gone entirely — spending it
    // on a scratch wastes the rest, which is the point of the keyword.
    private int SpendDivineShield(int incoming)
    {
        if (DivineShield <= 0 || incoming <= 0) return 0;

        int absorbed = Mathf.Min(DivineShield, incoming);
        DivineShield = 0;
        return absorbed;
    }

    // Damage silently vanishing into a shield pool is the most confusing thing to watch, so spell
    // the whole chain out: what was thrown, what each shield ate, and what actually reached HP.
    private void LogDamage(int incoming, int afterReduction, int blockBefore, int shieldBefore, int hpBefore, Card source)
    {
        string from = source != null ? source.cardName : "an effect";
        string reduced = afterReduction != incoming ? $", reduced to {afterReduction}" : "";
        int blocked = blockBefore - Defense;
        int shielded = shieldBefore - DivineShield;
        int toHp = hpBefore - CurrentHp;

        string soak = $"{blocked} blocked (Block {blockBefore}→{Defense})";
        if (shielded > 0 || shieldBefore > 0)
            soak += $", {shielded} on Divine Shield ({shieldBefore}→{DivineShield})";

        Debug.Log($"[Damage] {name} hit for {incoming} by {from}{reduced} → {soak}, " +
                  $"{toHp} to HP ({hpBefore}→{CurrentHp}).");
    }

    #endregion

    #region Burn & Divine Shield

    public void AddBurn(int amount)
    {
        if (amount <= 0) return;
        Burn += amount;
        Debug.Log($"[Burn] {name} now has {Burn} Burn.");
    }

    public void AddBleed(int amount)
    {
        if (amount <= 0) return;
        Bleed += amount;
        Debug.Log($"[Bleed] {name} now has {Bleed} Bleed.");
    }

    /// Clears the whole Burn stack and reports how much was removed (Purifying Flame heals it back).
    public int ClearBurn()
    {
        int removed = Burn;
        Burn = 0;
        return removed;
    }

    public void GainDivineShield(int amount)
    {
        if (amount <= 0) return;
        DivineShield += amount;
    }

    /// End of every turn, for both players: Bleed bites only if this player actually took direct
    /// damage during the turn; Burn always bites and is then halved, rounded down. Block expires
    /// last, so it is still available to soak the status damage that just landed.
    public void ResolveEndOfTurn()
    {
        if (Bleed > 0)
        {
            if (tookDirectDamageThisTurn)
            {
                Debug.Log($"[Bleed] {name} takes {Bleed} Bleed damage.");
                TakeDamage(Bleed);
            }
            else Debug.Log($"[Bleed] {name} took no direct damage — Bleed doesn't trigger.");
        }

        if (Burn > 0)
        {
            Debug.Log($"[Burn] {name} takes {Burn} Burn damage.");
            TakeDamage(Burn);
            Burn /= 2;   // halved, rounded down
        }

        Defense = 0;                       // Block never survives the turn it was gained in
        tookDirectDamageThisTurn = false;
        ResetTriggerBudgets();
    }

    /// Triggered abilities fire once per turn each, so every card on this board gets a fresh
    /// allowance when the turn rolls over.
    private void ResetTriggerBudgets()
    {
        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
                if (cardGO != null) cardGO.GetComponent<CardTapState>()?.ResetTriggers();
        }
    }

    /// Everything working against this player that isn't damage: the Burn stack and any Condition
    /// cards hung on them.
    public void RemoveAllDebuffs()
    {
        int burned = ClearBurn();
        int conditions = 0;

        if (auraZone != null)
        {
            foreach (var cardGO in new List<GameObject>(auraZone.Cards))
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data == null || data.cardType != Card.CardType.Condition) continue;
                SendToDiscard(cardGO);
                conditions++;
            }
        }

        Debug.Log($"[Effect] {name} cleansed {burned} Burn and {conditions} condition(s).");
    }

    #endregion

    #region Turn

    public void DrawCard()
    {
        if (!CanDraw)
        {
            Debug.Log($"[Draw] {name} cannot draw this turn.");
            return;
        }

        deckManager.DrawCard(handManager);
        FireTriggersOnBoard(Trigger.OnControllerDraws, null);
    }

    /// Draw until the deck runs dry (Omniscience). The hand limit still applies, so this stops
    /// early if the hand fills up rather than silently binning cards.
    public void DrawEntireDeck()
    {
        int drawn = 0;
        while (deckManager != null && deckManager.HasCards && !handManager.IsHandFull)
        {
            DrawCard();
            drawn++;
            if (!CanDraw) break;
        }
        Debug.Log($"[Draw] {name} drew {drawn} card(s) emptying their deck.");
    }

    public void ResolveUpkeep()
    {
        ResetStamina();

        if (queuedUpkeepStamina > 0)
        {
            Debug.Log($"[Effect] {name} gains {queuedUpkeepStamina} queued stamina at upkeep.");
            GainStamina(queuedUpkeepStamina);
            queuedUpkeepStamina = 0;
        }

        UntapBoard();
        FireTriggersOnBoard(Trigger.OnUpkeep, null);
    }

    private void UntapBoard()
    {
        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
                if (cardGO != null)
                    cardGO.GetComponent<CardTapState>()?.Untap();
        }
    }

    #endregion

    #region Playing Cards

    /// What `card` costs this player to play RIGHT NOW: its printed cost minus every static
    /// discount their board grants, never below 0. Always use this instead of `Card.energyCost` —
    /// the printed number is only the starting point.
    public int StaminaCostOf(Card card)
    {
        if (card == null) return 0;
        if (freeCostsNoDraw) return 0;   // Divine Intervention

        // Reductions apply FIRST and bottom out at 0; increases are then added on top. So a card
        // reduced to 0 and taxed by 1 costs 1, not 0.
        int cost = Mathf.Max(0, card.energyCost - CostModifier(card, EffectKind.ReduceCost));
        cost += CostModifier(card, EffectKind.IncreaseCost) + nextCardSurcharge;
        return Mathf.Max(0, cost);
    }

    // Static cost modifiers reach this player's cards from two directions: their own board
    // (abilities aimed at the Controller) and the opponent's (abilities aimed at the Opponent).
    private int CostModifier(Card card, EffectKind effect)
    {
        int total = ModifiersOnBoard(this, card, effect, EffectTarget.Controller);
        var other = Opponent;
        if (other != null) total += ModifiersOnBoard(other, card, effect, EffectTarget.Opponent);
        return total;
    }

    private static int ModifiersOnBoard(Player source, Card card, EffectKind effect, EffectTarget aimedAt)
    {
        int total = 0;
        foreach (var zone in source.BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data == null || data.abilities == null) continue;

                foreach (var ability in data.abilities)
                {
                    if (ability == null || ability.trigger != Trigger.Static) continue;
                    if (ability.effect != effect || ability.target != aimedAt) continue;
                    if (!card.MatchesCostScope(ability.costScope)) continue;
                    total += ability.amount;
                }
            }
        }
        return total;
    }

    public bool TryPlayCard(GameObject cardGO, Card cardData)
    {
        if (!CanPlay(cardData, out string reason))
        {
            Debug.Log($"[Play] {name} cannot play {cardData.cardName}: {reason}");
            return false;
        }

        var zone = ZoneFor(cardData.cardType);
        bool zoneWasFull = IsZoneFull(zone);
        int cost = StaminaCostOf(cardData);
        int staminaBefore = Stamina;

        SpendStamina(cost);
        handManager.RemoveCardFromHand(cardGO);
        zone.AddCard(cardGO);
        FreezeCardInteractions(cardGO);
        SyncBoardActionsForZone(cardGO, zone);

        string adjusted = cost != cardData.energyCost ? $" (printed {cardData.energyCost}, adjusted)" : "";
        Debug.Log($"[Play] {name} played {cardData.cardName} for {cost} stamina{adjusted} " +
                  $"({staminaBefore}→{Stamina}) → {zone.name}");

        nextCardSurcharge = 0;   // Tithe taxes the NEXT card only

        // The slot was already full, so something has to go — the player picks which.
        if (zoneWasFull && EffectRunner.Instance != null)
            EffectRunner.Instance.RequestZoneReplacement(this, zone);

        RegisterCardPlayed(cardData);
        PushToStack(cardGO, cardData, Trigger.OnPlay);
        return true;
    }

    /// Drop a card straight into the discard pile, building the object for it — used by Scry, where
    /// the binned cards were never on the table to begin with.
    public void PutCardInDiscard(Card data)
    {
        if (data == null || discardZone == null || handManager == null) return;

        var prefab = handManager.cardPrefab;
        if (prefab == null) return;

        var parent = handManager.handPosition;
        var cardGO = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);

        var display = cardGO.GetComponent<CardDisplay>();
        if (display != null) { display.cardData = data; display.SetFaceUp(true); }
        cardGO.GetComponent<CardMovement>()?.Init(handManager);   // gives it an owner

        discardZone.AddCard(cardGO);
        FreezeCardInteractions(cardGO);
        SyncBoardActionsForZone(cardGO, discardZone);
    }

    // Bookkeeping every "whenever you play…" effect hangs off.
    private void RegisterCardPlayed(Card cardData)
    {
        CardsPlayedThisTurn++;
        nextCardAtReflexSpeed = false;   // Flow State covers one card only
        if (cardData.speedType == Card.SpeedType.Reflex) ReflexCardsPlayedThisTurn++;

        if (cardData.IsSpell)
        {
            SpellsPlayedThisTurn++;
            FireTriggersOnBoard(Trigger.OnControllerPlaysSpell, null);
        }

        // Vow of Penance watches for a player's second card of the turn — from ANY board, since the
        // aura punishes whoever overextends, not just its controller.
        if (CardsPlayedThisTurn == 2) FireSecondCardPenalties();
    }

    private void FireSecondCardPenalties()
    {
        if (EffectRunner.Instance == null) return;

        foreach (var player in new[] { this, Opponent })
        {
            if (player == null) continue;
            foreach (var zone in player.BoardZones())
            {
                if (zone == null) continue;
                foreach (var cardGO in new List<GameObject>(zone.Cards))
                {
                    var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                    if (data == null || !data.HasAbilityFor(Trigger.OnAnyPlayerPlaysSecondCard)) continue;

                    // Context controller = the player who overextended, so the penalty lands on them.
                    EffectRunner.Instance.FireAbilitiesImmediate(
                        data, MakeContext(cardGO, data), Trigger.OnAnyPlayerPlaysSecondCard);
                }
            }
        }
    }

    public bool TryActivateCard(GameObject cardGO, Card cardData)
    {
        if (cardGO == null || cardData == null) return false;

        var ability = cardData.FirstActivated();
        if (ability == null) return false;

        if (GameStack.Instance != null && GameStack.Instance.IsResolving) return false;

        if (GameManager.Instance != null && !GameManager.Instance.IsControllingPlayer(this))
        {
            Debug.Log($"[Activate] {name} doesn't have priority.");
            return false;
        }

        var zone = cardGO.GetComponentInParent<CardZone>();
        if (zone == null || !IsBoardZone(zone))
        {
            Debug.Log($"[Activate] {cardData.cardName} must be in play to activate.");
            return false;
        }

        var tap = cardGO.GetComponent<CardTapState>();
        if (ability.tapToActivate && tap != null && tap.IsTapped)
        {
            Debug.Log($"[Activate] {cardData.cardName} is tapped — already used this turn.");
            return false;
        }

        if (!ActivationTimingAllowed(cardData, ability, out string reason))
        {
            Debug.Log($"[Activate] {name} cannot activate {cardData.cardName}: {reason}");
            return false;
        }

        if (Stamina < ability.activationCost)
        {
            Debug.Log($"[Activate] {name} lacks stamina to activate {cardData.cardName} ({Stamina}/{ability.activationCost}).");
            return false;
        }

        SpendStamina(ability.activationCost);
        if (ability.tapToActivate && tap != null) tap.Tap();

        Debug.Log($"[Activate] {name} activated {cardData.cardName}.");
        PushToStack(cardGO, cardData, Trigger.Activated);
        return true;
    }

    private void PushToStack(GameObject cardGO, Card cardData, Trigger trigger)
    {
        // Nothing to resolve = nothing to respond to. A permanent whose only ability fires later
        // (Iron Skin's upkeep block, a weapon you activate in Combat) would otherwise put an EMPTY
        // item on the stack, handing priority to the opponent and parking a Pass button on their
        // screen for a card that does nothing yet.
        if (cardData == null || !cardData.HasAbilityFor(trigger)) return;

        if (GameStack.Instance != null)
            GameStack.Instance.Push(new StackItem { controller = this, sourceCardGO = cardGO, sourceCardData = cardData, trigger = trigger });
        else if (EffectRunner.Instance != null)
            EffectRunner.Instance.FireAbilities(cardData, MakeContext(cardGO, cardData), trigger);
    }

    public bool HasReflexResponse()
    {
        if (reflexLocked) return false;

        if (handManager != null)
        {
            foreach (var cardGO in handManager.cardsInHand)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data != null && data.speedType == Card.SpeedType.Reflex && Stamina >= StaminaCostOf(data))
                    return true;
            }
        }

        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in zone.Cards)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                var ability = data != null ? data.FirstActivated() : null;
                if (ability == null || ability.activationSpeed != Card.SpeedType.Reflex) continue;
                if (Stamina < ability.activationCost) continue;
                var tap = cardGO.GetComponent<CardTapState>();
                bool alreadyTapped = ability.tapToActivate && tap != null && tap.IsTapped;
                if (!alreadyTapped) return true;
            }
        }

        return false;
    }

    private static bool IsBoardZone(CardZone zone)
        => zone.Kind != CardZone.ZoneKind.Discard && zone.Kind != CardZone.ZoneKind.Exile;

    private bool CanPlay(Card card, out string reason)
    {
        if (GameManager.Instance != null && GameManager.Instance.MatchOver)
        {
            reason = "The match is over";
            return false;
        }

        if (GameStack.Instance != null && GameStack.Instance.IsResolving)
        {
            reason = "An effect is resolving";
            return false;
        }

        if (GameManager.Instance != null && !GameManager.Instance.IsControllingPlayer(this))
        {
            reason = "You don't have priority";
            return false;
        }

        if (!SpeedAllowedThisPhase(card.speedType, out reason)) return false;

        int cost = StaminaCostOf(card);
        if (Stamina < cost)
        {
            reason = $"Not enough stamina ({Stamina}/{cost})";
            return false;
        }

        var zone = ZoneFor(card.cardType);
        if (zone == null) { reason = $"No zone configured for {card.cardType}"; return false; }

        // A full slot is not a refusal: you play the card and then choose which one it replaces.
        reason = null;
        return true;
    }

    private bool ActivationTimingAllowed(Card card, CardAbility ability, out string reason)
    {
        reason = null;
        var gm = GameManager.Instance;

        // Weapons/attacks are combat actions: only on your own turn, only during Combat.
        if (card.IsCombatCard)
        {
            if (gm != null && !gm.IsActivePlayer(this))
            {
                reason = "Weapons can only be used on your turn";
                return false;
            }
            if (gm != null && gm.CurrentPhase != GameManager.GamePhase.Combat)
            {
                reason = $"Weapons can only be activated in Combat (current: {gm.CurrentPhase})";
                return false;
            }
            return true;
        }

        return SpeedAllowedThisPhase(ability.activationSpeed, out reason);
    }

    private bool SpeedAllowedThisPhase(Card.SpeedType speed, out string reason)
    {
        reason = null;

        if (speed == Card.SpeedType.Reflex && reflexLocked)
        {
            reason = "Silenced — no Reflex cards this turn";
            return false;
        }

        if (speed != Card.SpeedType.Channel) return true;

        // Keen Instinct (always on) and Flow State (one card) both let Channel act like Reflex.
        if (nextCardAtReflexSpeed || StaticAmount(EffectKind.AllowChannelAtReflexSpeed) > 0) return true;

        if (!GameManager.Instance.IsActivePlayer(this))
        {
            reason = "Channel cards require your turn";
            return false;
        }

        var phase = GameManager.Instance.CurrentPhase;
        if (phase != GameManager.GamePhase.Main1 && phase != GameManager.GamePhase.Main2)
        {
            reason = $"Channel cards require Main1/Main2 (current: {phase})";
            return false;
        }
        return true;
    }

    private CardZone ZoneFor(Card.CardType type)
    {
        switch (type)
        {
            case Card.CardType.Weapon:    return weaponZone;
            case Card.CardType.Armour:    return armourZone;
            case Card.CardType.Shield:    return shieldZone;
            case Card.CardType.Equipment: return equipmentZone;
            case Card.CardType.Accesory:  return accessoryZone;
            case Card.CardType.Talent:    return talentZone;
            case Card.CardType.Aura:      return auraZone;
            case Card.CardType.Condition: return Opponent != null ? Opponent.auraZone : auraZone;
            // Attack / Spell / Skill / Consumable / Miracle resolve and are done with.
            default:                      return discardZone;
        }
    }

    #endregion

    #region Zone Movement

    public void SendToDiscard(GameObject cardGO)
    {
        FireTriggersForDyingCard(cardGO, Trigger.OnDestroyed);
        MoveCardToZone(cardGO, discardZone);
    }

    public void SendToExile(GameObject cardGO) => MoveCardToZone(cardGO, exileZone);

    public void DestroyAllEquipment()
    {
        foreach (var zone in EquipmentZones())
        {
            if (zone == null) continue;
            foreach (var cardGO in new List<GameObject>(zone.Cards))
                SendToDiscard(cardGO);
        }
    }

    public void DiscardFromHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var cards = handManager.cardsInHand;
            if (cards.Count == 0) return;
            SendToDiscard(cards[cards.Count - 1]);
        }
    }

    public void ReturnToHand(GameObject cardGO)
    {
        if (cardGO == null) return;
        if (handManager.cardsInHand.Contains(cardGO)) return;

        var data = cardGO.GetComponent<CardDisplay>()?.cardData;
        if (data == null) return;

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();

        RemoveFromAnyZone(cardGO);
        Destroy(cardGO);
        handManager.AddCardToHand(data);
    }

    public void MoveCardToZone(GameObject cardGO, CardZone destination)
    {
        if (destination == null || cardGO == null) return;

        if (handManager.cardsInHand.Contains(cardGO))
            handManager.RemoveCardFromHand(cardGO);
        else
            RemoveFromAnyZone(cardGO);

        destination.AddCard(cardGO);
        FreezeCardInteractions(cardGO);
        SyncBoardActionsForZone(cardGO, destination);
    }

    private void RemoveFromAnyZone(GameObject cardGO)
    {
        foreach (var zone in AllZones())
        {
            if (zone != null && zone.Cards.Contains(cardGO))
            {
                zone.RemoveCard(cardGO);
                return;
            }
        }
    }

    private IEnumerable<CardZone> AllZones()
    {
        yield return weaponZone;
        yield return armourZone;
        yield return shieldZone;
        yield return equipmentZone;
        yield return accessoryZone;
        yield return talentZone;
        yield return auraZone;
        yield return discardZone;
        yield return exileZone;
    }

    private IEnumerable<CardZone> BoardZones()
    {
        yield return weaponZone;
        yield return armourZone;
        yield return shieldZone;
        yield return equipmentZone;
        yield return accessoryZone;
        yield return talentZone;
        yield return auraZone;
    }

    // Public, stable-order enumeration of every zone whose contents are network-synced (board
    // permanents + discard + exile). The server snapshot and the client rebuild iterate this in the
    // same order, so per-zone counts line up.
    public IEnumerable<CardZone> SyncedZones() => AllZones();

    /// True when this player owns `zone`. Used server-side to reject a client asking to act on a
    /// card that isn't theirs.
    public bool OwnsZone(CardZone zone)
    {
        if (zone == null) return false;
        foreach (var mine in AllZones())
            if (mine == zone) return true;
        return false;
    }

    private IEnumerable<CardZone> EquipmentZones()
    {
        yield return weaponZone;
        yield return accessoryZone;
        yield return armourZone;
    }

    #endregion

    #region Effect Plumbing

    public EffectContext BuildContext(GameObject cardGO, Card cardData) => MakeContext(cardGO, cardData);

    private EffectContext MakeContext(GameObject cardGO, Card cardData) => new EffectContext
    {
        sourceCardGO = cardGO,
        sourceCardData = cardData,
        controller = this,
        opponent = Opponent
    };

    private void FireTriggersOnBoard(Trigger trigger, DamageEvent dmg)
    {
        if (EffectRunner.Instance == null) return;
        foreach (var zone in BoardZones())
        {
            if (zone == null) continue;
            var snapshot = new List<GameObject>(zone.Cards);
            foreach (var cardGO in snapshot)
            {
                var data = cardGO != null ? cardGO.GetComponent<CardDisplay>()?.cardData : null;
                if (data == null) continue;
                if (!data.HasAbilityFor(trigger)) continue;

                // Once per turn per card, per the Trigger keyword. Upkeep is naturally once a turn,
                // but "whenever you play a Spell" would otherwise fire on every spell.
                var tapState = cardGO.GetComponent<CardTapState>();
                if (tapState != null && !tapState.TryUseTrigger(trigger)) continue;

                var ctx = MakeContext(cardGO, data);
                ctx.damage = dmg;

                // Damage triggers must finish BEFORE the hit lands, so they run synchronously.
                // Everything else goes through the coroutine path — an upkeep Scry or a
                // spell-triggered Scry has to be able to pause and wait for the player.
                if (trigger == Trigger.OnControllerTakeDamage)
                    EffectRunner.Instance.FireAbilitiesImmediate(data, ctx, trigger);
                else
                    EffectRunner.Instance.FireAbilities(data, ctx, trigger);
            }
        }
    }

    private void FireTriggersForDyingCard(GameObject cardGO, Trigger trigger)
    {
        if (cardGO == null || EffectRunner.Instance == null) return;
        var data = cardGO.GetComponent<CardDisplay>()?.cardData;
        if (data == null) return;

        var zone = cardGO.GetComponentInParent<CardZone>();
        if (zone == null) return;
        if (zone.Kind == CardZone.ZoneKind.Discard || zone.Kind == CardZone.ZoneKind.Exile) return;

        EffectRunner.Instance.FireAbilitiesImmediate(data, MakeContext(cardGO, data), trigger);
    }

    #endregion

    #region Interaction Sync

    private static void FreezeCardInteractions(GameObject cardGO)
    {
        var movement = cardGO.GetComponent<CardMovement>();
        if (movement != null) movement.enabled = false;

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    private void SyncBoardActionsForZone(GameObject cardGO, CardZone destination)
    {
        var actions = cardGO.GetComponent<CardBoardActions>();
        if (actions == null) return;
        actions.enabled = destination != discardZone && destination != exileZone;
    }

    #endregion
}
