using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush {
public class SculptSubtoolManager : MonoBehaviour {

    public static SculptSubtoolManager m_Instance;

    private List<BaseSculptSubtool> m_Subtools;
    
    [SerializeField]
    private SculptTool m_SculptTool;

    /// Do not
    public enum SubTool {
        Push,
        Crease,
        Flatten,
    }

    void Awake() {
        m_Instance = this;
        m_Subtools = new List<BaseSculptSubtool>();
        foreach (Transform child in transform) {
            m_Subtools.Add(child.gameObject.GetComponent<BaseSculptSubtool>());
        }
    }
    
    public void SetSubtool(SubTool subTool) {
        m_SculptTool.SetSubtool(m_Subtools[(int) subTool]);
    }
}
} // namespace TiltBrush

