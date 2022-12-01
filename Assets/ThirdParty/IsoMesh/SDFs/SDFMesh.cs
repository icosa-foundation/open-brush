using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    public class SDFMesh : SDFObject
    {
        public int ID => m_asset.GetInstanceID();

        [SerializeField]
        private SDFMeshAsset m_asset;
        public SDFMeshAsset Asset => m_asset;

        [SerializeField]
        protected SDFCombineType m_operation;

        [SerializeField]
        protected bool m_flip = false;

        protected override void TryRegister()
        {
            if (!m_asset)
                return;

            base.TryRegister();

            Group?.Register(this);
        }

        protected override void TryDeregister()
        {
            if (!m_asset)
                return;

            base.TryRegister();

            Group?.Deregister(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (Group && !Group.IsRegistered(this) && m_asset)
                TryRegister();
        }

        public override SDFGPUData GetSDFGPUData(int sampleStartIndex, int uvStartIndex = -1)
        {
            return new SDFGPUData
            {
                Type = 0,
                Data = new Vector4(m_asset.Size, sampleStartIndex, uvStartIndex),
                Transform = transform.worldToLocalMatrix,
                CombineType = (int)m_operation,
                Flip = m_flip ? -1 : 1,
                MinBounds = m_asset.MinBounds,
                MaxBounds = m_asset.MaxBounds,
                Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing)
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!m_asset)
                return;

            Handles.color = Color.white;
            Handles.matrix = transform.localToWorldMatrix;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawWireCube((m_asset.MaxBounds + m_asset.MinBounds) * 0.5f, (m_asset.MaxBounds - m_asset.MinBounds));
        }
#endif

        #region Create Menu Items

        [MenuItem("GameObject/SDFs/Mesh", false, priority: 2)]
        private static void CreateSDFMesh(MenuCommand menuCommand)
        {
            GameObject selection = Selection.activeGameObject;

            GameObject child = new GameObject("Mesh");
            child.transform.SetParent(selection.transform);
            child.transform.Reset();

            SDFMesh newMesh = child.AddComponent<SDFMesh>();

            Selection.activeGameObject = child;
        }

        #endregion

    }
}