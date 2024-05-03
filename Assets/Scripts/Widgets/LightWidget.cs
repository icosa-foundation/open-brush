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
    public class LightWidget : MediaWidget
    {
        protected override void Awake()
        {
            base.Awake();
            transform.localScale = Vector3.one * Coords.CanvasPose.scale;
            m_ContainerBloat /= App.ActiveCanvas.Pose.scale;

            // Set a new batchId on this light so it can be picked up in GPU intersections.
            m_BatchId = GpuIntersector.GetNextBatchId();
            WidgetManager.m_Instance.AddWidgetToBatchMap(this, m_BatchId);
            Debug.Log($"Widget {name} assigned batchId {m_BatchId}");
        }

        public static List<LightWidget> FromModelWidget(ModelWidget modelWidget)
        {
            var go = modelWidget.gameObject;
            string baseName = go.name.Replace("ModelWidget", "LightWidget");
            go.SetActive(false);
            go.name = go.name.Replace("ModelWidget", "OldModelWidget");
            var lightWidgets = new List<LightWidget>();
            var layer = go.layer;
            foreach (var gizmo in go.GetComponentsInChildren<SceneLightGizmo>())
            {
                var tr = TrTransform.FromTransform(gizmo.transform);
                CreateWidgetCommand createCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.LightWidgetPrefab, tr, null, forceTransform: true
                );
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
                LightWidget lightWidget = createCommand.Widget as LightWidget;
                var light = gizmo.transform.parent.GetComponentInChildren<Light>();
                gizmo.SetupLightGizmos(light);
                lightWidget.m_BoxCollider = gizmo.GetComponentInChildren<BoxCollider>(includeInactive: false);
                lightWidget.m_Mesh = gizmo.ActiveMeshTransform;
                lightWidget.HighlightMeshXfs = new[] { gizmo.ActiveMeshTransform };
                light.transform.SetParent(lightWidget.transform, true);
                lightWidget.name = baseName;
                HierarchyUtils.RecursivelySetLayer(lightWidget.transform, layer, skipUI: false);
                WidgetManager.m_Instance.RegisterGrabWidget(lightWidget.gameObject);
                lightWidgets.Add(lightWidget);
            }
            modelWidget.Hide();
            WidgetManager.m_Instance.UnregisterGrabWidget(modelWidget.gameObject);
            Destroy(modelWidget.gameObject);
            return lightWidgets;
        }

        protected override void UpdateScale()
        {
            transform.localScale = Vector3.one * m_Size;
            m_BoxCollider.size = Vector3.one * m_Size;
        }

        public override string GetExportName()
        {
            return this.name;
        }

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            LightWidget clone = Instantiate(WidgetManager.m_Instance.LightWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            // We want to lie about our intro transition amount.
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);

            clone.transform.parent = App.Instance.m_CanvasTransform;
            clone.transform.localScale = Vector3.one;

            var light = clone.SetLightType(GetComponentInChildren<Light>().type);

            clone.Show(bShow: true, bPlayAudio: false);
            light.color = light.color;
            light.intensity = light.intensity;
            light.range = light.range;
            light.spotAngle = light.spotAngle;
            light.innerSpotAngle = light.innerSpotAngle;
            clone.SetPinned(Pinned);
            clone.Group = Group;
            var gizmo = Instantiate(WidgetManager.m_Instance.SceneLightGizmoPrefab, transform);
            gizmo.SetupLightGizmos(light);

            CanvasScript canvas = transform.parent.GetComponent<CanvasScript>();
            if (canvas != null)
            {
                var materials = clone.GetComponentsInChildren<Renderer>().SelectMany(x => x.materials);
                foreach (var material in materials)
                {
                    foreach (string keyword in canvas.BatchManager.MaterialKeywords)
                    {
                        material.EnableKeyword(keyword);
                    }
                }
            }

            return clone;
        }

        public static void FromTiltLight(TiltLights tiltLight)
        {
            LightWidget lightWidget = Instantiate(WidgetManager.m_Instance.LightWidgetPrefab);
            lightWidget.m_LoadingFromSketch = true;
            lightWidget.transform.parent = App.Instance.m_CanvasTransform;
            lightWidget.transform.localScale = Vector3.one;

            lightWidget.SetSignedWidgetSize(tiltLight.Transform.scale);
            lightWidget.Show(bShow: true, bPlayAudio: false);
            lightWidget.transform.localPosition = tiltLight.Transform.translation;
            lightWidget.transform.localRotation = tiltLight.Transform.rotation;
            var light = lightWidget.SetLightType(tiltLight.PunctualLightType);
            light.color = tiltLight.LightColor.Value;
            light.intensity = tiltLight.Intensity.Value;
            light.range = tiltLight.Range.Value;
            light.spotAngle = tiltLight.OuterConeAngle.Value;
            light.innerSpotAngle = tiltLight.InnerConeAngle.Value;
            if (tiltLight.Pinned)
            {
                lightWidget.PinFromSave();
            }
            lightWidget.Group = App.GroupManager.GetGroupFromId(tiltLight.GroupId);
            lightWidget.SetCanvas(App.Scene.GetOrCreateLayer(tiltLight.LayerId));
            var gizmo = Instantiate(WidgetManager.m_Instance.SceneLightGizmoPrefab, lightWidget.transform);
            gizmo.SetupLightGizmos(light);

            TiltMeterScript.m_Instance.AdjustMeterWithWidget(lightWidget.GetTiltMeterCost(), up: true);
            lightWidget.UpdateScale();
        }

        private Light SetLightType(LightType lightType)
        {
            var lights = gameObject.GetComponentsInChildren<Light>();
            Light activeLight = null;
            foreach (var l in lights)
            {
                l.gameObject.SetActive(false);
                if (l.type == lightType)
                {
                    l.gameObject.SetActive(true);
                    activeLight = l;
                }

            }
            return activeLight;
        }

        public TrTransform GetSaveTransform()
        {
            var xf = TrTransform.FromLocalTransform(transform);
            xf.scale = GetSignedWidgetSize();
            return xf;
        }
    }

} // namespace TiltBrush
