using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IsoMesh
{
    [ExecuteInEditMode]
    public abstract class SDFObject : MonoBehaviour
    {
        protected const float MIN_SMOOTHING = 0.000000001f;

        [SerializeField]
        [ReadOnly]
        private SDFGroup m_sdfGroup;
        public SDFGroup Group
        {
            get
            {
                if (!m_sdfGroup)
                    m_sdfGroup = GetComponentInParent<SDFGroup>();

                return m_sdfGroup;
            }
        }

        [SerializeField]
        private SDFMaterial m_material = new SDFMaterial(Color.white, Color.black, 0.5f, 0.5f, Color.black, 0f, 0.1f);
        public SDFMaterial Material => m_material;

        [SerializeField]
        protected float m_smoothing = MIN_SMOOTHING;
        
        protected bool m_isDirty = false;
        private bool m_isOrderDirty = false;

        private int m_lastSeenSiblingIndex = -1;

        public bool IsDirty => m_isDirty;
        public bool IsOrderDirty => m_isOrderDirty;

        protected virtual void Awake() => TryRegister();
        protected virtual void Reset() => TryRegister();
        protected virtual void OnEnable() => TryRegister();

        protected virtual void OnDisable() => TryDeregister();
        protected virtual void OnDestroy() => TryDeregister();

        protected virtual void OnValidate() => SetDirty();

        protected virtual void TryDeregister()
        {
            m_sdfGroup = GetComponentInParent<SDFGroup>();
            SetClean();
        }

        protected virtual void TryRegister()
        {
            m_lastSeenSiblingIndex = transform.GetSiblingIndex();

            m_sdfGroup = GetComponentInParent<SDFGroup>();
            SetDirty();
        }

        public abstract SDFGPUData GetSDFGPUData(int sampleStartIndex = -1, int uvStartIndex = -1);
        public SDFMaterialGPU GetMaterial() => new SDFMaterialGPU(m_material);

        protected void SetDirty() => m_isDirty = true;

        public void SetClean()
        {
            m_isDirty = false;
            transform.hasChanged = false;
        }

        public void SetOrderClean()
        {
            m_isOrderDirty = false;
        }

        protected virtual void Update()
        {
            m_isDirty |= transform.hasChanged;

            int siblingIndex = transform.GetSiblingIndex();

            if (siblingIndex != m_lastSeenSiblingIndex)
            {
                if (m_lastSeenSiblingIndex != -1)
                    m_isOrderDirty = true;
           
                m_lastSeenSiblingIndex = siblingIndex;
            }

            transform.hasChanged = false;
        }
    }


    public enum SDFCombineType
    {
        SmoothUnion,
        SmoothSubtract,
        SmoothIntersect
    }
}