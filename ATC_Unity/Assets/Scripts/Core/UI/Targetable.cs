using UnityEngine;

/// Marks a card object as something an effect can point at. TargetingService collects these,
/// highlights the legal ones and turns a click into a chosen target.
///
/// IMPORTANT: this type must stay in a file named after it. Unity only creates a MonoScript for
/// the class matching the file name, and without one the component cannot be saved on a prefab —
/// which is exactly why targeting silently did nothing while this class lived inside
/// TargetingService.cs (the card prefab held a component with a null script reference).
[RequireComponent(typeof(CardDisplay))]
[DisallowMultipleComponent]
public class Targetable : MonoBehaviour
{
    [Tooltip("Optional object switched on while this card is a legal target. Left empty, the card art is tinted instead so targeting stays visible with no extra wiring.")]
    [SerializeField] private GameObject highlight;

    [Tooltip("Tint applied to the card art while it is a legal target (used only when no highlight object is assigned).")]
    [SerializeField] private Color highlightTint = new Color(1f, 0.88f, 0.35f, 1f);

    private CardDisplay display;
    private Color restingTint = Color.white;
    private bool capturedRestingTint;

    public Card Data => Display != null ? Display.cardData : null;

    public CardZone Zone => GetComponentInParent<CardZone>();

    public Player Owner => OwnerOf(gameObject);

    /// Who controls a card object. On the authoritative host a card remembers the hand it was dealt
    /// into; a client's mirrored board card was never in a hand, so fall back to the owner of the
    /// zone it sits in.
    ///
    /// Static on purpose: callers must be able to ask this of any card object, whether or not it
    /// carries a Targetable.
    public static Player OwnerOf(GameObject cardGO)
    {
        if (cardGO == null) return null;

        // The zone a card SITS IN wins over the hand it came from. A Condition is played from your
        // hand onto the opponent's board — it is theirs once it lands, and treating it as yours let
        // you right-click their own status card off the table.
        var zone = cardGO.GetComponentInParent<CardZone>();
        if (zone != null && zone.Owner != null) return zone.Owner;

        var movement = cardGO.GetComponent<CardMovement>();
        return movement != null ? movement.Owner : null;
    }

    private CardDisplay Display => display != null ? display : display = GetComponent<CardDisplay>();

    /// True while this card is lit up as a legal target. CardDisplay checks it before repainting
    /// the card art, so a redraw mid-prompt can't wipe the highlight off.
    public bool IsHighlighted { get; private set; }

    public void SetHighlight(bool on)
    {
        IsHighlighted = on;

        if (highlight != null)
        {
            highlight.SetActive(on);
            return;
        }
        TintCardArt(on);
    }

    private void TintCardArt(bool on)
    {
        var image = Display != null ? Display.cardImage : null;
        if (image == null) return;

        if (!capturedRestingTint)
        {
            restingTint = image.color;
            capturedRestingTint = true;
        }
        image.color = on ? highlightTint : restingTint;
    }
}
