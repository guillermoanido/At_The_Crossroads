using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleAI : MonoBehaviour
{
    [Tooltip("The player this AI controls (usually Player 2).")]
    [SerializeField] private Player me;

    [Tooltip("Seconds between each AI action, so you can watch what it does.")]
    [SerializeField] private float stepDelay = 0.7f;

    [Tooltip("Turn the AI off to drive Player 2 by hand instead.")]
    [SerializeField] private bool autoPlay = true;

    private bool takingTurn;

    private void Update()
    {
        if (!autoPlay || me == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;

        var stack = GameStack.Instance;
        if (stack != null && !stack.IsEmpty && gm.IsControllingPlayer(me))
        {
            stack.Pass();
            return;
        }

        bool stackClear = stack == null || stack.IsEmpty;
        if (!takingTurn && gm.ActivePlayer == me && gm.IsControllingPlayer(me) && stackClear)
            StartCoroutine(TakeTurn());
    }

    private IEnumerator TakeTurn()
    {
        takingTurn = true;
        var gm = GameManager.Instance;

        int safety = 30;
        while (gm.ActivePlayer == me && safety-- > 0)
        {
            yield return new WaitForSeconds(stepDelay);
            if (gm.ActivePlayer != me) break;

            switch (gm.CurrentPhase)
            {
                case GameManager.GamePhase.Main1:
                case GameManager.GamePhase.Main2:
                    yield return PlayHand();
                    yield return ActivateBoard();
                    yield return WaitForStack();
                    gm.AdvancePhase();
                    break;
                default:
                    gm.AdvancePhase();
                    break;
            }
        }

        takingTurn = false;
    }

    private IEnumerator PlayHand()
    {
        if (me.handManager == null) { Debug.LogWarning($"[AI] {name}: no HandManager wired on 'me'."); yield break; }

        var hand = new List<GameObject>(me.handManager.cardsInHand);
        int played = 0;
        foreach (var cardGO in hand)
        {
            if (cardGO == null) continue;
            var data = cardGO.GetComponent<CardDisplay>()?.cardData;
            if (data == null) continue;
            if (me.TryPlayCard(cardGO, data))
            {
                played++;
                yield return WaitForStack();
                yield return new WaitForSeconds(stepDelay);
            }
        }
        Debug.Log($"[AI] {me.name} played {played}/{hand.Count} card(s) in {GameManager.Instance.CurrentPhase}.");
    }

    private IEnumerator ActivateBoard()
    {
        yield return ActivateZone(me.weaponZone);
        yield return ActivateZone(me.shieldZone);
        yield return ActivateZone(me.armourZone);
        yield return ActivateZone(me.equipmentZone);
        yield return ActivateZone(me.accessoryZone);
        yield return ActivateZone(me.talentZone);
        yield return ActivateZone(me.auraZone);
    }

    private IEnumerator ActivateZone(CardZone zone)
    {
        if (zone == null) yield break;
        foreach (var cardGO in new List<GameObject>(zone.Cards))
        {
            if (cardGO == null) continue;
            var data = cardGO.GetComponent<CardDisplay>()?.cardData;
            if (data != null && data.FirstActivated() != null && me.TryActivateCard(cardGO, data))
                yield return WaitForStack();
        }
    }

    private IEnumerator WaitForStack()
    {
        var stack = GameStack.Instance;
        if (stack == null) yield break;
        yield return new WaitUntil(() => stack.IsEmpty);
    }
}
