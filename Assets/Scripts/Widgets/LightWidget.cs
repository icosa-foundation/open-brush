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
        }

        public static List<LightWidget> FromModelWidget(
            ModelWidget modelWidget, bool destroyModelWidget = true)
        {
            var go = modelWidget.gameObject;
            string baseName = go.name.Replace("ModelWidget", "LightWidget");
            if (destroyModelWidget)
            {
                go.SetActive(false);
                go.name = go.name.Replace("ModelWidget", "OldModelWidget");
            }
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
                foreach (var placeholder in lightWidget.GetComponentsInChildren<Light>(true))
                {
                    placeholder.gameObject.SetActive(false);
                    Destroy(placeholder.gameObject);
                }
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
            if (destroyModelWidget)
            {
                modelWidget.Hide();
                WidgetManager.m_Instance.UnregisterGrabWidget(modelWidget.gameObject);
                Destroy(modelWidget.gameObject);
            }
            return lightWidgets;
        }

        protected override void UpdateScale()
        {
            transform.localScale = Vector3.one * m_Size;
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
            clone.m_LoadingFromSketch = true;
            clone.transform.parent = transform.parent;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            // We want to lie about our intro transition amount.
            clone.m_ShowTimer = clone.m_ShowDuration;
            var sourceLight = GetComponentInChildren<Light>();
            var light = clone.SetLightType(sourceLight.type);
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            clone.SetSignedWidgetSize(size);

            clone.Show(bShow: true, bPlayAudio: false);
            light.color = sourceLight.color;
            light.intensity = sourceLight.intensity;
            light.range = sourceLight.range;
            light.spotAngle = sourceLight.spotAngle;
            light.innerSpotAngle = sourceLight.innerSpotAngle;
            light.enabled = sourceLight.enabled;
            light.shadows = sourceLight.shadows;
            clone.SetPinned(Pinned);
            clone.Group = Group;
            clone.RefreshLightGizmo();

            clone.TrySetCanvasKeywordsFromObject(transform);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);

            return clone;
        }

        public static void FromTiltLight(TiltLights tiltLight)
        {
            LightWidget lightWidget = Instantiate(WidgetManager.m_Instance.LightWidgetPrefab);
            lightWidget.m_LoadingFromSketch = true;
            lightWidget.transform.parent = App.Instance.m_CanvasTransform;
            lightWidget.transform.localScale = Vector3.one;
            var light = lightWidget.SetLightType(tiltLight.PunctualLightType);
            lightWidget.SetCanvas(App.Scene.GetOrCreateLayer(tiltLight.LayerId));
            lightWidget.SetSignedWidgetSize(tiltLight.Transform.scale);
            lightWidget.Show(bShow: true, bPlayAudio: false);
            lightWidget.transform.localPosition = tiltLight.Transform.translation;
            lightWidget.transform.localRotation = tiltLight.Transform.rotation;
            light.color = tiltLight.LightColor ?? Color.white;
            light.intensity = tiltLight.Intensity ?? 1;
            if (tiltLight.Range.HasValue) { light.range = tiltLight.Range.Value; }
            if (tiltLight.OuterConeAngle.HasValue) { light.spotAngle = tiltLight.OuterConeAngle.Value; }
            if (tiltLight.InnerConeAngle.HasValue) { light.innerSpotAngle = tiltLight.InnerConeAngle.Value; }
            light.enabled = tiltLight.Enabled ?? true;
            if (tiltLight.Shadows.HasValue) { light.shadows = tiltLight.Shadows.Value; }
            if (tiltLight.Pinned)
            {
                lightWidget.PinFromSave();
            }
            lightWidget.Group = App.GroupManager.GetGroupFromId(tiltLight.GroupId);
            lightWidget.RefreshLightGizmo();

            TiltMeterScript.m_Instance.AdjustMeterWithWidget(lightWidget.GetTiltMeterCost(), up: true);
            lightWidget.UpdateScale();
        }

        public Light SetLightType(LightType lightType)
        {
            if (lightType != LightType.Point && lightType != LightType.Spot && lightType != LightType.Directional)
            {
                throw new ArgumentOutOfRangeException(nameof(lightType), "Unsupported widget light type");
            }
            var lights = gameObject.GetComponentsInChildren<Light>(true);
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
            if (activeLight == null)
            {
                var child = new GameObject($"{lightType} light");
                child.transform.SetParent(transform, false);
                activeLight = child.AddComponent<Light>();
                activeLight.type = lightType;
            }
            SetupLightGizmo(activeLight);
            return activeLight;
        }

        public void RefreshLightGizmo() => SetupLightGizmo(GetComponentInChildren<Light>());

        private void SetupLightGizmo(Light light)
        {
            var gizmo = light.GetComponentInChildren<SceneLightGizmo>(true);
            if (gizmo == null) { gizmo = Instantiate(WidgetManager.m_Instance.SceneLightGizmoPrefab, light.transform); }
            gizmo.SetupLightGizmos(light);
            m_BoxCollider = gizmo.GetComponentInChildren<BoxCollider>();
            m_Mesh = gizmo.ActiveMeshTransform;
            HighlightMeshXfs = new[] { m_Mesh };
            HierarchyUtils.RecursivelySetLayer(light.transform, gameObject.layer, skipUI: false);
        }

        public TrTransform GetSaveTransform()
        {
            var xf = TrTransform.FromLocalTransform(transform);
            xf.scale = GetSignedWidgetSize();
            return xf;
        }
    }
} // namespace TiltBrush
