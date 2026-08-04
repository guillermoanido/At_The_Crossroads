using System;
using UnityEngine;

/// Sends a "pick a card" request to the machine of the player who has to answer it.
///
/// Every effect resolves on the authoritative host, so without this the prompt for a card the
/// remote client played would open on the HOST's screen and the host would choose the target —
/// which is exactly why Player 2 could play Sunder but never got to aim it.
///
/// Offline, and for the host's own player, the local TargetingService is used unchanged. For a
/// remote seat the legal targets are worked out here (on the authority) and shipped to that client;
/// its answer comes back as a Command and resumes the waiting effect.
public static class NetworkTargeting
{
    public static void Request(Player chooser, Predicate<Targetable> filter, string prompt,
                               Action<Targetable> onChosen, Action onCancel)
    {
        var seat = NetworkPlayerSeat.ForPlayer(chooser);

        if (seat != null && !seat.isLocalPlayer)
            seat.ServerRequestTarget(filter, prompt, onChosen, onCancel);
        else
            RequestLocally(chooser, filter, prompt, onChosen, onCancel);
    }

    private static void RequestLocally(Player chooser, Predicate<Targetable> filter, string prompt,
                                       Action<Targetable> onChosen, Action onCancel)
    {
        var service = TargetingService.Instance;
        if (service == null)
        {
            Debug.LogWarning("[Targeting] No TargetingService in the scene — the effect fizzles.");
            onCancel?.Invoke();
            return;
        }
        service.Request(filter, onChosen, onCancel, chooser, prompt);
    }
}
