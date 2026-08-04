using System.Collections.Generic;
using UnityEngine;

/// A stable id for one physical card OBJECT — not for the card asset. CardDatabase already gives
/// every Card a shared id, but two copies of the same card have the same one, so it cannot answer
/// "which Iron Plate did you click?". Board targeting and remote activation need exactly that.
///
/// The host mints ids as it publishes the board; a client adopts them onto the mirrored copies it
/// builds. Afterwards either machine can turn an id back into its own local object, so a client
/// can name a card and the host knows precisely which one it meant.
[DisallowMultipleComponent]
public class CardInstance : MonoBehaviour
{
    /// The id of a card that has never been published — "no card".
    public const int None = 0;

    private static readonly Dictionary<int, CardInstance> registry = new Dictionary<int, CardInstance>();
    private static int nextId = 1;

    public int Id { get; private set; } = None;

    /// Host side: this card object's id, minting one the first time it is asked for.
    public static int EnsureId(GameObject cardGO)
    {
        var instance = GetOrAdd(cardGO);
        if (instance == null) return None;
        if (instance.Id == None) instance.Register(nextId++);
        return instance.Id;
    }

    /// Client side: adopt the id the host gave the card this object mirrors.
    public static void Bind(GameObject cardGO, int id)
    {
        if (id == None) return;
        var instance = GetOrAdd(cardGO);
        if (instance != null) instance.Register(id);
    }

    public static int IdOf(GameObject cardGO)
    {
        if (cardGO == null) return None;
        var instance = cardGO.GetComponent<CardInstance>();
        return instance != null ? instance.Id : None;
    }

    /// The local object carrying this id, or null when it does not exist on this machine.
    public static GameObject Find(int id)
    {
        if (id == None) return null;
        if (!registry.TryGetValue(id, out var instance) || instance == null)
        {
            registry.Remove(id);   // the card was destroyed; drop the stale entry
            return null;
        }
        return instance.gameObject;
    }

    /// Start a fresh numbering for a new hosting session, so ids from a previous match can't be
    /// mistaken for live ones.
    public static void ResetIds()
    {
        foreach (var instance in registry.Values)
            if (instance != null) instance.Id = None;   // leftovers must not answer to an old id

        registry.Clear();
        nextId = 1;
    }

    private static CardInstance GetOrAdd(GameObject cardGO)
    {
        if (cardGO == null) return null;
        var instance = cardGO.GetComponent<CardInstance>();
        return instance != null ? instance : cardGO.AddComponent<CardInstance>();
    }

    private void Register(int id)
    {
        if (Id == id) return;
        Unregister();
        Id = id;
        registry[id] = this;
    }

    private void Unregister()
    {
        if (Id != None && registry.TryGetValue(Id, out var current) && current == this)
            registry.Remove(Id);
        Id = None;
    }

    private void OnDestroy() => Unregister();
}
