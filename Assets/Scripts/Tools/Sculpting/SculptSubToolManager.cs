// Copyright 2022 Chingiz Dadashov-Khandan
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush
{
    public class SculptSubToolManager : MonoBehaviour
    {

        public static SculptSubToolManager m_Instance;

        private List<BaseSculptSubTool> m_SubTools;

        [SerializeField]
        private SculptTool m_SculptTool;

        /// Do not change the order of these items
        public enum SubTool
        {
            Push,
            Crease,
            Flatten,
            Rotate,
        }

        private SubTool m_ActiveSubtool = SubTool.Push;

        void Awake()
        {
            m_Instance = this;
            m_SubTools = new List<BaseSculptSubTool>();
            foreach (Transform child in transform)
            {
                m_SubTools.Add(child.gameObject.GetComponent<BaseSculptSubTool>());
            }
        }

        public SubTool GetActiveSubtool()
        {
            return m_ActiveSubtool;
        }

        public void SetSubTool(SubTool subTool)
        {
            m_ActiveSubtool = subTool;
            m_SculptTool.SetSubTool(m_SubTools[(int)subTool]);
        }
    }
} // namespace TiltBrush

