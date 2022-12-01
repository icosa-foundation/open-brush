using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    [ExecuteInEditMode]
    public class SDFPrimitive : SDFObject
    {
        [SerializeField]
        private SDFPrimitiveType m_type;
        public SDFPrimitiveType Type => m_type;

        [SerializeField]
        private Vector4 m_data = new Vector4(1f, 1f, 1f, 0f);

        [SerializeField]
        protected SDFCombineType m_operation;
        public SDFCombineType Operation => m_operation;

        [SerializeField]
        protected bool m_flip = false;

        protected override void TryDeregister()
        {
            base.TryDeregister();

            Group?.Deregister(this);
        }

        protected override void TryRegister()
        {
            base.TryDeregister();

            Group?.Register(this);
        }

        public Vector3 CubeBounds
        {
            get
            {
                if (m_type == SDFPrimitiveType.BoxFrame || m_type == SDFPrimitiveType.Cuboid)
                    return new Vector3(m_data.x, m_data.y, m_data.z);

                return Vector3.zero;
            }
        }

        public float SphereRadius
        {
            get
            {
                if (m_type == SDFPrimitiveType.Sphere)
                    return m_data.x;

                return 0f;
            }
        }

        public void SetCubeBounds(Vector3 vec)
        {
            if (m_type == SDFPrimitiveType.BoxFrame || m_type == SDFPrimitiveType.Cuboid)
            {
                m_data = new Vector4(vec.x, vec.y, vec.z, m_data.w);
                SetDirty();
            }
        }

        public void SetSphereRadius(float radius)
        {
            if (m_type == SDFPrimitiveType.Sphere)
            {
                m_data = m_data.SetX(Mathf.Max(0f, radius));
                SetDirty();
            }
        }

        public override SDFGPUData GetSDFGPUData(int sampleStartIndex = -1, int uvStartIndex = -1)
        {
            // note: has room for six more floats (minbounds, maxbounds)
            return new SDFGPUData
            {
                Type = (int)m_type + 1,
                Data = m_data,
                Transform = transform.worldToLocalMatrix,
                CombineType = (int)m_operation,
                Flip = m_flip ? -1 : 1,
                Smoothing = Mathf.Max(MIN_SMOOTHING, m_smoothing)
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color col = Operation == SDFCombineType.SmoothSubtract ? Color.red : Color.blue;
            Handles.color = col;
            Handles.matrix = transform.localToWorldMatrix;

            switch (Type)
            {
                case SDFPrimitiveType.BoxFrame:
                case SDFPrimitiveType.Cuboid:
                    Handles.DrawWireCube(Vector3.zero, m_data.XYZ() * 2f);
                    break;
                //case SDFPrimitiveType.BoxFrame:
                //    Handles.DrawWireCube(Vector3.zero, data.XYZ() * 2f);
                //    break;
                default:
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, m_data.x);
                    break;
            }
        }

#endif

        #region Create Menu Items

#if UNITY_EDITOR
        private static void CreateNewPrimitive(SDFPrimitiveType type, Vector4 startData)
        {
            GameObject selection = Selection.activeGameObject;

            GameObject child = new GameObject(type.ToString());
            child.transform.SetParent(selection.transform);
            child.transform.Reset();

            SDFPrimitive newPrimitive = child.AddComponent<SDFPrimitive>();
            newPrimitive.m_type = type;
            newPrimitive.m_data = startData;
            newPrimitive.SetDirty();

            Selection.activeGameObject = child;
        }

        [MenuItem("GameObject/SDFs/Sphere", false, priority: 2)]
        private static void CreateSphere(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Sphere, new Vector4(1f, 0f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Cuboid", false, priority: 2)]
        private static void CreateCuboid(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cuboid, new Vector4(1f, 1f, 1f, 0f));

        [MenuItem("GameObject/SDFs/Torus", false, priority: 2)]
        private static void CreateTorus(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Torus, new Vector4(1f, 0.5f, 0f, 0f));

        [MenuItem("GameObject/SDFs/Frame", false, priority: 2)]
        private static void CreateFrame(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.BoxFrame, new Vector4(1f, 1f, 1f, 0.2f));

        [MenuItem("GameObject/SDFs/Cylinder", false, priority: 2)]
        private static void CreateCylinder(MenuCommand menuCommand) => CreateNewPrimitive(SDFPrimitiveType.Cylinder, new Vector4(1f, 1f, 0f, 0f));

#endif
        #endregion
    }

    public enum SDFPrimitiveType
    {
        Sphere,
        Torus,
        Cuboid,
        BoxFrame, 
        Cylinder
    }
}