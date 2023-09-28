using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class AnchorToGuide : MonoBehaviour
    {
        // Start is called before the first frame update

        private OVRSceneVolume m_SceneComponentVolume;
        private OVRScenePlane m_SceneComponentPlane;
        private OVRSemanticClassification m_Classification;

        void Start()
        {
            m_SceneComponentVolume = GetComponent<OVRSceneVolume>();
            
            if(m_SceneComponentVolume)
            {
                var dimentions = m_SceneComponentVolume.Dimensions;
                
                var pos = App.Scene.transform.InverseTransformPoint(this.transform.position);
                pos.y /= 2.0f;
                var tr = TrTransform.TR(pos, this.transform.rotation);
                
                CreateWidgetCommand createCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.GetStencilPrefab(StencilType.Cube), tr, null, true);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);

                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(createCommand.Widget, tr, dimentions * 10));

                return;
            }

            // Hacky but quick proof of concept
            // TODO: Tidyup
            m_SceneComponentPlane = GetComponent<OVRScenePlane>();

            if(m_SceneComponentPlane)
            {
                var dimentions = m_SceneComponentPlane.Dimensions;
                
                var tr = TrTransform.TR(this.transform.position, this.transform.rotation);
                
                CreateWidgetCommand createCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.GetStencilPrefab(StencilType.Plane), tr, null, true);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);

                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(createCommand.Widget, tr, dimentions * 10));

                return;
            }
        }
    }

}
