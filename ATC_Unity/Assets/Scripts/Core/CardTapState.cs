using System.Collections.Generic;
using UnityEngine;

/// Per-card, per-turn state: whether the permanent is tapped, and which of its triggered abilities
/// have already fired this turn (triggers are once per turn unless a card says otherwise).
public class CardTapState : MonoBehaviour
{
    [SerializeField] private float tappedZRotation = -90f;

    private Quaternion untappedRotation = Quaternion.identity;
    private readonly HashSet<Trigger> firedThisTurn = new HashSet<Trigger>();

    public bool IsTapped { get; private set; }

    /// Raised whenever any card taps or untaps. The networking layer listens so it can re-publish
    /// the board — tapping changes no zone, so nothing else would notice.
    public static event System.Action<CardTapState> TapStateChanged;

    /// Claims this card's once-per-turn allowance for `trigger`. False means it already fired.
    public bool TryUseTrigger(Trigger trigger) => firedThisTurn.Add(trigger);

    /// New turn, fresh allowance for every trigger on this card.
    public void ResetTriggers() => firedThisTurn.Clear();

    public void Toggle()
    {
        if (IsTapped) Untap();
        else Tap();
    }

    public void Tap()
    {
        if (IsTapped) return;
        untappedRotation = transform.localRotation;
        transform.localRotation = untappedRotation * Quaternion.Euler(0f, 0f, tappedZRotation);
        IsTapped = true;
        TapStateChanged?.Invoke(this);
    }

    public void Untap()
    {
        if (!IsTapped) return;
        transform.localRotation = untappedRotation;
        IsTapped = false;
        TapStateChanged?.Invoke(this);
    }
}
