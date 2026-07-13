using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A deliberately dumb opponent so Player 2 does *something* while you test cards. On its turn it plays
// every card it can afford, activates its board permanents, and clicks through the phases. No strategy.
// Setup: put this on a GameObject (e.g. Player 2) and drag Player 2 into `me`.
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
        if (!autoPlay || me == null || takingTurn) return;
        var gm = GameManager.Instance;
        if (gm != null && gm.ActivePlayer == me && gm.IsControllingPlayer(me))
            StartCoroutine(TakeTurn());
    }

    private IEnumerator TakeTurn()
    {
        takingTurn = true;
        var gm = GameManager.Instance;

        int safety = 30;   // hard stop against any phase-loop surprise (e.g. chained extra turns)
        while (gm.ActivePlayer == me && safety-- > 0)
        {
            yield return new WaitForSeconds(stepDelay);
            if (gm.ActivePlayer != me) break;   // turn/priority changed under us

            switch (gm.CurrentPhase)
            {
                case GameManager.GamePhase.Main1:
                case GameManager.GamePhase.Main2:
                    yield return PlayHand();
                    ActivateBoard();
                    gm.AdvancePhase();
                    break;
                default:                         // Combat / EndTurn (Draw auto-advances to Main1)
                    gm.AdvancePhase();
                    break;
            }
        }

        takingTurn = false;
    }

    // Try to play every card in hand; TryPlayCard rejects anything unaffordable or illegal this phase.
    private IEnumerator PlayHand()
    {
        if (me.handManager == null) yield break;
        foreach (var cardGO in new List<GameObject>(me.handManager.cardsInHand))
        {
            if (cardGO == null) continue;
            var data = cardGO.GetComponent<CardDisplay>()?.cardData;
            if (data == null) continue;
            if (me.TryPlayCard(cardGO, data))
                yield return new WaitForSeconds(stepDelay);
        }
    }

    // Activate any board permanent with an activated ability (deals damage / gains block, etc.).
    private void ActivateBoard()
    {
        ActivateZone(me.weaponZone);
        ActivateZone(me.shieldZone);
        ActivateZone(me.armourZone);
        ActivateZone(me.equipmentZone);
        ActivateZone(me.accessoryZone);
        ActivateZone(me.talentZone);
        ActivateZone(me.auraZone);
    }

    private void ActivateZone(CardZone zone)
    {
        if (zone == null) return;
        foreach (var cardGO in new List<GameObject>(zone.Cards))
        {
            if (cardGO == null) continue;
            var data = cardGO.GetComponent<CardDisplay>()?.cardData;
            if (data != null && data.FirstActivated() != null)
                me.TryActivateCard(cardGO, data);
        }
    }
}
