using Mirror;
using UnityEngine;

/// Single entry point for the local player's "control" inputs (pass priority, advance phase).
/// Wire the Pass button and the phase-advance button OnClick to the methods here instead of
/// calling GameStack/GameManager directly. It routes the intent based on mode:
///   • Offline  → call the game logic directly (unchanged behaviour).
///   • Online   → send a [Command] from THIS client's seat so the host validates and runs it.
public class MatchInput : MonoBehaviour
{
    public static MatchInput Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Hook the phase-advance Button's OnClick to this.
    public void RequestAdvancePhase()
    {
        if (IsOnline())
        {
            var seat = LocalSeat();
            if (seat != null) seat.CmdAdvancePhase();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvancePhase();
        }
    }

    // Hook the Pass Button's OnClick to this.
    public void RequestPass()
    {
        if (IsOnline())
        {
            var seat = LocalSeat();
            if (seat != null) seat.CmdPass();
        }
        else if (GameStack.Instance != null)
        {
            GameStack.Instance.Pass();
        }
    }

    // Play a card from hand. Offline: play it locally. Online: send a Command from the local seat
    // so the host validates speed/priority (reflex anytime with the stack, channel only on your
    // turn) and resolves it. Returns true only when the card was consumed locally (offline); online
    // it returns false so the caller snaps it back — the authoritative hand sync removes it if legal.
    public static bool PlayCard(GameObject cardGO)
    {
        if (cardGO == null) return false;
        var hand = cardGO.GetComponentInParent<HandManager>();
        var data = cardGO.GetComponent<CardDisplay>() != null ? cardGO.GetComponent<CardDisplay>().cardData : null;
        if (hand == null || data == null) return false;

        // Offline, or the host (which is authoritative), plays directly and reports the result.
        if (!IsOnline() || NetworkServer.active)
            return hand.Owner != null && hand.Owner.TryPlayCard(cardGO, data);

        // A pure client sends the card's STABLE id (not a race-prone list index — the client hand
        // is an async mirror that can shift under it). Freeze the dropped card immediately so a
        // snap-back can't be re-dropped and fire a duplicate/stale command before the hand re-syncs.
        var seat = LocalSeat();
        var db = CardDatabase.Instance;
        int id = db != null ? db.Id(data) : -1;
        if (seat == null || id < 0) return false;
        seat.CmdPlayCard(id);
        CardDisplay.DisableGameplayInteractions(cardGO);
        return false;
    }

    private static bool IsOnline()
        => GameManager.Instance != null && GameManager.Instance.OnlineMode;

    private static NetworkPlayerSeat LocalSeat()
    {
        var lp = NetworkClient.localPlayer;
        return lp != null ? lp.GetComponent<NetworkPlayerSeat>() : null;
    }
}
