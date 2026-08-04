using UnityEngine;
using UnityEngine.EventSystems;

/// Pointer behaviour for a card the player can look at and act on: enlarge it in the preview on
/// hover, answer a targeting prompt, double-click to activate it, right/middle click to send it to
/// the discard/exile pile.
[RequireComponent(typeof(CardDisplay))]
[RequireComponent(typeof(CardTapState))]
[RequireComponent(typeof(Targetable))]
public class CardBoardActions : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// True on a client's copy of the host's board. The real card lives on the host, so the copy is
    /// only ever inspected, targeted, or used to send an intent over the wire — never resolved here.
    public bool IsMirrored { get; private set; }

    public void SetMirrored(bool mirrored) => IsMirrored = mirrored;

    #region Preview

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardPreview.Instance == null) return;

        var display = GetComponent<CardDisplay>();
        if (display == null || !display.IsFaceUp || display.cardData == null) return;
        if (!CanInspect()) return;

        CardPreview.Instance.Show(display.cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    /// Online, every card you can see face-up is yours to inspect: the board is public and your own
    /// hand is only ever face-up on your own screen, while the opponent's cards render as backs and
    /// are rejected above. Offline the two hands share one screen, so a hand card is previewable
    /// only on its owner's turn — otherwise sweeping the mouse over the other fan would leak it.
    private bool CanInspect()
    {
        var gm = GameManager.Instance;
        if (gm == null) return true;
        if (gm.OnlineMode) return true;

        var owner = GetOwner();
        if (owner == null || owner.handManager == null) return true;
        if (!owner.handManager.cardsInHand.Contains(gameObject)) return true;

        return gm.IsActivePlayer(owner);
    }

    #endregion

    #region Clicks

    public void OnPointerClick(PointerEventData eventData)
    {
        var targeting = TargetingService.Instance;
        if (targeting != null && targeting.IsActive)
        {
            AnswerTargetingPrompt(targeting, eventData.button);
            return;
        }

        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                if (eventData.clickCount >= 2) TryActivate();
                return;
            case PointerEventData.InputButton.Right:
                SendToPile(discard: true);
                return;
            case PointerEventData.InputButton.Middle:
                SendToPile(discard: false);
                return;
        }
    }

    private void AnswerTargetingPrompt(TargetingService targeting, PointerEventData.InputButton button)
    {
        if (button == PointerEventData.InputButton.Left) targeting.TryChoose(GetComponent<Targetable>());
        else if (button == PointerEventData.InputButton.Right) targeting.Cancel();
    }

    // Routed through MatchInput so a client sends the intent to the host instead of resolving it
    // locally, where it would desync the moment the host disagreed.
    private void TryActivate()
    {
        var card = GetComponent<CardDisplay>()?.cardData;
        if (card == null || card.FirstActivated() == null) return;

        MatchInput.ActivateCard(gameObject);
    }

    // The manual "put this card away" controls. They mutate the board on the spot, with no intent
    // sent anywhere, so only a machine that owns the real board may use them — offline, or the host.
    private void SendToPile(bool discard)
    {
        if (IsMirrored || !MatchInput.HasLocalAuthority) return;

        var owner = GetOwner();
        if (owner == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsControllingPlayer(owner)) return;

        if (discard) owner.SendToDiscard(gameObject);
        else owner.SendToExile(gameObject);

        if (CardPreview.Instance != null) CardPreview.Instance.Hide();
    }

    #endregion

    private Player GetOwner() => Targetable.OwnerOf(gameObject);
}
