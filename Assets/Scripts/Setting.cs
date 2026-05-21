using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    [Header("Buttons & Panel")]
    [Tooltip("Button shown in-game that opens the settings panel.")]
    [SerializeField] private Button settingsButton;
    [Tooltip("Root GameObject of the settings panel. Toggled active/inactive.")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Button inside the panel that toggles global mute on/off.")]
    [SerializeField] private Button muteButton;
    [Tooltip("Button inside the panel that closes the settings panel.")]
    [SerializeField] private Button exitButton;

    [Header("Mute Label (optional)")]
    [Tooltip("Optional label on the mute button. Updated to reflect current mute state.")]
    [SerializeField] private TMP_Text muteLabel;
    [SerializeField] private string mutedText = "음소거 해제";
    [SerializeField] private string unmutedText = "음소거";

    [Header("Behavior")]
    [Tooltip("Panel starts hidden.")]
    [SerializeField] private bool hidePanelOnStart = true;

    private bool isMuted = false;

    private void Start()
    {
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenPanel);
        if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
        if (exitButton != null) exitButton.onClick.AddListener(ClosePanel);

        if (hidePanelOnStart && settingsPanel != null) settingsPanel.SetActive(false);

        isMuted = AudioListener.volume <= 0f;
        UpdateMuteLabel();
    }

    private void OnDestroy()
    {
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenPanel);
        if (muteButton != null) muteButton.onClick.RemoveListener(ToggleMute);
        if (exitButton != null) exitButton.onClick.RemoveListener(ClosePanel);
    }

    public void OpenPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        AudioListener.volume = isMuted ? 0f : 1f;
        UpdateMuteLabel();
    }

    private void UpdateMuteLabel()
    {
        if (muteLabel == null) return;
        muteLabel.text = isMuted ? mutedText : unmutedText;
    }
}
