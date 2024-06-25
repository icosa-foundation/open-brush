// Copyright 2023 The Open Brush Authors
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class AnchorToGuide : MonoBehaviour
    {
        // Start is called before the first frame update
#if OCULUS_SUPPORTED
        private OVRSceneVolume m_SceneComponentVolume;
        private OVRScenePlane m_SceneComponentPlane;
        private OVRSemanticClassification m_Classification;

        void Start()
        {
            m_SceneComponentVolume = GetComponent<OVRSceneVolume>();

            if (m_SceneComponentVolume)
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
            // TODO: Tidyup, seems a bit broken right now.
            // You end up stuck grabbin the wall planes.
            // m_SceneComponentPlane = GetComponent<OVRScenePlane>();

            // if(m_SceneComponentPlane)
            // {
            //     var dimentions = m_SceneComponentPlane.Dimensions;

            //     var tr = TrTransform.TR(this.transform.position, this.transform.rotation);

            //     CreateWidgetCommand createCommand = new CreateWidgetCommand(
            //         WidgetManager.m_Instance.GetStencilPrefab(StencilType.Plane), tr, null, true);
            //     SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);

            //     SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            //         new MoveWidgetCommand(createCommand.Widget, tr, dimentions * 10));

            //     return;
            // }
        }
#endif // OCULUS_SUPPORTED
    }

}
