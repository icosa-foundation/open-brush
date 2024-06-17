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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class GltfExportStandinManager : MonoBehaviour
    {
        [SerializeField] public Transform m_TemporarySkySphere;
        [NonSerialized] public static GltfExportStandinManager m_Instance;
        private List<GameObject> m_TemporaryCameras;

        void Awake()
        {
            m_Instance = this;
        }

        public void CreateCameraStandins()
        {
            m_TemporaryCameras = new List<GameObject>();
            var cameraPathWidgets = WidgetManager.m_Instance.CameraPathWidgets.ToArray();
            foreach (var widget in cameraPathWidgets)
            {
                var layer = widget.m_WidgetScript.Canvas;
                var cam = Instantiate(new GameObject(), layer.transform);
                cam.AddComponent<Camera>();
                AnimatorOverrideController overrideController = new AnimatorOverrideController();
                Animator animator = cam.AddComponent<Animator>();
                overrideController.runtimeAnimatorController = animator.runtimeAnimatorController;
                animator.runtimeAnimatorController = overrideController;

                var clip = new AnimationClip {frameRate = 90}; // TODO What's the correct value?
                var posCurves = new AnimationCurve[] { new (), new(), new() };
                var rotCurves = new AnimationCurve[] { new (), new(), new(), new() };
                foreach (var knot in widget.WidgetScript.Path.PositionKnots)
                {
                    var xf = knot.KnotXf;
                    var t = knot.PathT.T;
                    posCurves[0].AddKey(t, xf.position.x);
                    posCurves[1].AddKey(t, xf.position.y);
                    posCurves[2].AddKey(t, xf.position.z);
                }
                clip.SetCurve("", typeof(Transform), "localPosition.x", posCurves[0]);
                clip.SetCurve("", typeof(Transform), "localPosition.y", posCurves[1]);
                clip.SetCurve("", typeof(Transform), "localPosition.z", posCurves[2]);
                foreach (var knot in widget.WidgetScript.Path.RotationKnots)
                {
                    var xf = knot.KnotXf;
                    var t = knot.PathT.T;
                    rotCurves[0].AddKey(t, xf.rotation.x);
                    rotCurves[1].AddKey(t, xf.rotation.y);
                    rotCurves[2].AddKey(t, xf.rotation.z);
                    rotCurves[3].AddKey(t, xf.rotation.w);
                }
                clip.SetCurve("", typeof(Transform), "localRotation.x", posCurves[0]);
                clip.SetCurve("", typeof(Transform), "localRotation.y", posCurves[1]);
                clip.SetCurve("", typeof(Transform), "localRotation.z", posCurves[2]);
                clip.SetCurve("", typeof(Transform), "localRotation.w", posCurves[3]);

                overrideController["DefaultAnimation"] = clip;
                m_TemporaryCameras.Add(cam);
            }
        }

        public void DestroyCameraStandins()
        {
            foreach (var cam in m_TemporaryCameras)
            {
                Destroy(cam);
            }
        }

        public void CreateSkyStandin()
        {
            // TODO check if we need box vs sphere etc
            m_TemporarySkySphere.GetComponent<MeshRenderer>().material = RenderSettings.skybox;
            m_TemporarySkySphere.gameObject.SetActive(false);
        }

        public void DestroySkyStandin()
        {
            m_TemporarySkySphere.gameObject.SetActive(false);
        }
    }
}

