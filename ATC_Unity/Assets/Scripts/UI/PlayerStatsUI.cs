using TMPro;
using UnityEngine;

// One per player. Drag the player's HP and Stamina TMP_Texts into the slots,
// and wire the four button OnClick events to HpUp / HpDown / StaminaUp / StaminaDown.
public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;

    private void Update()
    {
        if (player == null) return;
        if (hpText != null) hpText.text = $"HP {player.CurrentHp}/{player.MaxHp}";
        if (staminaText != null) staminaText.text = $"STA {player.Stamina}/{player.MaxStamina}";
    }

    public void HpUp()      { if (player != null) player.AdjustHp(+1); }
    public void HpDown()    { if (player != null) player.AdjustHp(-1); }
    public void StaminaUp()   { if (player != null) player.AdjustStamina(+1); }
    public void StaminaDown() { if (player != null) player.AdjustStamina(-1); }
}
