# Card Audit — every card vs. the rulings

All 78 cards checked individually against the ruling sheet, then the rulings applied to the code.
Verified against the real implementation in `Player.cs` / `EffectRunner.cs` and the authored
abilities on each `.asset` — not against code comments, several of which were stale.

**Status: 77 of 78 cards now behave as printed.** One card (`Dagger Dance`) is still unimplemented,
and one (`Slingshot`) needs a regenerate to pick up its new cost.

---

## What changed in this pass

| # | Ruling | Outcome |
|---|---|---|
| 1 | **Strike is an enabler** — untap target weapon, next Activate is buffed | ✅ Implemented — all 8 Strike cards |
| 2 | **Whenever = every time, many times per turn** | ✅ Implemented |
| 3 | **Equipment = armour, weapon, accessory, shield, consumable** | ✅ Implemented |
| 4 | **Slingshot must pay by discarding from field or discard pile** | ✅ Implemented (needs regenerate) |
| 5 | **Tome/Spellbook: cast from discard, but you pay the stamina** | ✅ Already correct — no change |
| 6 | **Block expires at the start of your next turn** | ✅ Already correct — kept as-is |
| 7 | **Trap doesn't exist** | ✅ Dropped from the spec |

### 1. Strike is now an enabler

Previously Strike untapped a weapon and *immediately* attacked with it. Now it untaps the weapon
and leaves a **Strike charge** on it; the weapon's next activation spends that charge and resolves
with the modifiers folded in. Since activating taps the weapon again, a Strike is effectively an
extra weapon use this turn.

- `StrikeBuff` (`EffectFramework.cs`) — multiplier, bonus, destroys-weapon.
- `CardTapState` holds a queue of charges; unspent ones lapse at end of turn ("for the rest of the turn").
- `EffectRunner.ArmStrike` untaps + queues. `RunAbilities` consumes a charge on activation.

**`Double Strike` ("Strike. Strike.") works correctly**: both charges land on the same weapon, and
after the first activation spends one, the weapon untaps again for the second.

**`Hurl`** (destroys the weapon) now destroys it *after* the buffed activation, not on play.

**Strike targets weapons *or* shields.** `TargetFilters.IsOwnStrikeTargetInPlay` accepts both, so a
Strike can ready a shield to answer with a block the same way it readies a weapon to answer with an
attack. Since activating re-taps, readying a shield effectively grants a second block that turn.

**`Backstab` passes its Reflex speed to what it readies.** A Strike played from a Reflex card sets
`StrikeBuff.grantsReflexActivation`, and the readied permanent may then be activated at Reflex
speed regardless of its printed speed — so you can react on the opponent's turn, swinging with a
weapon or blocking with a shield. `Player.ActivationTimingAllowed` and the reflex-response scan
both consult the pending charge. Channel Strike cards (everything else) leave the permanent on its
own speed.

> **Note:** the strike modifiers still only scale `DealDamage`. Readying a shield with
> `Heavy Swing` (+2) gives you the untap and the reflex window, but not +2 Block. Say if you want
> the bonus to scale whatever the readied permanent does — that would also mean `Hurl` doubling a
> shield's Block and then destroying the shield.

### 2. "Whenever" fires every time

`CardTapState.TryUseTrigger` now exempts `OnControllerPlaysSpell`, `OnControllerDraws` and
`OnAnyPlayerPlaysSecondCard` from the once-per-turn budget. Affects **Cloak of Protection**
(Block per Spell), **Mind Over Matter** (Scry per Spell) and **Guilt** (1 damage per draw — now
genuinely punishing against `Study` and `Omniscience`).

Other triggers keep the once-per-turn rule, per the ruling.

### 3. Equipment scope widened

`Card.IsEquipment` now covers **Weapon, Armour, Accessory, Shield, Consumable and Equipment**
(was Weapon/Accessory/Armour only). Widens what `Earthquake`, `Sunder`, `Disarm`,
`Bait and Switch` and Slingshot's new cost can hit.

### 4. Slingshot pays a real cost

New `EffectKind.SacrificeEquipment` (appended last — existing card data is untouched). It prompts
for one of your Equipment either **in play** (goes to discard) or **already in your discard**
(removed from the game).

It's authored *before* the damage, and `EffectContext.aborted` now stops a sequence mid-card, so
declining or having nothing to give **fizzles the whole card** rather than dealing free damage.
This closes the one live balance exploit — Slingshot was dealing 3 damage a turn for nothing.

> ⚠️ **Run `ATC ▸ Generate Card Library` again.** You ran it before this change landed, so
> Slingshot's asset still has only the damage ability. Everything else is already correct.

### 5–7. No code change needed

- **Tome / Spellbook** already charge the spell's stamina cost, which is what you want. Only the *card text* is silent about it — worth adding "pay its cost" when you next touch the wording.
- **Block** stays as it was: cleared at the owner's upkeep, so it guards you through the opponent's turn.
- **Trap** is off the spec; `Card.CardType` has no `Trap` value and none is needed.

---

## Rulings the engine already honoured

| Ruling | Status |
|---|---|
| **Burn** — end of each turn, damage = Burn, then halved rounded down | ✅ exact (the enum comment claiming "upkeep, −1" is stale; the code is right) |
| **Bleed** — end of turn, only if they took direct damage; no decay | ✅ exact |
| **Divine Shield** — prevent X from a single source, then lose all | ✅ exact |
| **Activate** — once/turn, Channel unless stated, taps the permanent | ✅ defaults are `Channel` + `tapToActivate` |
| **Scry** — top X, any number to discard, rest stay in order | ✅ |
| **Discarding** — the discarding player chooses | ✅ prompts per card |
| **Cost order** — reductions before increases | ✅ |
| **Equipment replacement** — you choose which to replace | ✅ |
| **Condition** — permanent, on the opponent's board | ✅ routed to `Opponent.auraZone`; `controller` correctly resolves to the victim |

**Still not implemented:** *"if multiple damage reductions apply, the defender chooses the order."*
Block is always spent before Divine Shield. Only matters when you hold both at once.

---

## Outstanding

### `Dagger Dance` — the only card with nothing behind it

> *"For the rest of the turn, your skills have: Strike."*

No abilities in the asset or the generator, and no `EffectKind` for it. Now that Strike is an
enabler this is cleaner to build than it was: it would mean "each Skill you play this turn also
readies a weapon". Needs a new turn-scoped effect.

### `Omniscience` — capped by the hand limit

> *"Draw your deck."*

`DrawEntireDeck` stops at `maxHandSize` (10). A 4-stamina INT17 card drawing 10 of 40 underdelivers.
Either exempt it from the cap or reword the card.

---

## Verified correct — the other 65

`Absolution` · `Aegis` · `Aether Focus` · `Arcane Barrier` · `Arcane Bolt` · `Arcane Staff` ·
`Ascension` · `Backstab`¹ · `Bait and Switch` · `Bless` · `Brace` · `Broken Stance` ·
`Cloak of Protection` · `Confession` · `Dagger` · `Dart` · `Defensive Stance` · `Disarm` ·
`Divination` · `Divine Intervention` · `Double Strike` · `Dual Wielding` · `Earthquake` ·
`Evasive Step` · `Fire Bolt` · `Fireball` · `Flow State` · `Fracture` · `Greatclub` · `Guilt` ·
`Heal` · `Heavy Swing` · `Hidden Dagger` · `Holy Fire` · `Hurl` · `Immolate` · `Iron Plate` ·
`Iron Skin` · `Keen Instinct` · `Kite Shield` · `Layered Armour` · `Leyline` · `Light Speed` ·
`Light Swing` · `Meteor Strike` · `Mind Over Matter` · `Monolith` · `Muscle Memory` · `Open Veins` ·
`Pickpocket` · `Purify` · `Purifying Flame` · `Quick Jab` · `Recall` · `Rewind` · `Sacred Ground` ·
`Sacred Interdict` · `Sacred Relic` · `Sacred Tome` · `Scrying Orb` · `Second Wind` · `Set-Up` ·
`Silence` · `Skull Splitter` · `Sleight of Hand` · `Spellbook` · `Spellbound Grimoire` · `Study` ·
`Sunder` · `Tax` · `Timewatch`² · `Tithe` · `Tower Shield` · `Unrelenting Rage` · `Vow of Penance`³

¹ `Backstab` readies a weapon or shield at Reflex speed — see the Strike section.
² a stale *"not implemented"* warning survives in `EffectRunner.ApplyInstant`; harmless, since `Timewatch` is an Activated ability and takes the coroutine path with the real reorder panel. It would only bite if a `RearrangeStack` were authored on a damage or death trigger.
³ the aura scans both boards but builds its context from whoever overextended, so the damage lands on *them*, not the aura's owner.
