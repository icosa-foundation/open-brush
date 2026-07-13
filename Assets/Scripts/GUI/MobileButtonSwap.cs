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
        bool isMobile = Application.isMobilePlatform;
        bool isDesktop = !Application.isMobilePlatform;
        bool isWindows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

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
