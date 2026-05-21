using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;

    [Tooltip("Amount applied per +/- HP button press.")]
    [SerializeField] private int hpAdjustStep = 1;
    [Tooltip("Amount applied per +/- Stamina button press.")]
    [SerializeField] private int staminaAdjustStep = 1;

    private void Update()
    {
        if (player == null) return;
        if (hpText != null) hpText.text = $"HP {player.CurrentHp}/{player.MaxHp}";
        if (staminaText != null) staminaText.text = $"STA {player.Stamina}/{player.MaxStamina}";
    }

    public void HpUp()        { if (player != null) player.AdjustHp(+hpAdjustStep); }
    public void HpDown()      { if (player != null) player.AdjustHp(-hpAdjustStep); }
    public void StaminaUp()   { if (player != null) player.AdjustStamina(+staminaAdjustStep); }
    public void StaminaDown() { if (player != null) player.AdjustStamina(-staminaAdjustStep); }
}
