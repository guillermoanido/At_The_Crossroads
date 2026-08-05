using UnityEngine;

/// The bonus a pending Strike hands to a weapon's next activation.
public readonly struct StrikeBonus
{
    public readonly int DamageMultiplier;
    public readonly int BonusDamage;
    public readonly bool DestroysWeapon;

    public StrikeBonus(int multiplier, int bonus, bool destroysWeapon)
    {
        DamageMultiplier = Mathf.Max(1, multiplier);
        BonusDamage = bonus;
        DestroysWeapon = destroysWeapon;
    }

    public int Scale(int baseDamage) => Mathf.Max(0, baseDamage * DamageMultiplier + BonusDamage);
}

/// A pending Strike sitting on a weapon: the strike card untapped it and promised its NEXT
/// activation an extra effect. Reading the charge spends it, whether or not it mattered.
///
/// Added at runtime rather than living on the card prefab — only a weapon that has actually been
/// struck needs one.
[DisallowMultipleComponent]
public class StrikeCharge : MonoBehaviour
{
    private StrikeBonus bonus;

    /// Hang a charge on `weaponGO`. A second strike replaces the first rather than stacking, so the
    /// most recent strike card is the one that pays out.
    public static void Apply(GameObject weaponGO, CardAbility strike)
    {
        if (weaponGO == null || strike == null) return;

        var charge = weaponGO.GetComponent<StrikeCharge>();
        if (charge == null) charge = weaponGO.AddComponent<StrikeCharge>();

        charge.bonus = new StrikeBonus(
            strike.strikeDamageMultiplier, strike.strikeBonusDamage, strike.strikeDestroysWeapon);
    }

    /// Take the charge off `weaponGO`. Returns false when the weapon isn't charged.
    public static bool TryTake(GameObject weaponGO, out StrikeBonus taken)
    {
        taken = default;

        var charge = weaponGO != null ? weaponGO.GetComponent<StrikeCharge>() : null;
        if (charge == null) return false;

        taken = charge.bonus;
        Destroy(charge);
        return true;
    }

    public static bool IsCharged(GameObject weaponGO)
        => weaponGO != null && weaponGO.GetComponent<StrikeCharge>() != null;
}
