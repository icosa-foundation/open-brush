using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush {
public class SculptSubToolManager : MonoBehaviour {

    public static SculptSubToolManager m_Instance;

    private List<BaseSculptSubTool> m_SubTools;
    
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
        m_SubTools = new List<BaseSculptSubTool>();
        foreach (Transform child in transform) {
            m_SubTools.Add(child.gameObject.GetComponent<BaseSculptSubTool>());
        }
    }
    
    public void SetSubTool(SubTool subTool) {
        m_SculptTool.SetSubTool(m_SubTools[(int) subTool]);
    }
}
} // namespace TiltBrush

