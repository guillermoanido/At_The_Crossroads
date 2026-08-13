using UnityEngine;
using UnityEngine.Audio;

/// Player preferences that outlive a session. Stored in PlayerPrefs and applied the moment they
/// change, so the settings panel needs no Apply button.
///
/// Master volume works with no setup at all (it drives the AudioListener). Music and SFX need an
/// AudioMixer with exposed float parameters — assign one on the menu's settings panel and name the
/// parameters to match, otherwise those two sliders are remembered but inert.
public static class GameSettings
{
    private const string MasterKey = "atc.audio.master";
    private const string MusicKey  = "atc.audio.music";
    private const string SfxKey    = "atc.audio.sfx";

    public const string MusicMixerParameter = "MusicVolume";
    public const string SfxMixerParameter   = "SfxVolume";

    private static AudioMixer mixer;
    private static bool loaded;

    public static float Master { get; private set; } = 1f;
    public static float Music  { get; private set; } = 1f;
    public static float Sfx    { get; private set; } = 1f;

    /// Called by the settings panel so music/sfx have somewhere to go. Optional.
    public static void UseMixer(AudioMixer audioMixer)
    {
        mixer = audioMixer;
        ApplyAll();
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        Master = PlayerPrefs.GetFloat(MasterKey, 1f);
        Music  = PlayerPrefs.GetFloat(MusicKey, 1f);
        Sfx    = PlayerPrefs.GetFloat(SfxKey, 1f);
        ApplyAll();
    }

    public static void SetMaster(float value) => Set(MasterKey, value, v => Master = v);
    public static void SetMusic(float value)  => Set(MusicKey, value, v => Music = v);
    public static void SetSfx(float value)    => Set(SfxKey, value, v => Sfx = v);

    private static void Set(string key, float value, System.Action<float> assign)
    {
        value = Mathf.Clamp01(value);
        assign(value);
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        ApplyAll();
    }

    private static void ApplyAll()
    {
        AudioListener.volume = Master;
        if (mixer == null) return;

        mixer.SetFloat(MusicMixerParameter, ToDecibels(Music));
        mixer.SetFloat(SfxMixerParameter, ToDecibels(Sfx));
    }

    // Mixers work in decibels on a log scale; 0 has to be pinned to silence rather than -infinity.
    private static float ToDecibels(float linear)
        => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
}
