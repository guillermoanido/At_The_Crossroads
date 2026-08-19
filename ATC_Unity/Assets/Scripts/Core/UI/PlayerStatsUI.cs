using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text defenseText;

    [Header("Statuses")]
    [Tooltip("Shows every active status above the other stats. Auto-created just above the HP label if left empty.")]
    [SerializeField] private TMP_Text statusText;

    [Tooltip("How far above the HP label the status row sits. 0 = one label height. Raise it if the tokens overlap the stats.")]
    [SerializeField] private float statusOffsetY = 0f;

    [Tooltip("Label for each status. Kept to characters LiberationSans actually has — emoji and " +
             "symbols like ✦ are not in it and render as a hollow box. Swap these for TMP sprite " +
             "tags such as <sprite=0> once you have an icon atlas.")]
    [SerializeField] private string burnIcon = "BURN";
    [SerializeField] private string bleedIcon = "BLEED";
    [SerializeField] private string divineShieldIcon = "SHIELD";

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

    // Statuses need somewhere to go even in a scene laid out before they existed. The row sits
    // ABOVE the stat block, so Burn / Bleed / Divine Shield read as tokens stacked on top of HP
    // and Defence. Cloned from the HP label so it inherits the font, size and anchoring the scene
    // already set up.
    private void EnsureStatusLabel()
    {
        if (statusText != null) return;

        var template = hpText != null ? hpText : defenseText;
        if (template == null) return;

        statusText = Instantiate(template, template.transform.parent);
        statusText.name = "Status Text";
        statusText.text = string.Empty;

        var source = template.rectTransform;
        var rect = statusText.rectTransform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = source.sizeDelta;

        float step = statusOffsetY > 0f ? statusOffsetY : Mathf.Max(source.sizeDelta.y, 24f);
        rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, step);
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
