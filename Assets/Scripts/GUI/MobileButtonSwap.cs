using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MobileButtonSwap : MonoBehaviour
{

    public List<GameObject> m_DesktopOnlyButtons;
    public List<GameObject> m_MobileOnlyButtons;
    public List<GameObject> m_WindowsOnlyButtons;

    void Start()
    {
        // Use App.Config rather than Application.isMobilePlatform so that the Editor's
        // "Spoof Mobile Hardware" option (Android build target only) is respected.
        bool isMobile = TiltBrush.App.Config.IsMobileHardware;
        bool isDesktop = !isMobile;
        bool isWindows = !isMobile &&
            (Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor);

        foreach (var btn in m_DesktopOnlyButtons)
        {
            btn.SetActive(isDesktop);
        }
        foreach (var btn in m_MobileOnlyButtons)
        {
            btn.SetActive(isMobile);
        }
        foreach (var btn in m_WindowsOnlyButtons)
        {
            btn.SetActive(isWindows);
        }
    }
}
