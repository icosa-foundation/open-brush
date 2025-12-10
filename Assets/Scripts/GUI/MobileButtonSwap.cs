using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MobileButtonSwap : MonoBehaviour
{

    public GameObject m_LabsButtonDesktop;
    public GameObject m_LabsButtonMobile;

    void Start()
    {
        if (Application.isMobilePlatform)
        {
            m_LabsButtonDesktop.SetActive(false);
            m_LabsButtonMobile.SetActive(true);
        }
        else
        {
            m_LabsButtonDesktop.SetActive(true);
            m_LabsButtonMobile.SetActive(false);
        }
    }
}
