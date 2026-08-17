using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class GameStack : MonoBehaviour
{
    public static GameStack Instance { get; private set; }

    [Tooltip("Optional Pass button, shown only while a response window is open. Wire its OnClick to Pass().")]
    [SerializeField] private GameObject passButton;

    private readonly List<StackItem> items = new List<StackItem>();
    private bool running;
    private bool forceResolveTop;

    public bool IsEmpty => items.Count == 0;
    public bool IsResolving => running;
    public bool IsBusy => !IsEmpty || running;
    public int Count => items.Count;

    /// Everything waiting to resolve, oldest first — the top of the stack is the LAST entry.
    public IReadOnlyList<StackItem> Items => items;

    /// Raised whenever the stack gains, loses or resolves an item, so the display can follow it.
    public static event System.Action Changed;

    private void NotifyChanged() => Changed?.Invoke();

    // The player whose response window is currently open — null when there is none.
    // Use this to show "Player X: respond or pass" so local players know who acts.
    public Player PriorityPlayer => (!IsEmpty && !running && GameManager.Instance != null)
        ? GameManager.Instance.ControllingPlayer
        : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        UpdatePassButton();
    }

    /// True when `cardGO` is the source of an item still waiting on the stack. Counterspells target
    /// through this: the card object itself is what the player clicks.
    public bool HoldsCard(GameObject cardGO)
    {
        if (cardGO == null) return false;
        foreach (var item in items)
            if (item != null && item.sourceCardGO == cardGO) return true;
        return false;
    }

    /// Pull the item sourced by `cardGO` off the stack so it never resolves — the card itself is
    /// left where it is for the caller to dispose of (discard, exile, back to hand).
    public StackItem RemoveCard(GameObject cardGO)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null || items[i].sourceCardGO != cardGO) continue;

            var removed = items[i];
            items.RemoveAt(i);
            UpdatePassButton();
            NotifyChanged();
            return removed;
        }
        return null;
    }

    /// Rearrange what is waiting. `order` lists the CURRENT indices in the sequence they should end
    /// up in, so it can only ever be a permutation of what is already there — a client cannot use
    /// it to invent or drop an item. Anything missing keeps its place at the end.
    public void ApplyOrder(int[] order)
    {
        if (order == null || order.Length == 0) return;

        var rebuilt = new List<StackItem>(items.Count);
        var taken = new HashSet<int>();

        foreach (int index in order)
        {
            if (index < 0 || index >= items.Count || !taken.Add(index)) continue;
            rebuilt.Add(items[index]);
        }
        for (int i = 0; i < items.Count; i++)
            if (!taken.Contains(i)) rebuilt.Add(items[i]);

        if (rebuilt.Count != items.Count) return;   // refuse anything that isn't a clean permutation

        items.Clear();
        items.AddRange(rebuilt);
        NotifyChanged();
        Debug.Log($"[Stack] Reordered — {items.Count} item(s) now resolve top-down as listed.");
    }

    public void Push(StackItem item)
    {
        if (item == null || item.controller == null) return;
        items.Add(item);
        NotifyChanged();
        if (GameManager.Instance != null) GameManager.Instance.GivePriorityTo(item.controller.Opponent);
        UpdatePassButton();
        Advance();
    }

    public void Pass()
    {
        if (IsEmpty || running) return;
        forceResolveTop = true;
        Advance();
    }

    private void Advance()
    {
        if (!running) StartCoroutine(RunLoop());
    }

    private IEnumerator RunLoop()
    {
        running = true;
        UpdatePassButton();
        NotifyChanged();

        while (!IsEmpty)
        {
            var responder = GameManager.Instance != null ? GameManager.Instance.ControllingPlayer : null;
            bool canRespond = responder != null && responder.HasReflexResponse();

            if (canRespond && !forceResolveTop) break;   // wait for the responder to Pass() or respond

            forceResolveTop = false;
            yield return ResolveTop();
        }

        running = false;
        UpdatePassButton();
        NotifyChanged();
    }

    private IEnumerator ResolveTop()
    {
        int top = items.Count - 1;
        StackItem item = items[top];
        items.RemoveAt(top);
        NotifyChanged();

        Debug.Log($"[Stack] Resolving {item.sourceCardData?.cardName} ({item.trigger}) " +
                  $"for {item.controller?.name} — {items.Count} item(s) left below it.");

        if (item.controller != null && item.sourceCardData != null && EffectRunner.Instance != null)
            yield return EffectRunner.Instance.RunAbilities(
                item.sourceCardData,
                item.controller.BuildContext(item.sourceCardGO, item.sourceCardData),
                item.trigger);

        if (GameManager.Instance != null)
        {
            Player next = IsEmpty ? GameManager.Instance.ActivePlayer : items[items.Count - 1].controller.Opponent;
            GameManager.Instance.GivePriorityTo(next);
        }
        UpdatePassButton();
    }

    private void UpdatePassButton()
    {
        // Shown only when the stack is holding — i.e. a response is possible, not mid-resolution.
        bool windowOpen = !IsEmpty && !running;

        if (GameManager.Instance != null && GameManager.Instance.OnlineMode)
        {
            // Online: the server decides who may respond and pushes it to that seat; each
            // client shows its own button via the seat's SyncVar hook (see NetworkPlayerSeat).
            if (NetworkServer.active)
                NetworkPlayerSeat.ServerSetResponseWindow(windowOpen ? PriorityPlayer : null);
            return;
        }

        ShowPassButton(windowOpen);
    }

    // Toggle the local Pass button. Called locally offline, or by the local seat's SyncVar
    // hook online. Safe to call on any peer.
    public void ShowPassButton(bool visible)
    {
        if (passButton != null) passButton.SetActive(visible);
    }
}
