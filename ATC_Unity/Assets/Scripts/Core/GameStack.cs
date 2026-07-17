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

    public void Push(StackItem item)
    {
        if (item == null || item.controller == null) return;
        items.Add(item);
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
    }

    private IEnumerator ResolveTop()
    {
        int top = items.Count - 1;
        StackItem item = items[top];
        items.RemoveAt(top);

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
