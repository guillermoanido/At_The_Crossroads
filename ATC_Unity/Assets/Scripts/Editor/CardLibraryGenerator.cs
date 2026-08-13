#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CardLibraryGenerator
{
    private const string Folder = "Assets/Card Data";
    private const string CardPrefabPath = "Assets/Prefabs/CardPrefab.prefab";

    [MenuItem("ATC/Generate Card Library")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Card Data");

        int created = 0, updated = 0;
        foreach (var d in Warriors()) Upsert(d, ref created, ref updated);
        foreach (var d in Rogues()) Upsert(d, ref created, ref updated);
        foreach (var d in Mages()) Upsert(d, ref created, ref updated);
        foreach (var d in Clerics()) Upsert(d, ref created, ref updated);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CardLibrary] Done — {created} created, {updated} updated in {Folder}.");
    }

    [MenuItem("ATC/Ensure Card Prefab Components")]
    public static void EnsureCardPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
        bool changed = false;
        if (root.GetComponent<Targetable>() == null) { root.AddComponent<Targetable>(); changed = true; }
        if (changed) PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log(changed ? "[CardPrefab] Added Targetable." : "[CardPrefab] Already has Targetable.");
    }

    private static void Upsert(Def d, ref int created, ref int updated)
    {
        string path = $"{Folder}/{d.name}.asset";
        var card = AssetDatabase.LoadAssetAtPath<Card>(path);
        bool isNew = card == null;
        if (isNew) card = ScriptableObject.CreateInstance<Card>();

        card.cardName = d.name;
        card.cardType = d.type;
        card.speedType = d.speed;
        card.energyCost = d.stamina;
        card.strRequired = d.str;
        card.dexRequired = d.dex;
        card.intRequired = d.intel;
        card.wisRequired = d.wis;
        card.deckCost = 1;
        card.effectDescription = d.text;
        card.abilities = d.abilities ?? new List<CardAbility>();

        if (isNew) { AssetDatabase.CreateAsset(card, path); created++; }
        else { EditorUtility.SetDirty(card); updated++; }
    }


    private static List<CardAbility> A(params CardAbility[] xs) => new List<CardAbility>(xs);

    private static CardAbility OnPlay(EffectKind e, int amt, EffectTarget t = EffectTarget.Controller)
        => new CardAbility { trigger = Trigger.OnPlay, effect = e, amount = amt, target = t, tapToActivate = false };

    private static CardAbility Upkeep(EffectKind e, int amt)
        => new CardAbility { trigger = Trigger.OnUpkeep, effect = e, amount = amt, target = EffectTarget.Controller, tapToActivate = false };

    private static CardAbility Activated(EffectKind e, int amt, Card.SpeedType speed, EffectTarget t = EffectTarget.Controller, int cost = 0)
        => new CardAbility { trigger = Trigger.Activated, effect = e, amount = amt, target = t, activationSpeed = speed, activationCost = cost, tapToActivate = true };

    private static CardAbility Strike(int multiplier = 1, int bonus = 0, bool destroysWeapon = false)
        => new CardAbility
        {
            trigger = Trigger.OnPlay, effect = EffectKind.Strike, target = EffectTarget.Opponent, tapToActivate = false,
            strikeDamageMultiplier = multiplier, strikeBonusDamage = bonus, strikeDestroysWeapon = destroysWeapon,
        };

    private static Def W(string name, Card.CardType type, Card.SpeedType speed, int stamina, int str, string text, List<CardAbility> abilities = null)
        => new Def { name = name, type = type, speed = speed, stamina = stamina, str = str, dex = 0, text = text, abilities = abilities };

    private static Def R(string name, Card.CardType type, Card.SpeedType speed, int stamina, int dex, string text, List<CardAbility> abilities = null)
        => new Def { name = name, type = type, speed = speed, stamina = stamina, dex = dex, text = text, abilities = abilities };

    // Mage (Intelligence) and Cleric (Wisdom).
    private static Def M(string name, Card.CardType type, Card.SpeedType speed, int stamina, int intel, string text, List<CardAbility> abilities = null)
        => new Def { name = name, type = type, speed = speed, stamina = stamina, intel = intel, text = text, abilities = abilities };

    private static Def C(string name, Card.CardType type, Card.SpeedType speed, int stamina, int wis, string text, List<CardAbility> abilities = null)
        => new Def { name = name, type = type, speed = speed, stamina = stamina, wis = wis, text = text, abilities = abilities };

    // An ability whose magnitude counts something ("X + 1"): Amount is the +1, the source is the X.
    private static CardAbility Scaled(CardAbility a, AmountSource source)
    {
        a.amountSource = source;
        return a;
    }

    private static CardAbility Cost(EffectKind e, int amt, CostScope scope, EffectTarget t = EffectTarget.Controller)
        => new CardAbility { trigger = Trigger.Static, effect = e, amount = amt, target = t, costScope = scope, tapToActivate = false };

    private static CardAbility When(Trigger trigger, EffectKind e, int amt, EffectTarget t = EffectTarget.Controller)
        => new CardAbility { trigger = trigger, effect = e, amount = amt, target = t, tapToActivate = false };


    // Always-on modifier that is not about cost (block gain, speed permission).
    private static CardAbility StaticMod(EffectKind e, int amt, EffectTarget t = EffectTarget.Controller)
        => new CardAbility { trigger = Trigger.Static, effect = e, amount = amt, target = t, tapToActivate = false };

    // Always-on "you may equip one more X".
    private static CardAbility ExtraSlots(Card.CardType slot, int amt)
        => new CardAbility { trigger = Trigger.Static, effect = EffectKind.ExtraZoneSlots, amount = amt,
                             target = EffectTarget.Controller, slotType = slot, tapToActivate = false };

    // An ability with a conditional bonus on top ("+2 if you played a Reflex card this turn").
    private static CardAbility WithBonus(CardAbility a, AmountCondition condition, int bonus)
    {
        a.bonusCondition = condition;
        a.conditionalBonus = bonus;
        return a;
    }

    private class Def
    {
        public string name;
        public Card.CardType type;
        public Card.SpeedType speed;
        public int stamina, str, dex, intel, wis;
        public string text;
        public List<CardAbility> abilities;
    }


    private static List<Def> Warriors() => new List<Def>
    {
        W("Brace",            Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 5,  "Gain 4 Block.",                                        A(OnPlay(EffectKind.GainBlock, 4))),
        W("Light Swing",      Card.CardType.Attack,    Card.SpeedType.Channel, 1, 5,  "Strike",                                               A(Strike())),
        W("Kite Shield",      Card.CardType.Shield,    Card.SpeedType.Channel, 2, 6,  "Activate (Reflex) — Gain 2 Block",                     A(Activated(EffectKind.GainBlock, 2, Card.SpeedType.Reflex))),
        W("Heavy Swing",      Card.CardType.Attack,    Card.SpeedType.Channel, 2, 7,  "Strike: +2 Damage",                                    A(Strike(bonus: 2))),
        W("Second Wind",      Card.CardType.Skill,     Card.SpeedType.Channel, 1, 8,  "Gain 2 Stamina.",                                      A(OnPlay(EffectKind.GainStamina, 2))),
        W("Iron Skin",        Card.CardType.Talent,    Card.SpeedType.Channel, 2, 8,  "Start of turn: Gain 1 Block",                          A(Upkeep(EffectKind.GainBlock, 1))),
        W("Hurl",             Card.CardType.Attack,    Card.SpeedType.Channel, 1, 8,  "Strike: Deal double damage. Destroy this weapon.",     A(Strike(multiplier: 2, destroysWeapon: true))),
        W("Sunder",           Card.CardType.Skill,     Card.SpeedType.Channel, 2, 9,  "Destroy target equipment.",                            A(OnPlay(EffectKind.DestroyTargetEquipment, 0))),
        W("Unrelenting Rage", Card.CardType.Talent,    Card.SpeedType.Channel, 2, 9,  "Your Strike cards cost 1 less Stamina (minimum 0).", A(Cost(EffectKind.ReduceCost, 1, CostScope.StrikeCards))),
        W("Layered Armour",   Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "Start of turn: Lose 1 Stamina. You can equip 1 more Armour.", A(Upkeep(EffectKind.LoseStamina, 1), ExtraSlots(Card.CardType.Armour, 1))),
        W("Tower Shield",     Card.CardType.Shield,    Card.SpeedType.Channel, 3, 10, "Activate (Reflex) — Gain 3 Block",                     A(Activated(EffectKind.GainBlock, 3, Card.SpeedType.Reflex))),
        W("Greatclub",        Card.CardType.Weapon,    Card.SpeedType.Channel, 3, 10, "Activate (Channel) — Deal 4 damage.",                  A(Activated(EffectKind.DealDamage, 4, Card.SpeedType.Channel, EffectTarget.Opponent))),
        W("Iron Plate",       Card.CardType.Armour,    Card.SpeedType.Channel, 3, 10, "Start of turn: Gain 3 Block",                          A(Upkeep(EffectKind.GainBlock, 3))),
        W("Broken Stance",    Card.CardType.Condition, Card.SpeedType.Channel, 1, 11, "Whenever you gain Block, reduce it by 2.",             A(StaticMod(EffectKind.ModifyBlockGain, -2))),
        W("Monolith",         Card.CardType.Weapon,    Card.SpeedType.Channel, 3, 12, "Activate (Channel) Pay 1 Stamina — Deal 7 damage.",    A(Activated(EffectKind.DealDamage, 7, Card.SpeedType.Channel, EffectTarget.Opponent, 1))),
        W("Skull Splitter",   Card.CardType.Attack,    Card.SpeedType.Channel, 3, 13, "Strike: Opponent discard 3 cards.",                    A(Strike(), OnPlay(EffectKind.OpponentDiscards, 3, EffectTarget.Opponent))),
        W("Fracture",         Card.CardType.Condition, Card.SpeedType.Channel, 3, 13, "Start of turn: Lose 1 Stamina.",                       A(Upkeep(EffectKind.LoseStamina, 1))),
        W("Earthquake",       Card.CardType.Attack,    Card.SpeedType.Channel, 4, 17, "Destroy all opponent's equipment.",                    A(OnPlay(EffectKind.DestroyAllOpponentEquipment, 0, EffectTarget.Opponent))),
    };

    private static List<Def> Rogues() => new List<Def>
    {
        R("Flow State",       Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 5,  "Your next card is cast at reflex speed. Scry 1.",      A(OnPlay(EffectKind.NextCardAtReflexSpeed, 0), OnPlay(EffectKind.Scry, 1))),
        R("Dagger",           Card.CardType.Weapon,    Card.SpeedType.Channel, 1, 5,  "Activate (Channel) — Deal 1 damage.",                  A(Activated(EffectKind.DealDamage, 1, Card.SpeedType.Channel, EffectTarget.Opponent))),
        R("Evasive Step",     Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 5,  "Avoid the next source of direct damage this turn.",    A(OnPlay(EffectKind.AvoidNextDirectDamage, 0))),
        R("Quick Jab",        Card.CardType.Attack,    Card.SpeedType.Reflex,  0, 5,  "Deal 3 damage.",                                       A(OnPlay(EffectKind.DealDamage, 3, EffectTarget.Opponent))),
        R("Open Veins",       Card.CardType.Skill,     Card.SpeedType.Channel, 1, 6,  "Strike: -1 Damage. Apply Bleed 1.",                    A(Strike(bonus: -1), OnPlay(EffectKind.ApplyBleed, 1, EffectTarget.Opponent))),
        R("Pickpocket",       Card.CardType.Skill,     Card.SpeedType.Channel, 2, 7,  "Look at your opponent's hand, take one card.",         A(OnPlay(EffectKind.TakeCardFromOpponentHand, 0))),
        R("Hidden Dagger",    Card.CardType.Weapon,    Card.SpeedType.Channel, 2, 8,  "Activate (Reflex) — Deal 1 damage. +2 if you played a Reflex card this turn.", A(WithBonus(Activated(EffectKind.DealDamage, 1, Card.SpeedType.Reflex, EffectTarget.Opponent), AmountCondition.IfYouPlayedAReflexCardThisTurn, 2))),
        R("Backstab",         Card.CardType.Skill,     Card.SpeedType.Reflex,  1, 8,  "Strike: Reflex",                                       A(Strike())),
        R("Slingshot",        Card.CardType.Weapon,    Card.SpeedType.Channel, 1, 8,  "Activate (Channel) Discard 1 Equipment or remove one from discard — Deal 3 damage.", A(Activated(EffectKind.DealDamage, 3, Card.SpeedType.Channel, EffectTarget.Opponent))),
        R("Disarm",           Card.CardType.Skill,     Card.SpeedType.Reflex,  1, 9,  "Return target equipment to opponent's hand.",          A(OnPlay(EffectKind.ReturnTargetEquipmentToHand, 0))),
        R("Bait and Switch",  Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 9,  "Return target Equipment you own to hand. You may play 1 Equipment from your hand without paying its cost.", A(OnPlay(EffectKind.ReturnOwnEquipmentToHand, 0), OnPlay(EffectKind.NextEquipmentIsFree, 0))),
        R("Keen Instinct",    Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "All your cards can be played at Reflex speed.",        A(StaticMod(EffectKind.AllowChannelAtReflexSpeed, 1))),
        R("Dual Wielding",    Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "Start of turn: Lose 1 Stamina. You can equip 1 more weapon.", A(Upkeep(EffectKind.LoseStamina, 1), ExtraSlots(Card.CardType.Weapon, 1))),
        R("Double Strike",    Card.CardType.Attack,    Card.SpeedType.Channel, 2, 10, "Strike. Strike.",                                      A(Strike(), Strike())),
        R("Sleight of Hand",  Card.CardType.Skill,     Card.SpeedType.Channel, 2, 10, "Copy an equipment from opponent, destroy the original.", A(OnPlay(EffectKind.StealCopyOfOpponentEquipment, 0))),
        R("Muscle Memory",    Card.CardType.Talent,    Card.SpeedType.Channel, 2, 11, "Your skills cost 1 less.",                             A(Cost(EffectKind.ReduceCost, 1, CostScope.Skills))),
        R("Set-Up",           Card.CardType.Skill,     Card.SpeedType.Channel, 0, 12, "Place a card face down, next turn play it with cost 0.", A(OnPlay(EffectKind.SetAsideCardForNextTurn, 0))),
        R("Dagger Dance",     Card.CardType.Skill,     Card.SpeedType.Channel, 1, 12, "For the rest of the turn, your skills have: Strike."),
        R("Light Speed",      Card.CardType.Skill,     Card.SpeedType.Channel, 2, 17, "Take an extra turn after this one.",                    A(OnPlay(EffectKind.TakeExtraTurn, 0))),
        R("Defensive Stance", Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "You gain +1 Block from all sources.",                  A(StaticMod(EffectKind.ModifyBlockGain, 1))),
    };


    // Mage — Intelligence. Spells, Burn and stack interaction.
    private static List<Def> Mages() => new List<Def>
    {
        M("Dart",                Card.CardType.Spell,     Card.SpeedType.Reflex,  0, 4,  "Deal 2 damage.",
            A(OnPlay(EffectKind.DealDamage, 2, EffectTarget.Opponent))),

        M("Arcane Bolt",         Card.CardType.Spell,     Card.SpeedType.Reflex,  1, 5,  "Scry 1. Deal 3 damage.",
            A(OnPlay(EffectKind.Scry, 1), OnPlay(EffectKind.DealDamage, 3, EffectTarget.Opponent))),

        M("Arcane Barrier",      Card.CardType.Spell,     Card.SpeedType.Reflex,  1, 6,  "Gain X + 1 Block, where X is the number of Spells in your discard pile.",
            A(Scaled(OnPlay(EffectKind.GainBlock, 1), AmountSource.SpellsInYourDiscard))),

        M("Study",               Card.CardType.Skill,     Card.SpeedType.Channel, 2, 6,  "Draw 3 cards.",
            A(OnPlay(EffectKind.DrawCards, 3))),

        M("Fire Bolt",           Card.CardType.Spell,     Card.SpeedType.Channel, 1, 7,  "Deal 2 damage. Apply 2 Burn.",
            A(OnPlay(EffectKind.DealDamage, 2, EffectTarget.Opponent), OnPlay(EffectKind.ApplyBurn, 2, EffectTarget.Opponent))),

        M("Recall",              Card.CardType.Skill,     Card.SpeedType.Channel, 1, 7,  "Return target Skill or Spell from your discard pile to your hand.",
            A(OnPlay(EffectKind.ReturnTargetFromDiscardToHand, 0))),

        M("Divination",          Card.CardType.Spell,     Card.SpeedType.Channel, 1, 8,  "Draw 2 cards.",
            A(OnPlay(EffectKind.DrawCards, 2))),

        M("Cloak of Protection", Card.CardType.Armour,    Card.SpeedType.Channel, 2, 8,  "Start of your turn: gain 1 Block. Whenever you play a Spell, gain 1 Block.",
            A(Upkeep(EffectKind.GainBlock, 1), When(Trigger.OnControllerPlaysSpell, EffectKind.GainBlock, 1))),

        M("Arcane Staff",        Card.CardType.Accesory,  Card.SpeedType.Channel, 2, 9,  "Activate (Reflex) — deal X + 1 damage, where X is the number of Spells you played this turn.",
            A(Scaled(Activated(EffectKind.DealDamage, 1, Card.SpeedType.Reflex, EffectTarget.Opponent), AmountSource.SpellsYouPlayedThisTurn))),

        M("Rewind",              Card.CardType.Spell,     Card.SpeedType.Reflex,  1, 9,  "Return target card on the stack to its owner's hand.",
            A(OnPlay(EffectKind.ReturnTargetOnStackToHand, 0))),

        M("Scrying Orb",         Card.CardType.Accesory,  Card.SpeedType.Channel, 2, 10, "Start of your turn: Scry 2.",
            A(Upkeep(EffectKind.Scry, 2))),

        M("Aether Focus",        Card.CardType.Accesory,  Card.SpeedType.Channel, 3, 10, "Start of your turn: gain 1 Stamina.",
            A(Upkeep(EffectKind.GainStamina, 1))),

        M("Spellbound Grimoire", Card.CardType.Accesory,  Card.SpeedType.Channel, 2, 10, "Activate (Channel) — draw 1 card.",
            A(Activated(EffectKind.DrawCards, 1, Card.SpeedType.Channel))),

        M("Spellbook",           Card.CardType.Accesory,  Card.SpeedType.Channel, 2, 10, "Activate (Channel) — cast target Spell from your discard pile. It is removed from the game afterwards.",
            A(Activated(EffectKind.CastTargetSpellFromDiscard, 0, Card.SpeedType.Channel))),

        M("Leyline",             Card.CardType.Aura,      Card.SpeedType.Channel, 2, 11, "Your Spells cost 1 less.",
            A(Cost(EffectKind.ReduceCost, 1, CostScope.Spells))),

        M("Mind Over Matter",    Card.CardType.Talent,    Card.SpeedType.Channel, 1, 12, "Whenever you play a Spell, Scry 1.",
            A(When(Trigger.OnControllerPlaysSpell, EffectKind.Scry, 1))),

        M("Omniscience",         Card.CardType.Spell,     Card.SpeedType.Channel, 4, 12, "Draw your deck.",
            A(OnPlay(EffectKind.DrawEntireDeck, 0))),

        M("Fireball",            Card.CardType.Spell,     Card.SpeedType.Channel, 3, 12, "Deal 6 damage. Apply 3 Burn.",
            A(OnPlay(EffectKind.DealDamage, 6, EffectTarget.Opponent), OnPlay(EffectKind.ApplyBurn, 3, EffectTarget.Opponent))),

        M("Timewatch",           Card.CardType.Equipment, Card.SpeedType.Channel, 2, 13, "Activate (Reflex) — rearrange the stack.",
            A(Activated(EffectKind.RearrangeStack, 0, Card.SpeedType.Reflex))),

        M("Meteor Strike",       Card.CardType.Spell,     Card.SpeedType.Channel, 5, 17, "Deal 21 damage.",
            A(OnPlay(EffectKind.DealDamage, 21, EffectTarget.Opponent))),
    };


    // Cleric — Wisdom. Miracles, Burn as a weapon, Divine Shield and taxes.
    private static List<Def> Clerics() => new List<Def>
    {
        C("Heal",                Card.CardType.Miracle,   Card.SpeedType.Channel, 0, 4,  "Restore 3 Life.",
            A(OnPlay(EffectKind.GainLife, 3))),

        C("Holy Fire",           Card.CardType.Miracle,   Card.SpeedType.Channel, 1, 4,  "Apply 3 Burn.",
            A(OnPlay(EffectKind.ApplyBurn, 3, EffectTarget.Opponent))),

        C("Aegis",               Card.CardType.Miracle,   Card.SpeedType.Reflex,  1, 5,  "Gain 4 Divine Shield.",
            A(OnPlay(EffectKind.GainDivineShield, 4))),

        C("Absolution",          Card.CardType.Miracle,   Card.SpeedType.Channel, 1, 6,  "Remove all debuffs from yourself. Draw 1 card.",
            A(OnPlay(EffectKind.RemoveAllDebuffs, 0), OnPlay(EffectKind.DrawCards, 1))),

        C("Confession",          Card.CardType.Skill,     Card.SpeedType.Channel, 0, 6,  "Look at your opponent's hand. Scry 1.",
            A(OnPlay(EffectKind.RevealOpponentHand, 0), OnPlay(EffectKind.Scry, 1))),

        C("Tithe",               Card.CardType.Miracle,   Card.SpeedType.Reflex,  1, 7,  "The next card your opponent plays this turn costs 1 more.",
            A(OnPlay(EffectKind.IncreaseNextOpponentCardCost, 1, EffectTarget.Opponent))),

        C("Purifying Flame",     Card.CardType.Miracle,   Card.SpeedType.Channel, 0, 7,  "Lose all Burn and heal that amount.",
            A(OnPlay(EffectKind.RemoveAllBurnAndHeal, 0))),

        C("Silence",             Card.CardType.Miracle,   Card.SpeedType.Reflex,  0, 8,  "Your opponent can't play Reflex cards for the rest of this turn.",
            A(OnPlay(EffectKind.LockOpponentReflex, 0, EffectTarget.Opponent))),

        C("Sacred Relic",        Card.CardType.Accesory,  Card.SpeedType.Channel, 2, 8,  "Activate (Channel) — gain 2 Divine Shield.",
            A(Activated(EffectKind.GainDivineShield, 2, Card.SpeedType.Channel))),

        C("Vow of Penance",      Card.CardType.Aura,      Card.SpeedType.Channel, 2, 8,  "The next time a player plays more than one card in a turn, they take 3 damage.",
            A(When(Trigger.OnAnyPlayerPlaysSecondCard, EffectKind.DealDamage, 3),
              When(Trigger.OnAnyPlayerPlaysSecondCard, EffectKind.DestroySelf, 0))),

        C("Guilt",               Card.CardType.Condition, Card.SpeedType.Channel, 1, 8,  "Whenever you draw a card, you lose 1 Life.",
            A(When(Trigger.OnControllerDraws, EffectKind.DealDamage, 1))),

        C("Sacred Interdict",    Card.CardType.Miracle,   Card.SpeedType.Reflex,  2, 9,  "Destroy target card on the stack.",
            A(OnPlay(EffectKind.DestroyTargetOnStack, 0))),

        C("Bless",               Card.CardType.Miracle,   Card.SpeedType.Channel, 1, 9,  "Gain 2 Stamina at the start of your next turn.",
            A(OnPlay(EffectKind.GainStaminaNextUpkeep, 2))),

        C("Immolate",            Card.CardType.Miracle,   Card.SpeedType.Channel, 2, 9,  "Apply 6 Burn. You gain 3 Burn.",
            A(OnPlay(EffectKind.ApplyBurn, 6, EffectTarget.Opponent), OnPlay(EffectKind.ApplyBurn, 3))),

        C("Purify",              Card.CardType.Miracle,   Card.SpeedType.Channel, 1, 9,  "Destroy target condition.",
            A(OnPlay(EffectKind.DestroyTargetCondition, 0))),

        C("Tax",                 Card.CardType.Aura,      Card.SpeedType.Channel, 2, 10, "All costs are increased by 1.",
            A(Cost(EffectKind.IncreaseCost, 1, CostScope.AllCards),
              Cost(EffectKind.IncreaseCost, 1, CostScope.AllCards, EffectTarget.Opponent))),

        C("Sacred Tome",         Card.CardType.Equipment, Card.SpeedType.Channel, 2, 10, "Activate (Channel) — cast target Spell from your discard pile. It is removed from the game afterwards.",
            A(Activated(EffectKind.CastTargetSpellFromDiscard, 0, Card.SpeedType.Channel))),

        C("Sacred Ground",       Card.CardType.Aura,      Card.SpeedType.Channel, 2, 11, "Your Miracles cost 1 less.",
            A(Cost(EffectKind.ReduceCost, 1, CostScope.Miracles))),

        C("Divine Intervention", Card.CardType.Miracle,   Card.SpeedType.Reflex,  3, 12, "For the rest of the turn your costs are 0 and you can't draw cards.",
            A(OnPlay(EffectKind.FreeCostsButNoDraw, 0))),

        C("Ascension",           Card.CardType.Miracle,   Card.SpeedType.Channel, 2, 17, "If your deck and hand are empty, you win the game.",
            A(OnPlay(EffectKind.WinIfDeckAndHandEmpty, 0))),
    };
}
#endif
