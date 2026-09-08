using TiltBrush;
using UnityEngine;

public class ViewModeUI : MonoBehaviour
{
    public GameObject m_UiRoot;
    public GameObject m_CloseButton;
    public GameObject m_MenuButton;

    // Authored in the prefab so its appearance/placement can be tweaked in the editor. Wire its
    // Button OnClick to HandleSkipPlaybackButton. Shown only while a sketch is playing back.
    public GameObject m_SkipPlaybackButton;

    public static ViewModeUI m_Instance;

    void Awake()
    {
        m_UiRoot.SetActive(!App.VrSdk.IsHmdInitialized() || App.Config.m_SdkMode == SdkMode.Monoscopic);
        m_Instance = this;
    }

    void Update()
    {
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

    public void HandleCloseButton()
    {
        if (InitNoHeadsetMode.m_Instance == null)
        {
            // We're viewing a sketch so close it and open the loading dialog
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
