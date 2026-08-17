using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Works out what a card's numbers ACTUALLY are right now, as opposed to what is printed on it.
/// A Greatclub prints "4 damage" but hits for 6 while a Heavy Swing charge is on it; Arcane Staff
/// prints "X + 1" and X changes every time you cast; a Leyline makes your spells cheaper.
///
/// CardDisplay renders whatever this returns, so the rules and the card face can never disagree.
public static class CardValues
{
    /// What `card` costs `owner` right now. Falls back to the printed cost with no owner.
    public static int Cost(Player owner, Card card)
    {
        if (card == null) return 0;
        return owner != null ? owner.StaminaCostOf(card) : card.energyCost;
    }

    /// A short "here is what it does right now" line, or empty when nothing differs from the print.
    /// Only abilities whose magnitude has actually changed are listed, so a card in a plain board
    /// state shows nothing extra.
    public static string LiveSummary(GameObject cardGO, Card card, Player owner)
    {
        if (card == null || card.abilities == null || owner == null) return string.Empty;

        var context = owner.BuildContext(cardGO, card);
        var parts = new List<string>();

        foreach (var ability in card.abilities)
        {
            if (ability == null || ability.effect == EffectKind.None) continue;

            int printed = ability.amount;
            int actual = EffectRunner.EffectiveAmount(ability, context);
            if (actual == printed) continue;

            string noun = NounFor(ability.effect);
            if (noun == null) continue;
            parts.Add($"{actual} {noun}");
        }

        if (parts.Count == 0) return string.Empty;

        var text = new StringBuilder("<color=#FFD24A>▲ ");
        text.Append(string.Join(", ", parts));
        text.Append("</color>");
        return text.ToString();
    }

    // Only effects with a number worth showing on the face get a word here; the rest are skipped.
    private static string NounFor(EffectKind effect)
    {
        switch (effect)
        {
            case EffectKind.DealDamage:          return "damage";
            case EffectKind.GainBlock:           return "Block";
            case EffectKind.GainLife:            return "Life";
            case EffectKind.GainStamina:         return "Stamina";
            case EffectKind.LoseStamina:         return "Stamina lost";
            case EffectKind.DrawCards:           return "cards";
            case EffectKind.ApplyBurn:           return "Burn";
            case EffectKind.ApplyBleed:          return "Bleed";
            case EffectKind.GainDivineShield:    return "Divine Shield";
            case EffectKind.Scry:                return "Scry";
            case EffectKind.ReduceIncomingDamage: return "damage prevented";
            case EffectKind.OpponentDiscards:    return "discarded";
            default:                             return null;
        }
    }
}
