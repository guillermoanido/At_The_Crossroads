#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-click authoring of the whole card library from the design spec. Type-safe (no hand-written
// YAML), idempotent (updates existing assets by name, keeping their GUIDs; creates the rest).
// Run from the Unity menu: ATC ▸ Generate Card Library.
//
// Cards whose text needs systems we haven't built yet (Strike, Bleed/Burn/Divine Shield, cost
// reducers, equip-limits, face-down, copy/steal, speed grants) get correct stats + description but
// EMPTY abilities for now — they exist and are playable, they just don't do their special thing yet.
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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CardLibrary] Done — {created} created, {updated} updated in {Folder}.");
    }

    // Adds the Targetable component to the card prefab if it's missing (needed for Destroy/Return-target
    // effects). Uses the safe prefab-contents API rather than editing YAML by hand.
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
        card.intRequired = 0;
        card.wisRequired = 0;
        card.deckCost = 1;
        card.effectDescription = d.text;
        card.abilities = d.abilities ?? new List<CardAbility>();

        if (isNew) { AssetDatabase.CreateAsset(card, path); created++; }
        else { EditorUtility.SetDirty(card); updated++; }
    }

    // ---- Ability builders ---------------------------------------------

    private static List<CardAbility> A(params CardAbility[] xs) => new List<CardAbility>(xs);

    private static CardAbility OnPlay(EffectKind e, int amt, EffectTarget t = EffectTarget.Controller)
        => new CardAbility { trigger = Trigger.OnPlay, effect = e, amount = amt, target = t, tapToActivate = false };

    private static CardAbility Upkeep(EffectKind e, int amt)
        => new CardAbility { trigger = Trigger.OnUpkeep, effect = e, amount = amt, target = EffectTarget.Controller, tapToActivate = false };

    private static CardAbility Activated(EffectKind e, int amt, Card.SpeedType speed, EffectTarget t = EffectTarget.Controller, int cost = 0)
        => new CardAbility { trigger = Trigger.Activated, effect = e, amount = amt, target = t, activationSpeed = speed, activationCost = cost, tapToActivate = true };

    private static Def W(string name, Card.CardType type, Card.SpeedType speed, int stamina, int str, string text, List<CardAbility> abilities = null)
        => new Def { name = name, type = type, speed = speed, stamina = stamina, str = str, dex = 0, text = text, abilities = abilities };

    private static Def R(string name, Card.CardType type, Card.SpeedType speed, int stamina, int dex, string text, List<CardAbility> abilities = null)
        => new Def { name = name, type = type, speed = speed, stamina = stamina, str = 0, dex = dex, text = text, abilities = abilities };

    private class Def
    {
        public string name;
        public Card.CardType type;
        public Card.SpeedType speed;
        public int stamina, str, dex;
        public string text;
        public List<CardAbility> abilities;
    }

    // ---- The card library (from the design spec) ----------------------
    // Abilities are filled where the current effect vocabulary supports them; deferred cards note why.

    private static List<Def> Warriors() => new List<Def>
    {
        W("Brace",            Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 5,  "Gain 4 Block.",                                        A(OnPlay(EffectKind.GainBlock, 4))),
        W("Light Swing",      Card.CardType.Attack,    Card.SpeedType.Channel, 1, 5,  "Strike"),                                              // needs Strike
        W("Kite Shield",      Card.CardType.Shield,    Card.SpeedType.Channel, 2, 6,  "Activate (Reflex) — Gain 2 Block",                     A(Activated(EffectKind.GainBlock, 2, Card.SpeedType.Reflex))),
        W("Heavy Swing",      Card.CardType.Attack,    Card.SpeedType.Channel, 2, 7,  "Strike: +2 Damage"),                                   // needs Strike
        W("Second Wind",      Card.CardType.Skill,     Card.SpeedType.Channel, 1, 8,  "Gain 2 Stamina.",                                      A(OnPlay(EffectKind.GainStamina, 2))),
        W("Iron Skin",        Card.CardType.Talent,    Card.SpeedType.Channel, 2, 8,  "Start of turn: Gain 1 Block",                          A(Upkeep(EffectKind.GainBlock, 1))),
        W("Hurl",             Card.CardType.Attack,    Card.SpeedType.Channel, 1, 8,  "Strike: Deal double damage. Destroy this weapon."),    // needs Strike
        W("Sunder",           Card.CardType.Skill,     Card.SpeedType.Channel, 2, 9,  "Destroy target equipment.",                            A(OnPlay(EffectKind.DestroyTargetEquipment, 0))),
        W("Unrelenting Rage", Card.CardType.Talent,    Card.SpeedType.Channel, 2, 9,  "Your attacks cost 1 less."),                           // needs cost modifiers
        W("Layered Armour",   Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "Start of turn: Lose 1 Stamina. You can equip 1 more Armour.", A(Upkeep(EffectKind.LoseStamina, 1))), // equip-limit part deferred
        W("Tower Shield",     Card.CardType.Shield,    Card.SpeedType.Channel, 3, 10, "Activate (Reflex) — Gain 3 Block",                     A(Activated(EffectKind.GainBlock, 3, Card.SpeedType.Reflex))),
        W("Greatclub",        Card.CardType.Weapon,    Card.SpeedType.Channel, 3, 10, "Activate (Channel) — Deal 4 damage.",                  A(Activated(EffectKind.DealDamage, 4, Card.SpeedType.Channel, EffectTarget.Opponent))),
        W("Iron Plate",       Card.CardType.Armour,    Card.SpeedType.Channel, 3, 10, "Start of turn: Gain 3 Block",                          A(Upkeep(EffectKind.GainBlock, 3))),
        W("Broken Stance",    Card.CardType.Condition, Card.SpeedType.Channel, 1, 11, "Whenever you gain Block, reduce it by 2."),            // needs block-modifier conditions
        W("Monolith",         Card.CardType.Weapon,    Card.SpeedType.Channel, 3, 12, "Activate (Channel) Pay 1 Stamina — Deal 7 damage.",    A(Activated(EffectKind.DealDamage, 7, Card.SpeedType.Channel, EffectTarget.Opponent, 1))),
        W("Skull Splitter",   Card.CardType.Attack,    Card.SpeedType.Channel, 3, 13, "Strike: Opponent discard 3 cards.",                    A(OnPlay(EffectKind.OpponentDiscards, 3, EffectTarget.Opponent))), // Strike wrapper simplified
        W("Fracture",         Card.CardType.Condition, Card.SpeedType.Channel, 3, 13, "Start of turn: Lose 1 Stamina.",                       A(Upkeep(EffectKind.LoseStamina, 1))),
        W("Earthquake",       Card.CardType.Attack,    Card.SpeedType.Channel, 4, 17, "Destroy all opponent's equipment.",                    A(OnPlay(EffectKind.DestroyAllOpponentEquipment, 0, EffectTarget.Opponent))),
    };

    private static List<Def> Rogues() => new List<Def>
    {
        R("Flow State",       Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 5,  "Your next card is cast at reflex speed. Scry 1.",      A(OnPlay(EffectKind.Scry, 1))), // speed-grant part deferred
        R("Dagger",           Card.CardType.Weapon,    Card.SpeedType.Channel, 1, 5,  "Activate (Channel) — Deal 1 damage.",                  A(Activated(EffectKind.DealDamage, 1, Card.SpeedType.Channel, EffectTarget.Opponent))),
        R("Evasive Step",     Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 5,  "Avoid the next source of direct damage this turn."),   // needs Evade
        R("Quick Jab",        Card.CardType.Attack,    Card.SpeedType.Reflex,  0, 5,  "Deal 3 damage.",                                       A(OnPlay(EffectKind.DealDamage, 3, EffectTarget.Opponent))),
        R("Open Veins",       Card.CardType.Skill,     Card.SpeedType.Channel, 1, 6,  "Strike: -1 Damage. Apply Bleed 1."),                   // needs Strike + Bleed
        R("Pickpocket",       Card.CardType.Skill,     Card.SpeedType.Channel, 2, 7,  "Look at your opponent's hand, take one card."),        // needs steal-from-hand
        R("Hidden Dagger",    Card.CardType.Weapon,    Card.SpeedType.Channel, 2, 8,  "Activate (Reflex) — Deal 1 damage. +2 if you played a Reflex card this turn.", A(Activated(EffectKind.DealDamage, 1, Card.SpeedType.Reflex, EffectTarget.Opponent))), // conditional +2 deferred
        R("Backstab",         Card.CardType.Skill,     Card.SpeedType.Reflex,  1, 8,  "Strike: Reflex"),                                      // needs Strike
        R("Slingshot",        Card.CardType.Weapon,    Card.SpeedType.Channel, 1, 8,  "Activate (Channel) Discard 1 Equipment or remove one from discard — Deal 3 damage.", A(Activated(EffectKind.DealDamage, 3, Card.SpeedType.Channel, EffectTarget.Opponent))), // resource cost deferred
        R("Disarm",           Card.CardType.Skill,     Card.SpeedType.Reflex,  1, 9,  "Return target equipment to opponent's hand.",          A(OnPlay(EffectKind.ReturnTargetEquipmentToHand, 0))),
        R("Bait and Switch",  Card.CardType.Skill,     Card.SpeedType.Reflex,  0, 9,  "Return target Equipment you own to hand. You may play 1 Equipment from your hand without paying its cost."), // needs free-play
        R("Keen Instinct",    Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "All your cards can be played at Reflex speed."),       // needs speed grant
        R("Dual Wielding",    Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "Start of turn: Lose 1 Stamina. You can equip 1 more weapon.", A(Upkeep(EffectKind.LoseStamina, 1))), // equip-limit deferred
        R("Double Strike",    Card.CardType.Attack,    Card.SpeedType.Channel, 2, 10, "Strike. Strike."),                                     // needs Strike
        R("Sleight of Hand",  Card.CardType.Skill,     Card.SpeedType.Channel, 2, 10, "Copy an equipment from opponent, destroy the original."), // needs copy
        R("Muscle Memory",    Card.CardType.Talent,    Card.SpeedType.Channel, 2, 11, "Your skills cost 1 less."),                            // needs cost modifiers
        R("Set-Up",           Card.CardType.Skill,     Card.SpeedType.Channel, 0, 12, "Place a card face down, next turn play it with cost 0."), // needs face-down
        R("Dagger Dance",     Card.CardType.Skill,     Card.SpeedType.Channel, 1, 12, "For the rest of the turn, your skills have: Strike."), // needs Strike
        R("Light Speed",      Card.CardType.Skill,     Card.SpeedType.Channel, 2, 17, "Take an extra turn after this one.",                    A(OnPlay(EffectKind.TakeExtraTurn, 0))),
        R("Defensive Stance", Card.CardType.Talent,    Card.SpeedType.Channel, 1, 9,  "You gain +1 Block from all sources."),                 // needs block-modifier conditions
    };
}
#endif
