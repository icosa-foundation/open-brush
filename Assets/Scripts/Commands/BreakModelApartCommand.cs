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
using System.Linq;
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

        private static List<Transform> ExtractNextLevel(Transform startNode)
        {
            var nodes = new List<Transform>();
            var results = new List<Transform>();
            foreach (Transform child in startNode)
            {
                if (!child.gameObject.activeSelf) continue;
                nodes.Add(child);
            }

            var nextNodes = new List<Transform>();
            int failsafe = 0;
            while (
                (
                    // Skip levels with no child meshes
                    results.Count < 1 ||
                    // Skip levels with a single non-mesh node
                    (results.Count == 1 && results[0].GetComponent<MeshFilter>() == null)
                )
                && failsafe < 1000)
            {
                results.Clear();
                foreach (var node in nodes)
                {
                    foreach (Transform child in node)
                    {
                        if (!child.gameObject.activeSelf) continue;
                        nextNodes.Add(child);
                        bool hasMeshChildren = child.GetComponentInChildren<MeshFilter>() != null;
                        if (hasMeshChildren)
                        {
                            results.Add(child);
                        }
                    }
                }
                nodes = nextNodes.ToList(); // Clone the list
                nextNodes.Clear();
                failsafe++;
            }
            return results;
        }

        public BreakModelApartCommand(ModelWidget initialWidget, BaseCommand parent = null) : base(parent)
        {
            m_InitialWidget = initialWidget;
            m_NewWidgets = new List<ModelWidget>();
            List<Transform> results = null;
            var root = initialWidget.GetComponentInChildren<ObjModelScript>().transform;
            results = ExtractNextLevel(root);
            m_NodePaths = results.Select(x => GetHierarchyPath(root, x)).ToList();
        }

        private static string GetHierarchyPath(Transform root, Transform obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != root)
            {
                obj = obj.transform.parent;
                path = "/" + obj.name + path;
            }
            return path;
        }

        protected override void OnRedo()
        {
            SelectionManager.m_Instance.DeselectWidgets(new List<GrabWidget>{m_InitialWidget});
            foreach (var path in m_NodePaths)
            {
                var newWidget = m_InitialWidget.Clone();
                var newModelWidget = newWidget as ModelWidget;
                if (newModelWidget == null) continue;
                newModelWidget.Subtree = path;
                newModelWidget.SyncHierarchyToSubtree();
                SelectionManager.m_Instance.SelectWidget(newModelWidget);
                m_NewWidgets.Add(newModelWidget);
            }
            m_InitialWidget.gameObject.SetActive(false);
        }

        protected override void OnUndo()
        {
            SelectionManager.m_Instance.DeselectWidgets(m_NewWidgets);
            foreach (var widget in m_NewWidgets)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(widget.gameObject);
                Object.Destroy(widget.gameObject);
            }
            m_InitialWidget.gameObject.SetActive(true);
            SelectionManager.m_Instance.SelectWidget(m_InitialWidget);
        }
    }
} // namespace TiltBrush
