using System.Collections.Generic;
using UnityEngine;

public class GameStack : MonoBehaviour
{
    public static GameStack Instance { get; private set; }

    [Tooltip("Optional Pass button, shown only while a response window is open. Wire its OnClick to Pass().")]
    [SerializeField] private GameObject passButton;

    private readonly List<StackItem> items = new List<StackItem>();

    public bool IsEmpty => items.Count == 0;
    public int Count => items.Count;

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
        Settle();
    }

    public void Pass()
    {
        if (IsEmpty) return;
        ResolveTopOnce();
        Settle();
    }

    private void Settle()
    {
        int guard = 64;
        while (!IsEmpty && guard-- > 0)
        {
            var responder = GameManager.Instance != null ? GameManager.Instance.ControllingPlayer : null;
            if (responder != null && responder.HasReflexResponse()) break;
            ResolveTopOnce();
        }
        UpdatePassButton();
    }

    private void ResolveTopOnce()
    {
        if (IsEmpty) return;

        int top = items.Count - 1;
        StackItem item = items[top];
        items.RemoveAt(top);

        if (item.controller != null && item.sourceCardData != null && EffectRunner.Instance != null)
            EffectRunner.Instance.FireAbilities(
                item.sourceCardData,
                item.controller.BuildContext(item.sourceCardGO, item.sourceCardData),
                item.trigger);

        if (GameManager.Instance != null)
        {
            Player next = IsEmpty ? GameManager.Instance.ActivePlayer : items[items.Count - 1].controller.Opponent;
            GameManager.Instance.GivePriorityTo(next);
        }
    }

    private void UpdatePassButton()
    {
        if (passButton != null) passButton.SetActive(!IsEmpty);
    }
}
