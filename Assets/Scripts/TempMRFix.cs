using System;
using TiltBrush;
using UnityEngine;

public class TempMRFix : MonoBehaviour
{
    private MeshRenderer[] m_MeshRenderers;

    void Start()
    {
        m_MeshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
    }

    private void Update()
    {
        var pos = gameObject.transform.position;
        gameObject.transform.position = new Vector3(pos.x, App.Scene.Pose.translation.y, pos.z);
        var rot = gameObject.transform.position;
        gameObject.transform.rotation = Quaternion.Euler(0, rot.y, 0);
    }

    void OnEnable()
    {
        for (int i = 0; i < m_MeshRenderers.Length; i++)
        {
            m_MeshRenderers[i].enabled = true;
        }
    }
}
