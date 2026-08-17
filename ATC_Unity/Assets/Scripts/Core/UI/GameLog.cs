using System.Collections.Generic;
using UnityEngine;

/// What the game tells the player, in their language.
///
/// Two separate things, because they answer different questions:
///   • Instruction — "what must I do right now?" It stays on screen until it is answered.
///   • Feed — "what just happened?" A short rolling history that fades on its own.
///
/// Everything goes in as a Localization key plus values, never as an assembled sentence, so the
/// whole log re-reads correctly the moment the language changes.
public static class GameLog
{
    public const int MaxLines = 6;

    private static readonly List<Entry> feed = new List<Entry>();
    private static Entry instruction;

    /// Raised whenever the instruction or the feed changes, and when the language does.
    public static event System.Action Changed;

    private class Entry
    {
        public string Key;
        public object[] Args;
        public float Time;
        public string Text => Localization.T(Key, Args);
    }

    static GameLog()
    {
        // Re-render in place when the player switches language mid-match.
        Localization.Changed += () => Changed?.Invoke();
    }

    /// The standing instruction, or null when the game isn't waiting on anything.
    public static string Instruction => instruction?.Text;

    /// Most recent first.
    public static IReadOnlyList<string> Feed
    {
        get
        {
            var lines = new List<string>(feed.Count);
            for (int i = feed.Count - 1; i >= 0; i--) lines.Add(feed[i].Text);
            return lines;
        }
    }

    /// How long ago the newest line arrived, for fading.
    public static float SecondsSinceLastLine
        => feed.Count == 0 ? float.MaxValue : Time.unscaledTime - feed[feed.Count - 1].Time;

    /// Tell the player what the game is waiting for. Pass null to clear it.
    public static void Instruct(string key, params object[] args)
    {
        instruction = string.IsNullOrEmpty(key) ? null : new Entry { Key = key, Args = args };
        Changed?.Invoke();
    }

    /// An instruction whose text is already localised — targeting prompts arrive this way.
    public static void InstructLiteral(string text)
    {
        instruction = string.IsNullOrEmpty(text) ? null : new Entry { Key = text, Args = null };
        Changed?.Invoke();
    }

    public static void ClearInstruction() => Instruct(null);

    /// Add a line to the "what just happened" feed.
    public static void Say(string key, params object[] args)
    {
        feed.Add(new Entry { Key = key, Args = args, Time = Time.unscaledTime });
        while (feed.Count > MaxLines) feed.RemoveAt(0);
        Changed?.Invoke();
    }

    public static void Clear()
    {
        feed.Clear();
        instruction = null;
        Changed?.Invoke();
    }

}
