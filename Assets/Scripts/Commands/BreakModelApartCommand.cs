// Copyright 2024 The Open Brush Authors
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

    public class BreakModelApartCommand : BaseCommand
    {
        private ModelWidget m_InitialWidget;
        private List<ModelWidget> m_NewWidgets;
        private List<string> m_NodePaths;

        override public bool NeedsSave
        {
            get => true;
        }

        public BreakModelApartCommand(ModelWidget initialWidget, BaseCommand parent = null) : base(parent)
        {
            m_InitialWidget = initialWidget;
            m_NewWidgets = new List<ModelWidget>();

            m_NodePaths = new List<string>();
            var nodes = new List<Transform>();
            foreach (Transform child in initialWidget.GetComponentInChildren<ObjModelScript>().transform)
            {
                nodes.Add(child);
            }

            var nextNodes = new List<Transform>();
            var currentPath = "";
            int failsafe = 0;
            while (m_NodePaths.Count < 2 && failsafe < 1000)
            {
                foreach (var node in nodes)
                {
                    if (currentPath == "") currentPath = node.name;
                    else currentPath = $"{currentPath}/{node.name}";

                    foreach (Transform child in node)
                    {
                        nextNodes.Add(child);
                        if (child.GetComponent<MeshFilter>() != null)
                        {
                            m_NodePaths.Add($"{currentPath}/{child.name}");
                        }
                    }
                }
                nodes = nextNodes;
                nextNodes.Clear();
                failsafe++;
            }
        }

        protected override void OnRedo()
        {
            foreach (var path in m_NodePaths)
            {
                var newWidget = m_InitialWidget.Clone();
                var newModelWidget = newWidget as ModelWidget;
                if (newModelWidget == null) continue;
                newModelWidget.Subtree = path;
                newModelWidget.SyncHierarchyToSubtree();
                newWidget.SetCanvas(App.Scene.ActiveCanvas);
                SelectionManager.m_Instance.SelectWidget(newModelWidget);
                m_NewWidgets.Add(newModelWidget);
            }
            SelectionManager.m_Instance.DeselectWidgets(new List<GrabWidget>{m_InitialWidget});
            m_InitialWidget.gameObject.SetActive(false);
        }

        protected override void OnUndo()
        {
            SelectionManager.m_Instance.DeselectWidgets(m_NewWidgets);
            foreach (var widget in m_NewWidgets)
            {
                Object.Destroy(widget);
            }
            m_InitialWidget.gameObject.SetActive(true);
            SelectionManager.m_Instance.SelectWidget(m_InitialWidget);
        }
    }
} // namespace TiltBrush
