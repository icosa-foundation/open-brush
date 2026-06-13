using TiltBrush;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ViewModeUI : MonoBehaviour
{
    public GameObject m_UiRoot;
    public GameObject m_CloseButton;
    public GameObject m_MenuButton;

    public static ViewModeUI m_Instance;

    // Runtime-created so we don't depend on prefab wiring; cloned from the close button to inherit
    // its styling, pointer-cursor handling and canvas placement.
    private GameObject m_SkipPlaybackButton;

    void Awake()
    {
        m_UiRoot.SetActive(!App.VrSdk.IsHmdInitialized() || App.Config.m_SdkMode == SdkMode.Monoscopic);
        m_Instance = this;
    }

    void Update()
    {
        // Only relevant in the non-VR UI, and only while a sketch is actively playing back.
        bool showSkip = m_UiRoot != null && m_UiRoot.activeInHierarchy
            && SketchMemoryScript.m_Instance != null
            && SketchMemoryScript.m_Instance.IsPlayingBack;

        if (showSkip)
        {
            EnsureSkipPlaybackButton();
        }
        if (m_SkipPlaybackButton != null && m_SkipPlaybackButton.activeSelf != showSkip)
        {
            m_SkipPlaybackButton.SetActive(showSkip);
        }
    }

    private void EnsureSkipPlaybackButton()
    {
        if (m_SkipPlaybackButton != null || m_CloseButton == null)
        {
            return;
        }

        m_SkipPlaybackButton = Instantiate(m_CloseButton, m_CloseButton.transform.parent);
        m_SkipPlaybackButton.name = "Skip Playback";

        RectTransform rect = m_SkipPlaybackButton.GetComponent<RectTransform>();
        if (rect != null)
        {
            // Bottom-right corner, clear of the top-right close button.
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-30f, 30f);
            rect.sizeDelta = new Vector2(120f, 50f);
        }

        TextMeshProUGUI label = m_SkipPlaybackButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = "Skip »";
            label.enableAutoSizing = false;
            label.fontSize = 26f;
        }

        Button button = m_SkipPlaybackButton.GetComponent<Button>();
        if (button != null)
        {
            // RemoveAllListeners only clears runtime listeners; the close button's HandleCloseButton
            // is a serialized persistent call that came along with the clone, so disable it explicitly.
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleSkipPlaybackButton);
        }

        m_SkipPlaybackButton.SetActive(false);
    }

    public void HandleSkipPlaybackButton()
    {
        App.Instance.RequestQuickLoad();
    }

    public void HandleCloseButton()
    {
        // TODO If we allow other tools than the FlyTool, we should probably have
        // a better way to test if the load sketch dialog is showing
        if (InitNoHeadsetMode.m_Instance == null)
        {
            // We're viewing a sketch so close it and open the loading dialog
            ApiMethods.NewSketch();
            SketchSurfacePanel.m_Instance.DisableSpecificTool(BaseTool.ToolType.FlyTool);
            App.Instance.CreateFailedToDetectVrDialog();
        }
        else
        {
            // We're hopefully showing the sketch loading dialog so close the app
            Application.Quit();
        }
    }
}
