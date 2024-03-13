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

        private static List<string> ExtractPathsForNextLevel(Transform root)
        {
            var nodes = new List<Transform>();
            var resultNodes = new List<Transform>();
            var resultPaths = new List<string>();

            // We should only have a single child
            var firstChild = root.GetChild(0);
            nodes.Add(firstChild);

            var nextNodes = new List<Transform>();
            int failsafe = 0;

            while (
                (
                    // Skip levels with no child meshes
                    resultPaths.Count < 1 ||
                    // Skip levels with a single non-mesh node
                    (resultPaths.Count == 1 && resultNodes[0].GetComponent<MeshFilter>() == null)
                )
                && failsafe < 1000)
            {
                resultNodes.Clear();
                resultPaths.Clear();
                foreach (var node in nodes)
                {
                    foreach (Transform child in node)
                    {
                        if (!child.gameObject.activeSelf) continue;
                        nextNodes.Add(child);
                        bool hasMeshChildren = child.GetComponentInChildren<MeshFilter>() != null;
                        if (hasMeshChildren)
                        {
                            resultNodes.Add(child);
                            string path = GetHierarchyPath(root, child);
                            resultPaths.Add(path);
                        }
                    }
                }

                // If we found no meshes, continue to the next level
                nodes = nextNodes.ToList(); // Clone the list
                nextNodes.Clear();
                failsafe++;
            }

            // Special case for leaf nodes that also contain a mesh
            if (firstChild.GetComponent<MeshFilter>() && firstChild.childCount > 0)
            {
                string path = GetHierarchyPath(root, firstChild);
                resultPaths.Add($"{path}.mesh");
            }
            return resultPaths;
        }

        public List<Transform> GetDirectMeshChildren(Transform root)
        {
            var nodes = new List<Transform>();
            foreach (Transform child in root)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.GetComponentInChildren<MeshFilter>() != null)
                {
                    nodes.Add(child);
                }
            }
            return nodes;
        }

        public BreakModelApartCommand(ModelWidget initialWidget, BaseCommand parent = null) : base(parent)
        {
            m_InitialWidget = initialWidget;
            m_NewWidgets = new List<ModelWidget>();
            var root = initialWidget.GetComponentInChildren<ObjModelScript>().transform;
            var meshChildren = GetDirectMeshChildren(root);
            if (meshChildren.Count > 1)
            {
                // Shouldn't happen?
                Debug.LogWarning($"Attempting to break apart mesh with {meshChildren} children");
            }
            else if (meshChildren.Count == 1)
            {
                m_NodePaths = ExtractPathsForNextLevel(root);
            }
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
            SelectionManager.m_Instance.DeselectWidgets(new List<GrabWidget> { m_InitialWidget });
            foreach (var path in m_NodePaths)
            {
                var newWidget = m_InitialWidget.Clone();
                var newModelWidget = newWidget as ModelWidget;
                if (newModelWidget == null) continue;
                var previousSubtree = newModelWidget.Subtree;
                string newSubtree;
                if (string.IsNullOrEmpty(previousSubtree))
                {
                    newSubtree = path;
                }
                else
                {
                    // Join the previous subtree with the new path
                    // Remove the duplicate last part of the previous subtree
                    var parts = previousSubtree.Split('/');
                    newSubtree = string.Join('/', parts.Take(parts.Length - 1)) + path;
                }
                newModelWidget.Subtree = newSubtree;
                newModelWidget.SyncHierarchyToSubtree(previousSubtree);
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
