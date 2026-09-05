// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;
using System.Collections.Generic;
using Unity.VectorGraphics.OpenBrush;

namespace TiltBrush
{

    public class ObjModelScript : MonoBehaviour
    {
        private int m_NumVertsInMeshes = -1;

        // This must be public -- or at least serialized -- or it won't survive
        // past an Instantiate() operation. It contains all MeshFilters in this
        // GameObject tree such that:
        // - The mesh will be visible when the tree root is enabled
        // More specificially, this means:
        // - MeshFilter exists, and its .mesh is non-null
        // - MeshRenderer exists
        // - root.activeInHierarchy  implies  gameObject.activeInHierarchy
        public MeshFilter[] m_MeshChildren;
        public SkinnedMeshRenderer[] m_SkinnedMeshChildren;

        public int NumMeshes
        {
            get { return MeshChildren.Length + SkinnedMeshChildren.Length; }
        }
        public SVGParser.SceneInfo SvgSceneInfo { get; set; }

        private MeshFilter[] MeshChildren => m_MeshChildren ?? new MeshFilter[0];
        private SkinnedMeshRenderer[] SkinnedMeshChildren =>
            m_SkinnedMeshChildren ?? new SkinnedMeshRenderer[0];

        public int GetNumVertsInMeshes()
        {
            if (m_NumVertsInMeshes <= 0)
            {
                MeshFilter[] meshChildren = MeshChildren;
                SkinnedMeshRenderer[] skinnedMeshChildren = SkinnedMeshChildren;
                for (int i = 0; i < meshChildren.Length; ++i)
                {
                    m_NumVertsInMeshes += meshChildren[i].sharedMesh.vertexCount;
                }
                for (int i = 0; i < skinnedMeshChildren.Length; ++i)
                {
                    m_NumVertsInMeshes += skinnedMeshChildren[i].sharedMesh.vertexCount;
                }

                m_NumVertsInMeshes = Mathf.Max(1,
                    (int)(m_NumVertsInMeshes * WidgetManager.m_Instance.ModelVertCountScalar));
            }
            return m_NumVertsInMeshes;
        }

        private static void GetAllMeshes(List<MeshFilter> filters, List<SkinnedMeshRenderer> smrs, Transform t, bool isRoot)
        {
            // Only return meshes that will be visible when the hierarchy root is enabled
            if (!isRoot && !t.gameObject.activeSelf)
            {
                // Prune this whole section of the tree.
                // This case should be unexpected, but currently ModelWidget inserts SnapGhost
                // objects into our tree (which it should not; they should be kept separate),
                // and it calls Init() on us (which it should not, because we're already initialized).
                // TODO: re-enable this warning when those things are fixed.
                // Debug.LogWarningFormat("Unexpected: sub-object {0} is disabled", t);
                return;
            }

            var meshFilter = t.GetComponent<MeshFilter>();
            var meshRenderer = t.GetComponent<MeshRenderer>();
            if (meshFilter != null &&
                meshRenderer != null &&
                meshFilter.sharedMesh != null &&
                meshFilter.gameObject.layer != LayerMask.NameToLayer("UI"))
            {
                filters.Add(meshFilter);
            }

            var smr = t.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null) smrs.Add(smr);
            foreach (Transform child in t) GetAllMeshes(filters, smrs, child, isRoot: false);
        }

        public void UpdateAllMeshChildren()
        {
            var filters = new List<MeshFilter>();
            var smrs = new List<SkinnedMeshRenderer>();
            GetAllMeshes(filters, smrs, transform, isRoot: true);
            m_MeshChildren = filters.ToArray();
            m_SkinnedMeshChildren = smrs.ToArray();
        }

        void Awake()
        {
            // ObjReader used to destroy sub-objects even after Init().
            // We don't use ObjReader, so that behavior is probably no longer relevant; verify.
            if (m_MeshChildren != null)
            {
                foreach (var mf in m_MeshChildren)
                {
                    Debug.Assert(mf != null);
                }
            }
            if (m_SkinnedMeshChildren != null)
            {
                foreach (var sm in m_SkinnedMeshChildren)
                {
                    Debug.Assert(sm != null);
                }
            }
        }

        public void RegisterHighlight()
        {
            MeshFilter[] meshChildren = MeshChildren;
            SkinnedMeshRenderer[] skinnedMeshChildren = SkinnedMeshChildren;
            for (int i = 0; i < meshChildren.Length; i++)
            {
                App.Instance.SelectionEffect.RegisterMesh(meshChildren[i]);
            }
            for (int i = 0; i < skinnedMeshChildren.Length; i++)
            {
                App.Instance.SelectionEffect.RegisterMesh(skinnedMeshChildren[i]);
            }
        }

        public void UnregisterHighlight()
        {
            MeshFilter[] meshChildren = MeshChildren;
            SkinnedMeshRenderer[] skinnedMeshChildren = SkinnedMeshChildren;
            for (int i = 0; i < meshChildren.Length; i++)
            {
                App.Instance.SelectionEffect.UnregisterMesh(meshChildren[i]);
            }
            for (int i = 0; i < skinnedMeshChildren.Length; i++)
            {
                App.Instance.SelectionEffect.UnregisterMesh(skinnedMeshChildren[i]);
            }
        }

    }
} // namespace TiltBrush
