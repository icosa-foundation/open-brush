using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush
{
    public class SculptSubToolManager : MonoBehaviour
    {

        public static SculptSubToolManager m_Instance;

        private Dictionary<SubTool, BaseSculptSubTool> m_SubTools;

        [SerializeField]
        private PushPullTool m_PushPullTool;

        // These explicit values preserve existing serialized prefab values.
        public enum SubTool
        {
            Push = 0,
            Pinch = 1,
            Flatten = 2,
            Twist = 3,
            Grab = 4,
            Smooth = 5
        }

        private void Awake()
        {
            m_Instance = this;
            m_SubTools = new Dictionary<SubTool, BaseSculptSubTool>();
            foreach (Transform child in transform)
            {
                BaseSculptSubTool subTool = child.GetComponent<BaseSculptSubTool>();
                if (subTool == null)
                {
                    continue;
                }

                SubTool identifier = subTool.SubToolIdentifier;
                if (m_SubTools.ContainsKey(identifier))
                {
                    Debug.LogError($"Multiple reshape subtools use identifier {identifier}.", child);
                    continue;
                }
                m_SubTools.Add(identifier, subTool);
            }
        }

        public void SetSubTool(SubTool subTool)
        {
            if (m_SubTools.TryGetValue(subTool, out BaseSculptSubTool selectedSubTool))
            {
                m_PushPullTool.SetSubTool(selectedSubTool);
            }
            else
            {
                Debug.LogError($"No reshape subtool is registered for {subTool}.", this);
            }
        }

        public SubTool ActiveSubTool => m_PushPullTool.m_ActiveSubTool.SubToolIdentifier;
    }
} // namespace TiltBrush
