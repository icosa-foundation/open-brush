using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    public class SDFTexture : SDFObject
    {
        public int ID => m_rt.GetInstanceID();

        [SerializeField]
        private RenderTexture m_rt;
        public RenderTexture RT => m_rt;

        [SerializeField]
        protected SDFCombineType m_operation;

        [SerializeField]
        protected bool m_flip = false;

        protected override void TryRegister()
        {
            if (!m_rt)
                return;

            base.TryRegister();

            Group?.Register(this);
        }

        protected override void TryDeregister()
        {
            if (!m_rt)
                return;

            base.TryRegister();

            Group?.Deregister(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (Group && !Group.IsRegistered(this) && m_rt)
                TryRegister();
        }

        public override SDFGPUData GetSDFGPUData(int sampleStartIndex, int uvStartIndex = -1)
        {
            return new SDFGPUData
            {
                Type = 0,
                Data = new Vector4(m_rt.volumeDepth, sampleStartIndex, uvStartIndex),
                Transform = transform.worldToLocalMatrix,
                CombineType = (int)m_operation,
                Flip = m_flip ? -1 : 1,
                // MinBounds = m_rt.MinBounds,
                // MaxBounds = m_rt.MaxBounds,
                Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing)
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!m_rt)
                return;

            Handles.color = Color.white;
            Handles.matrix = transform.localToWorldMatrix;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawWireCube(Vector3.one * -.5f, Vector3.one*.5f);
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