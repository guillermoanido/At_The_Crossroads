using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TargetingService : MonoBehaviour
{
    public static TargetingService Instance { get; private set; }

    private Predicate<Targetable> activeFilter;
    private Action<Targetable> activeOnChosen;
    private Action activeOnCancel;
    private readonly List<Targetable> currentValidTargets = new List<Targetable>();

    public bool IsActive => activeFilter != null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Request(Predicate<Targetable> filter, Action<Targetable> onChosen, Action onCancel = null)
    {
        if (IsActive)
        {
            Debug.LogWarning("[Targeting] Request ignored — already active.");
            return;
        }
        if (filter == null || onChosen == null)
        {
            Debug.LogWarning("[Targeting] Request needs a filter and an onChosen callback.");
            return;
        }

        foreach (var t in FindObjectsByType<Targetable>(FindObjectsSortMode.None))
        {
            if (t != null && filter(t))
            {
                currentValidTargets.Add(t);
                t.SetHighlight(true);
            }
        }

        if (currentValidTargets.Count == 0)
        {
            Debug.Log("[Targeting] No valid targets — request auto-cancelled.");
            onCancel?.Invoke();
            return;
        }

        activeFilter = filter;
        activeOnChosen = onChosen;
        activeOnCancel = onCancel;
        Debug.Log($"[Targeting] Awaiting choice — {currentValidTargets.Count} valid target(s).");
    }

    public bool TryChoose(Targetable t)
    {
        if (!IsActive || t == null) return false;
        if (!currentValidTargets.Contains(t)) return false;

        var onChosen = activeOnChosen;
        Clear();
        onChosen?.Invoke(t);
        return true;
    }

    public void Cancel()
    {
        if (!IsActive) return;
        var onCancel = activeOnCancel;
        Clear();
        Debug.Log("[Targeting] Cancelled.");
        onCancel?.Invoke();
    }

    private void Update()
    {
        if (!IsActive) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Cancel();
    }

    private void Clear()
    {
        foreach (var t in currentValidTargets)
            if (t != null) t.SetHighlight(false);
        currentValidTargets.Clear();
        activeFilter = null;
        activeOnChosen = null;
        activeOnCancel = null;
    }
}
