using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text defenseText;

    [Header("Statuses")]
    [Tooltip("Shows every active status beside the other stats. Auto-created next to the defence label if left empty.")]
    [SerializeField] private TMP_Text statusText;

    [Tooltip("Glyphs used for each status. Swap for TMP sprite tags like <sprite=0> once you have an icon atlas.")]
    [SerializeField] private string burnIcon = "🔥";
    [SerializeField] private string bleedIcon = "🩸";
    [SerializeField] private string divineShieldIcon = "✦";

    [SerializeField] private Color burnColour = new Color(0.95f, 0.28f, 0.15f);
    [SerializeField] private Color bleedColour = new Color(0.75f, 0.1f, 0.2f);
    [SerializeField] private Color divineShieldColour = new Color(1f, 0.87f, 0.45f);

    [Tooltip("Amount applied per +/- HP button press.")]
    [SerializeField] private int hpAdjustStep = 1;
    [Tooltip("Amount applied per +/- Stamina button press.")]
    [SerializeField] private int staminaAdjustStep = 1;
    [Tooltip("Amount applied per +/- Defense button press.")]
    [SerializeField] private int defenseAdjustStep = 1;

    private void Awake() => EnsureStatusLabel();

    // Statuses need somewhere to go even in a scene laid out before they existed, so clone the
    // defence label and sit the copy just below it.
    private void EnsureStatusLabel()
    {
        if (statusText != null || defenseText == null) return;

        statusText = Instantiate(defenseText, defenseText.transform.parent);
        statusText.name = "Status Text";
        statusText.text = string.Empty;

        var rect = statusText.rectTransform;
        rect.anchoredPosition = defenseText.rectTransform.anchoredPosition
                              + new Vector2(0f, -rect.sizeDelta.y);
    }

    private void Update()
    {
        if (player == null) return;
        if (hpText != null) hpText.text = $"HP {player.CurrentHp}/{player.MaxHp}";
        if (staminaText != null) staminaText.text = $"STA {player.Stamina}/{player.MaxStamina}";
        if (defenseText != null) defenseText.text = DefenceLine();
        if (statusText != null) statusText.text = StatusLine();
    }

    private string DefenceLine()
        => player.MaxDefense > 0
            ? $"DEF {player.Defense}/{player.MaxDefense}"
            : $"DEF {player.Defense}";

    /// Every active status, as a coloured icon and its number, next to the other stats. Statuses
    /// only appear while they are actually on the player, so a clean board shows nothing at all.
    private string StatusLine()
    {
        var line = new System.Text.StringBuilder();
        Append(line, player.Burn, burnIcon, burnColour);
        Append(line, player.Bleed, bleedIcon, bleedColour);
        Append(line, player.DivineShield, divineShieldIcon, divineShieldColour);
        return line.ToString();
    }

    private static void Append(System.Text.StringBuilder line, int value, string icon, Color colour)
    {
        if (value <= 0) return;
        if (line.Length > 0) line.Append("  ");
        line.Append($"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{icon} {value}</color>");
    }

    public void HpUp()        { if (player != null) player.AdjustHp(+hpAdjustStep); }
    public void HpDown()      { if (player != null) player.AdjustHp(-hpAdjustStep); }
    public void StaminaUp()   { if (player != null) player.AdjustStamina(+staminaAdjustStep); }
    public void StaminaDown() { if (player != null) player.AdjustStamina(-staminaAdjustStep); }
    public void DefenseUp()   { if (player != null) player.AdjustDefense(+defenseAdjustStep); }
    public void DefenseDown() { if (player != null) player.AdjustDefense(-defenseAdjustStep); }
}
