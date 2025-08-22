using TiltBrush;
using UnityEngine;

public class ViewModeUI : MonoBehaviour
{
    public GameObject m_UiRoot;
    public GameObject m_CloseButton;
    public GameObject m_MenuButton;

    public static ViewModeUI m_Instance;

    void Awake()
    {
        m_UiRoot.SetActive(!App.VrSdk.IsHmdInitialized() || App.Config.m_SdkMode == SdkMode.Monoscopic);
        m_Instance = this;
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
