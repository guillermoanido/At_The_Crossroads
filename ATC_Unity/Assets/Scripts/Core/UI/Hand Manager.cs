using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    public DeckManager deckManager;

    public GameObject cardPrefab;
    public Transform handPosition;

    public float fanSpread = 5f;
    public float cardSpacing = 100f;
    public float verticalSpacing = 10f;

    [Header("Sizing")]
    [Tooltip("Visual scale of cards in this hand. 1 = prefab size.")]
    [Range(0.2f, 1.5f)] public float cardScale = 1f;

    [Tooltip("Total horizontal space the fan may occupy. When the fan would exceed this, spacing shrinks so cards overlap instead of running off-screen.")]
    public float maxHandWidth = 800f;

    [SerializeField] private bool isFlipped = false;
    [SerializeField] private bool showFaceUp = true;
    [SerializeField] private int maxHandSize = 10;

    public Player Owner { get; private set; }
    public List<GameObject> cardsInHand = new List<GameObject>();
    public bool IsHandFull => cardsInHand.Count >= maxHandSize;

    /// Raised after any change to the hand's contents (add/remove/clear). The networking layer
    /// subscribes on the server to push the new hand state to clients. No-op offline.
    public event System.Action OnHandChanged;

    public void SetOwner(Player player) => Owner = player;

    /// Whether this hand may be shown face-up on THIS machine.
    ///
    /// Online it is derived from ownership rather than trusted to whatever SetFaceUpMode was last
    /// told: only the local player's own hand is ever readable, so no mis-ordered network callback
    /// can leave the opponent's cards showing. Offline (hotseat) the authored setting stands, since
    /// both players share one screen.
    /// Set while an effect has laid this hand open to the player looking at it. Lasts until the
    /// turn ends, then the cards go back to being backs.
    private bool revealed;

    public void RevealUntilEndOfTurn()
    {
        revealed = true;
        RefreshPrivacy();
        Debug.Log($"[Reveal] {name} is laid open for the rest of the turn.");
    }

    public void ClearReveal()
    {
        if (!revealed) return;
        revealed = false;
        RefreshPrivacy();
    }

    public bool MayShowFaceUp => ShouldShowFaceUp();

    private bool ShouldShowFaceUp()
    {
        if (revealed) return true;   // an effect is showing this hand to whoever is watching

        var gm = GameManager.Instance;
        if (gm == null || Owner == null) return showFaceUp;

        // Online: each machine only ever reads its own hand.
        if (gm.OnlineMode) return Owner == gm.LocalDisplayPlayer;

        // Hotseat: both hands share one screen, so only the player who may currently act sees
        // theirs. That follows priority rather than the turn, so a reflex response still works.
        return gm.IsControllingPlayer(Owner);
    }

    /// Re-apply that rule to every card currently held.
    public void RefreshPrivacy()
    {
        bool faceUp = ShouldShowFaceUp();
        foreach (var go in cardsInHand)
        {
            var display = go != null ? go.GetComponent<CardDisplay>() : null;
            if (display != null && display.IsFaceUp != faceUp) display.SetFaceUp(faceUp);
        }
    }

    public void AddCardToHand(Card cardData)
    {
        var newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        var display = newCard.GetComponent<CardDisplay>();
        display.cardData = cardData;
        display.SetFaceUp(ShouldShowFaceUp());

        newCard.GetComponent<CardMovement>().Init(this);

        LayoutHand();
        OnHandChanged?.Invoke();
    }

    public void RemoveCardFromHand(GameObject card)
    {
        cardsInHand.Remove(card);
        LayoutHand();
        OnHandChanged?.Invoke();
    }

    /// Flip every card in this hand (and any added afterwards) face-up or face-down. Used by the
    /// network layer to set each machine's perspective: your own hand face-up, the opponent's down.
    public void SetFaceUpMode(bool faceUp)
    {
        showFaceUp = faceUp;
        foreach (var go in cardsInHand)
            if (go != null) go.GetComponent<CardDisplay>()?.SetFaceUp(faceUp);
    }

    /// Destroy every card object and empty the hand. Clients rebuild their view of a hand from
    /// synced state, so they clear first to stay in lock-step with the authoritative host.
    public void ClearHand()
    {
        foreach (var go in cardsInHand)
            if (go != null) Destroy(go);
        cardsInHand.Clear();
        LayoutHand();
        OnHandChanged?.Invoke();
    }

    /// Add a faceless, non-interactive card back — one per hidden card in an opponent's hand.
    public void AddFaceDownPlaceholder()
    {
        var newCard = Instantiate(cardPrefab, handPosition.position, Quaternion.identity, handPosition);
        cardsInHand.Add(newCard);

        var display = newCard.GetComponent<CardDisplay>();
        display.cardData = null;
        display.SetFaceUp(false);

        CardDisplay.DisableGameplayInteractions(newCard);
        newCard.GetComponent<CardMovement>().Init(this);

        LayoutHand();
    }

    public void RefreshLayout() => LayoutHand();

    private void LayoutHand()
    {
        // Every add, remove and clear passes through here, so it is the one place guaranteed to run
        // whenever a hand changes — and therefore where privacy is worth re-asserting.
        RefreshPrivacy();

        int count = cardsInHand.Count;
        if (count == 0) return;

        Vector3 scale = Vector3.one * cardScale;

        if (count == 1)
        {
            cardsInHand[0].GetComponent<CardMovement>().SetSlot(Vector3.zero, Quaternion.identity, scale);
            return;
        }

        float spacing = EffectiveSpacing(count);
        for (int i = 0; i < count; i++)
        {
            var slot = SlotFor(i, count, spacing);
            cardsInHand[i].GetComponent<CardMovement>().SetSlot(slot.position, slot.rotation, scale);
        }
    }

    private float EffectiveSpacing(int count)
    {
        float total = (count - 1) * cardSpacing;
        return total > maxHandWidth ? maxHandWidth / (count - 1) : cardSpacing;
    }

    private (Vector3 position, Quaternion rotation) SlotFor(int index, int count, float spacing)
    {
        float flip = isFlipped ? -1f : 1f;
        float center = (count - 1) / 2f;

        float angle = flip * fanSpread * (index - center);
        var rotation = Quaternion.Euler(0f, 0f, angle);

        float x = spacing * (index - center);
        float y = flip * verticalSpacing * ParabolicLift(index, count);

        return (new Vector3(x, y, 0f), rotation);
    }

    private static float ParabolicLift(int index, int count)
    {
        if (count <= 1) return 0f;
        float t = (2f * index) / (count - 1) - 1f;
        return 1f - t * t;
    }
}
