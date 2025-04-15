using UnityEngine;

public class CanvasTransformGizmo : MonoBehaviour
{
    public Color[] m_AxesColors;

    public GameObject[] m_TranslationAxes;
    public GameObject[] m_RotationAxes;
    public GameObject[] m_ScaleAxes;

    public Transform TranslationGizmo;
    public Transform RotationGizmo;
    public Transform ScaleGizmo;

    public Transform BoundsGhost;

    private void Awake()
    {
        SetAxisColors();
    }

    public void SetBoundsGhost(Bounds bounds_CS)
    {
        BoundsGhost.localScale = bounds_CS.size;
    }

    public void SetAxisColors()
    {
        for (int i = 0; i < Mathf.Min(m_AxesColors.Length, m_AxesColors.Length); ++i)
        {
            foreach (var mr in m_TranslationAxes[i].GetComponentsInChildren<Renderer>())
            {
                mr.material.SetColor("_Color", m_AxesColors[i]);
            }
            foreach (var mr in m_RotationAxes[i].GetComponentsInChildren<Renderer>())
            {
                mr.material.SetColor("_Color", m_AxesColors[i]);
            }
            foreach (var mr in m_ScaleAxes[i].GetComponentsInChildren<Renderer>())
            {
                mr.material.SetColor("_Color", m_AxesColors[i]);
            }
        }
    }
}
