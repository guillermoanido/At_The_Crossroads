using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text defenseText;

    [Tooltip("Amount applied per +/- HP button press.")]
    [SerializeField] private int hpAdjustStep = 1;
    [Tooltip("Amount applied per +/- Stamina button press.")]
    [SerializeField] private int staminaAdjustStep = 1;
    [Tooltip("Amount applied per +/- Defense button press.")]
    [SerializeField] private int defenseAdjustStep = 1;

    private void Update()
    {
        if (player == null) return;
        if (hpText != null) hpText.text = $"HP {player.CurrentHp}/{player.MaxHp}";
        if (staminaText != null) staminaText.text = $"STA {player.Stamina}/{player.MaxStamina}";
        if (defenseText != null) defenseText.text = DefenceLine();
    }

    // Divine Shield and Burn ride along on the defence line: both matter constantly once the
    // cleric cards are in play, and this way they show up without any extra scene wiring.
    private string DefenceLine()
    {
        string line = player.MaxDefense > 0
            ? $"DEF {player.Defense}/{player.MaxDefense}"
            : $"DEF {player.Defense}";

        if (player.DivineShield > 0) line += $"  |  SHIELD {player.DivineShield}";
        if (player.Burn > 0) line += $"  |  BURN {player.Burn}";
        if (player.Bleed > 0) line += $"  |  BLEED {player.Bleed}";
        return line;
    }

    public void HpUp()        { if (player != null) player.AdjustHp(+hpAdjustStep); }
    public void HpDown()      { if (player != null) player.AdjustHp(-hpAdjustStep); }
    public void StaminaUp()   { if (player != null) player.AdjustStamina(+staminaAdjustStep); }
    public void StaminaDown() { if (player != null) player.AdjustStamina(-staminaAdjustStep); }
    public void DefenseUp()   { if (player != null) player.AdjustDefense(+defenseAdjustStep); }
    public void DefenseDown() { if (player != null) player.AdjustDefense(-defenseAdjustStep); }
}
