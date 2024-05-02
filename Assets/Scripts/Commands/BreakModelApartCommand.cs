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
        private List<ModelWidget> m_NewModelWidgets;
        private List<LightWidget> m_NewLightWidgets;
        private List<string> m_NodePaths;

        override public bool NeedsSave
        {
            get => true;
        }

        private static List<string> ExtractPaths(Transform root)
        {
            List<Transform> nodes = root.Cast<Transform>().ToList();
            var resultPaths = new List<string>();
            var nextNodes = new List<Transform>();
            var validButNotAdded = new List<Transform>();

            // Prevent infinite loops
            const int maxIterations = 1000;
            int failsafe = 0;

            bool isValidSelf(Transform node)
            {
                // Skip UI elements (SceneLightGizmo presently)
                if (node.gameObject.layer == LayerMask.NameToLayer("UI")) return false;
                if (!node.gameObject.activeSelf) return false;
                // Skip nodes with no valid children
                if (node.GetComponent<MeshFilter>() == null &&
                    node.GetComponent<Light>() == null) return false;
                return true;
            }

            bool isValid(Transform node)
            {
                // Skip UI elements (SceneLightGizmo presently)
                if (node.gameObject.layer == LayerMask.NameToLayer("UI")) return false;
                if (!node.gameObject.activeSelf) return false;
                // Skip nodes with no valid children
                if (node.GetComponentInChildren<MeshFilter>() == null &&
                    node.GetComponentInChildren<Light>() == null) return false;
                return true;
            }

            bool hasValidDirectChildren(Transform node)
            {
                if (node.gameObject.activeInHierarchy == false) return false;
                int count = 0;
                foreach (Transform child in node)
                {
                    if (!isValid(child)) continue;
                    count++;
                }
                return count > 0;
            }

            bool isSubPath(string basePath, string potentialSubPath)
            {
                var baseParts = basePath.Split('/');
                var subParts = potentialSubPath.Split('/');

                // If the potential subpath has more parts than the base path, it can't be a subpath
                if (subParts.Length > baseParts.Length)
                {
                    return false;
                }

                for (int i = 0; i < subParts.Length; i++)
                {
                    if (baseParts[i] != subParts[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            while (resultPaths.Count < 2 && failsafe < maxIterations)
            {

                validButNotAdded.Clear();

                foreach (var child in nodes)
                {
                    if (isValidSelf(child) && hasValidDirectChildren(child)) // Both a leaf and a valid node
                    {
                        resultPaths.Add(GetHierarchyPath(root, child));
                        // Add the children to the next level
                        nextNodes.AddRange(child.Cast<Transform>().ToList());
                    }
                    else if (isValidSelf(child)) // A leaf but not a valid node
                    {
                        resultPaths.Add(GetHierarchyPath(root, child));
                    }
                    else if (hasValidDirectChildren(child)) // Not valid but children are
                    {
                        validButNotAdded.Add(child);
                        nextNodes.AddRange(child.Cast<Transform>().ToList());
                    }
                }

                // Break if have no more levels
                if (nextNodes.Count == 0) break;

                // If we found no meshes, continue to the next level
                nodes = nextNodes.ToList(); // Clone the list

                nextNodes.Clear();
                failsafe++;
            }

            // If the while loop has decided we're done. there might still be some valid nodes that we haven't added
            foreach (var child in validButNotAdded)
            {
                resultPaths.Add(GetHierarchyPath(root, child));
            }

            // Add the mesh suffix to all nodes if their descendents are also in the list
            for (var i = 0; i < resultPaths.Count; i++)
            {
                var path = resultPaths[i];
                for (var j = 0; j < resultPaths.Count; j++)
                {
                    if (i == j) continue;
                    var pathToCompare = resultPaths[j];
                    if (isSubPath(pathToCompare, path))
                    {
                        resultPaths[i] = $"{path}.mesh";
                        break;
                    }
                }
            }


            return resultPaths;
        }

        public BreakModelApartCommand(ModelWidget initialWidget, BaseCommand parent = null) : base(parent)
        {
            m_InitialWidget = initialWidget;
            m_NewModelWidgets = new List<ModelWidget>();
            m_NewLightWidgets = new List<LightWidget>();
            var root = initialWidget.GetComponentInChildren<ObjModelScript>().transform;
            m_NodePaths = ExtractPaths(root);
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
                var newWidget = m_InitialWidget.Clone() as ModelWidget;
                if (newWidget == null) continue;
                var previousSubtree = newWidget.Subtree;
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
                newWidget.Subtree = newSubtree;
                newWidget.name = $"{newWidget.name} Subtree:{path}";
                newWidget.SyncHierarchyToSubtree(previousSubtree);
                newWidget.RegisterHighlight();
                newWidget.UpdateBatchInfo();

                // If a ModelWidget contains no more meshes
                // then try and convert it to multiple LightWidgets
                var objModel = newWidget.GetComponentInChildren<ObjModelScript>();
                if (objModel != null && objModel.NumMeshes == 0)
                {
                    m_NewLightWidgets.AddRange(LightWidget.FromModelWidget(newWidget));
                }
                else
                {
                    SelectionManager.m_Instance.SelectWidget(newWidget);
                    m_NewModelWidgets.Add(newWidget);
                }
            }
            m_InitialWidget.gameObject.SetActive(false);
        }

        protected override void OnUndo()
        {
            SelectionManager.m_Instance.DeselectWidgets(m_NewModelWidgets);
            foreach (var widget in m_NewModelWidgets)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(widget.gameObject);
                Object.Destroy(widget.gameObject);
            }
            SelectionManager.m_Instance.DeselectWidgets(m_NewLightWidgets);
            foreach (var widget in m_NewLightWidgets)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(widget.gameObject);
                Object.Destroy(widget.gameObject);
            }
            m_InitialWidget.gameObject.SetActive(true);
            SelectionManager.m_Instance.SelectWidget(m_InitialWidget);
        }
    }
} // namespace TiltBrush
