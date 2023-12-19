// Copyright 2022 The Open Brush Authors
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
using Polyhydra.Core;
using UnityEngine;
using TiltBrush.MeshEditing;

namespace TiltBrush
{

    public class EditableModelWidget : ModelWidget
    {
        public PolyRecipe m_PolyRecipe;
        public PolyMesh m_PolyMesh;

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            EditableModelWidget clone = EditableModelManager.m_Instance.GeneratePolyMesh(
                m_PolyMesh,
                m_PolyRecipe.Clone(),
                TrTransform.FromLocalTransform(transform)
            );
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;

            PolyMesh oldPoly = m_PolyMesh;
            PolyMesh newPoly = oldPoly.Duplicate();
            newPoly.ScalingFactor = oldPoly.ScalingFactor;
            clone.m_PolyRecipe = m_PolyRecipe.Clone();
            EditableModelManager.m_Instance.RegenerateMesh(clone, newPoly, m_PolyRecipe.CurrentMaterial);
            clone.transform.parent = transform.parent;
            clone.SetSignedWidgetSize(size);
            clone.m_WidgetRenderers = GetComponentsInChildren<Renderer>();
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
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
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        protected override void CloneInitialMaterials(GrabWidget other)
        {
            m_WidgetRenderers = GetComponentsInChildren<Renderer>()
                // Exclude the gameobject that has the editableModelId
                .Where(r => r.gameObject != GetModelGameObject()).ToArray();
            m_InitialMaterials = m_WidgetRenderers.ToDictionary(x => x, x => x.sharedMaterials);
            m_NewMaterials = m_WidgetRenderers.ToDictionary(x => x, x => x.materials);
        }

        public GameObject GetModelGameObject()
        {
            // Returns the child GameObject that contains the editable model itself
            return gameObject.GetComponentInChildren<ObjModelScript>().gameObject;
        }

        public override void RegisterHighlight()
        {
#if !UNITY_ANDROID
            var mf = GetModelGameObject().GetComponent<MeshFilter>();
            App.Instance.SelectionEffect.RegisterMesh(mf);
            return;
#endif
            base.RegisterHighlight();
        }

        protected override void UnregisterHighlight()
        {
#if !UNITY_ANDROID
            var mf = GetModelGameObject().GetComponent<MeshFilter>();
            App.Instance.SelectionEffect.UnregisterMesh(mf);
            return;
#endif
            base.UnregisterHighlight();
        }

        // TODO reduce code duplication with CreateModelFromSaveData
        public static void CreateEditableModelFromSaveData(TiltEditableModels modelDatas)
        {
            Debug.AssertFormat(modelDatas.AssetId == null || modelDatas.FilePath == null,
                "Model Data should not have an AssetID *and* a File Path");

            bool ok;
            if (modelDatas.FilePath != null)
            {
                var task = CreateModelsFromRelativePath(
                    modelDatas.FilePath,
                    modelDatas.Transforms, modelDatas.RawTransforms, modelDatas.PinStates,
                    modelDatas.GroupIds, modelDatas.LayerIds, modelDatas.FrameIds);
                ok = task.IsCompletedSuccessfully;
            }
            else if (modelDatas.AssetId != null)
            {
                CreateEditableModelsFromAssetId(
                    modelDatas.AssetId, modelDatas.RawTransforms, modelDatas.PinStates,
                    modelDatas.GroupIds, modelDatas.LayerIds, modelDatas.FrameIds);
                ok = true;
            }
            else
            {
                Debug.LogError("Model Data doesn't contain an AssetID or File Path.");
                ok = false;
            }

            if (!ok)
            {
                ModelCatalog.m_Instance.AddMissingModel(
                    modelDatas.FilePath, modelDatas.Transforms, modelDatas.RawTransforms);
            }
        }

        // Used when loading model assetIds from a serialized format (e.g. Tilt file).
        private static void CreateEditableModelsFromAssetId(
            string assetId, TrTransform[] rawXfs,
            bool[] pinStates, uint[] groupIds, int[] layerIds, int[] frameIds)
        {
            // Request model from Poly and if it doesn't exist, ask to load it.
            Model model = App.PolyAssetCatalog.GetModel(assetId);
            if (model == null)
            {
                // This Model is transient; the Widget will replace it with a good Model from the PAC
                // as soon as the PAC loads it.
                model = new Model(Model.Location.Generated(assetId));
            }
            if (!model.m_Valid)
            {
                App.PolyAssetCatalog.RequestModelLoad(assetId, "widget");
            }

            // Create a widget for each transform.
            for (int i = 0; i < rawXfs.Length; ++i)
            {
                bool pin = (i < pinStates.Length) ? pinStates[i] : true;
                uint groupId = (groupIds != null && i < groupIds.Length) ? groupIds[i] : 0;
                int layerId = (layerIds != null && i < layerIds.Length) ? layerIds[i] : 0;
                int frameId = (frameIds != null && i < frameIds.Length) ? frameIds[i] : 0;
                CreateModel(model, rawXfs[i], pin, isNonRawTransform: false, groupId, layerId, frameId, assetId: assetId);
            }
        }

        protected override TrTransform ApplyAxisLocks(TrTransform xf_GS)
        {
            var outXf_CS = App.ActiveCanvas.Pose.inverse * xf_GS;
            // Restore transforms for locked axes
            if (SelectionManager.m_Instance.m_LockTranslationX) outXf_CS.translation.x = transform.localPosition.x;
            if (SelectionManager.m_Instance.m_LockTranslationY) outXf_CS.translation.y = transform.localPosition.y;
            if (SelectionManager.m_Instance.m_LockTranslationZ) outXf_CS.translation.z = transform.localPosition.z;
            var euler = outXf_CS.rotation.eulerAngles;
            if (SelectionManager.m_Instance.m_LockRotationX) euler.x = transform.localRotation.eulerAngles.x;
            if (SelectionManager.m_Instance.m_LockRotationY) euler.y = transform.localRotation.eulerAngles.y;
            if (SelectionManager.m_Instance.m_LockRotationZ) euler.z = transform.localRotation.eulerAngles.z;
            outXf_CS.rotation.eulerAngles = euler;
            xf_GS = App.ActiveCanvas.Pose * outXf_CS;
            return xf_GS;
        }

    }
} // namespace TiltBrush
