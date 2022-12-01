using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    public class SDFOperation : SDFObject
    {
        //[SerializeField]
        //private SDFOperationType m_type;
        public SDFOperationType Type => SDFOperationType.Elongate;

        [SerializeField]
        private Vector4 m_data = new Vector4(0f, 0f, 0f, 0f);
        public Vector4 Data => m_data;

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

        public override SDFGPUData GetSDFGPUData(int sampleStartIndex = -1, int uvStartIndex = -1)
        {
            Vector4 data = m_data;

            if (Type == SDFOperationType.Elongate)
                data = new Vector4(Mathf.Max(0, data.x), Mathf.Max(0, data.y), Mathf.Max(0, data.z), Mathf.Max(0, data.w));

            return new SDFGPUData
            {
                Type = -(int)Type - 1,
                Transform = transform.worldToLocalMatrix,
                Data = data
            };
        }

        public void SetData(Vector4 data)
        {
            m_data = data;
            SetDirty();
        }

        #region Create Menu Items

#if UNITY_EDITOR
        private static void CreateNewOperation(SDFOperationType type)
        {
            GameObject selection = Selection.activeGameObject;

            GameObject child = new GameObject(type.ToString());
            child.transform.SetParent(selection.transform);
            child.transform.Reset();

            SDFOperation newPrimitive = child.AddComponent<SDFOperation>();
            //newPrimitive.m_type = type;

            Selection.activeGameObject = child;
        }

        [MenuItem("GameObject/SDFs/Operation/Elongate", false, priority: 2)]
        private static void CreateElongateOperation(MenuCommand menuCommand) => CreateNewOperation(SDFOperationType.Elongate);

        //[MenuItem("GameObject/SDFs/Operation/Round", false, priority: 2)]
        //private static void CreateRoundOperation(MenuCommand menuCommand) => CreateNewOperation(SDFOperationType.Round);

        //[MenuItem("GameObject/SDFs/Operation/Onion", false, priority: 2)]
        //private static void CreateOnionOperation(MenuCommand menuCommand) => CreateNewOperation(SDFOperationType.Onion);
#endif

        #endregion
    }

    public enum SDFOperationType
    {
        Elongate,
        Round,
        Onion
    }
}