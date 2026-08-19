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

    // "Whenever you play/draw something" means every time, not once a turn — these triggers are
    // reactions to an action the controller can repeat, so they never claim the per-turn allowance.
    private static readonly HashSet<Trigger> Unlimited = new HashSet<Trigger>
    {
        Trigger.OnControllerPlaysSpell,
        Trigger.OnControllerDraws,
        Trigger.OnAnyPlayerPlaysSecondCard,
    };

    /// Claims this card's once-per-turn allowance for `trigger`. False means it already fired.
    public bool TryUseTrigger(Trigger trigger)
        => Unlimited.Contains(trigger) || firedThisTurn.Add(trigger);

    /// New turn, fresh allowance for every trigger on this card. Unspent Strike charges lapse too —
    /// a Strike readies the weapon "for the rest of the turn", not indefinitely.
    public void ResetTriggers()
    {
        firedThisTurn.Clear();
        pendingStrikes.Clear();
    }

    #region Limited uses

    private int usesSpent;

    /// Whether a card with a capped number of activations (Torch = 2) has any left.
    /// `maxUses` of 0 means unlimited.
    public bool HasUseLeft(int maxUses) => maxUses <= 0 || usesSpent < maxUses;

    /// Records one activation and reports whether that was the last one the card had.
    public bool SpendUse(int maxUses)
    {
        usesSpent++;
        return maxUses > 0 && usesSpent >= maxUses;
    }

    #endregion

    #region Strike charges

    private readonly List<StrikeBuff> pendingStrikes = new List<StrikeBuff>();

    public bool HasPendingStrike => pendingStrikes.Count > 0;

    /// A Strike card readied this weapon; its next activation spends this charge.
    public void QueueStrike(StrikeBuff buff)
    {
        if (buff == null) return;
        pendingStrikes.Add(buff);
    }

    /// The charge the next activation would spend, without spending it. The activation-timing
    /// check needs this to know whether the permanent may act at Reflex speed.
    public StrikeBuff PeekPendingStrike() => pendingStrikes.Count > 0 ? pendingStrikes[0] : null;

    /// Takes the oldest unspent charge, so "Strike. Strike." spends them in the order played.
    public StrikeBuff ConsumePendingStrike()
    {
        if (pendingStrikes.Count == 0) return null;

        var next = pendingStrikes[0];
        pendingStrikes.RemoveAt(0);
        return next;
    }

    #endregion

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
