using TMPro;
using UnityEngine;

/// Keeps one label in the current language. Put it on any TMP_Text, give it a key from
/// Localization, and it redraws itself whenever the language changes — including labels that were
/// authored in the scene by hand.
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Key into Localization's UI table, e.g. 'menu.host'.")]
    [SerializeField] private string key;

    private TMP_Text label;

    public void SetKey(string newKey)
    {
        key = newKey;
        Apply();
    }

    private void Awake() => label = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        Localization.Load();
        Localization.Changed += Apply;
        Apply();
    }

    private void OnDisable() => Localization.Changed -= Apply;

    private void Apply()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label != null && !string.IsNullOrEmpty(key)) label.text = Localization.T(key);
    }
}
