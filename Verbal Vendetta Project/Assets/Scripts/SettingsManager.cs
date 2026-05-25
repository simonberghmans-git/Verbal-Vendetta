using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages game settings that persist via PlayerPrefs.
/// Attach to the Settings panel GameObject.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Toggle for enabling / disabling text input during interrogation and phone call.")]
    public Toggle textInputToggle;

    private const string TextInputPrefKey = "TextInputEnabled";
    private const int TextInputDefaultValue = 1; // enabled by default

    private void OnEnable()
    {
        // Read current preference
        bool isEnabled = PlayerPrefs.GetInt(TextInputPrefKey, TextInputDefaultValue) == 1;

        // Set toggle without triggering the callback
        if (textInputToggle != null)
        {
            textInputToggle.SetIsOnWithoutNotify(isEnabled);
            textInputToggle.onValueChanged.AddListener(OnTextInputToggleChanged);
        }
    }

    private void OnDisable()
    {
        if (textInputToggle != null)
        {
            textInputToggle.onValueChanged.RemoveListener(OnTextInputToggleChanged);
        }
    }

    private void OnTextInputToggleChanged(bool isOn)
    {
        // Persist the preference
        PlayerPrefs.SetInt(TextInputPrefKey, isOn ? 1 : 0);
        PlayerPrefs.Save();

        // Live-update: tell InterrogationInputManager to refresh right now
        InterrogationInputManager inputManager = FindObjectOfType<InterrogationInputManager>();
        if (inputManager != null)
        {
            inputManager.RefreshTextInputVisibility();
        }
    }
}
