using TiltBrush;
using TMPro;
using UnityEngine;

public class ViewModeUI : MonoBehaviour
{
    public GameObject m_UiRoot;
    public GameObject m_CloseButton;
    public GameObject m_MenuButton;

    // Authored in the prefab so its appearance/placement can be tweaked in the editor. Wire its
    // Button OnClick to HandleSkipPlaybackButton. Shown only while a sketch is playing back.
    public GameObject m_SkipPlaybackButton;

    // Authored in the prefab like the skip button; wire its OnClick to HandleArNavigationButton.
    // Shown only while viewing a sketch on a device that can do handheld AR.
    public GameObject m_ArNavigationButton;
    public TMP_Text m_ArNavigationLabel;
    // Shown only while AR navigation is active; wire its OnClick to HandleArCameraFeedButton.
    public GameObject m_ArCameraFeedButton;
    public TMP_Text m_ArCameraFeedLabel;

    private const float kArErrorDisplaySeconds = 3f;
    private float m_ArErrorHideTime;

    public static ViewModeUI m_Instance;

    void Awake()
    {
        m_UiRoot.SetActive(!App.VrSdk.IsHmdInitialized() || App.Config.m_SdkMode == SdkMode.Monoscopic);
        m_Instance = this;
    }

    void OnEnable()
    {
        HandheldArNavigation.StateChanged += OnArNavigationStateChanged;
    }

    void OnDisable()
    {
        HandheldArNavigation.StateChanged -= OnArNavigationStateChanged;
    }

    void Update()
    {
        UpdateArNavigationButton();

        if (m_SkipPlaybackButton == null)
        {
            return;
        }

        // Only relevant in the non-VR UI, and only while a sketch is actively playing back.
        bool showSkip = m_UiRoot != null && m_UiRoot.activeInHierarchy
            && SketchMemoryScript.m_Instance != null
            && SketchMemoryScript.m_Instance.IsPlayingBack;
        if (m_SkipPlaybackButton.activeSelf != showSkip)
        {
            m_SkipPlaybackButton.SetActive(showSkip);
        }
    }

    public void HandleSkipPlaybackButton()
    {
        App.Instance.RequestQuickLoad();
    }

    public void HandleArNavigationButton()
    {
        HandheldArNavigation.Toggle();
    }

    public void HandleArCameraFeedButton()
    {
        HandheldArNavigation.CameraFeed = !HandheldArNavigation.CameraFeed;
    }

    private void OnArNavigationStateChanged()
    {
        if (HandheldArNavigation.CurrentState == HandheldArNavigation.State.Off &&
            HandheldArNavigation.LastError != null)
        {
            m_ArErrorHideTime = Time.realtimeSinceStartup + kArErrorDisplaySeconds;
        }
    }

    private void UpdateArNavigationButton()
    {
        if (m_ArCameraFeedButton != null)
        {
            bool showFeedButton = HandheldArNavigation.IsActive;
            if (m_ArCameraFeedButton.activeSelf != showFeedButton)
            {
                m_ArCameraFeedButton.SetActive(showFeedButton);
            }
            if (showFeedButton && m_ArCameraFeedLabel != null)
            {
                string feedLabel = HandheldArNavigation.CameraFeed ? "Hide room" : "Show room";
                if (m_ArCameraFeedLabel.text != feedLabel)
                {
                    m_ArCameraFeedLabel.text = feedLabel;
                }
            }
        }

        if (m_ArNavigationButton == null)
        {
            return;
        }

        // Not while the sketch picker is up: it isn't placed to follow a moving camera.
        bool show = HandheldArNavigation.CurrentState != HandheldArNavigation.State.Off ||
            (HandheldArNavigation.IsSupported && InitNoHeadsetMode.m_Instance == null);
        if (m_ArNavigationButton.activeSelf != show)
        {
            m_ArNavigationButton.SetActive(show);
        }
        if (!show || m_ArNavigationLabel == null)
        {
            return;
        }

        string label;
        switch (HandheldArNavigation.CurrentState)
        {
            case HandheldArNavigation.State.Starting:
                label = "AR...";
                break;
            case HandheldArNavigation.State.Active:
                label = "Exit AR";
                break;
            default:
                label = Time.realtimeSinceStartup < m_ArErrorHideTime ? "No AR" : "AR";
                break;
        }
        if (m_ArNavigationLabel.text != label)
        {
            m_ArNavigationLabel.text = label;
        }
    }

    public void HandleCloseButton()
    {
        if (InitNoHeadsetMode.m_Instance == null)
        {
            // We're viewing a sketch so close it and open the loading dialog
            HandheldArNavigation.SetActive(false);
            ApiMethods.NewSketch();
            SketchControlsScript.m_Instance.DisableViewOnlyNavigationTool();
            App.Instance.CreateFailedToDetectVrDialog();
        }
        else
        {
            // We're hopefully showing the sketch loading dialog so close the app
            Application.Quit();
        }
    }
}
