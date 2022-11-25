using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


namespace TiltBrush
{
    public class SculptSubToolManager : MonoBehaviour
    {

        public static SculptSubToolManager m_Instance;

        private List<BaseSculptSubTool> m_SubTools;

        [FormerlySerializedAs("m_SculptTool")]
        [SerializeField]
        private PushPullTool m_PushPullTool;

        /// Do not change the order of these items
        public enum SubTool
        {
            Push,
            Crease,
            Flatten,
            Rotate
        }

        private void Awake()
        {
            m_Instance = this;
            m_SubTools = new List<BaseSculptSubTool>();
            foreach (Transform child in transform)
                m_SubTools.Add(child.gameObject.GetComponent<BaseSculptSubTool>());
        }

        public void SetSubTool(SubTool subTool)
        {
            m_PushPullTool.SetSubTool(m_SubTools[(int)subTool]);
        }
    }
} // namespace TiltBrush
