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

using System.Linq;
using UnityEngine;
using TiltBrush.MeshEditing;

namespace TiltBrush
{

    public class EditableModelWidget : ModelWidget
    {

        override public GrabWidget Clone()
        {
            EditableModelWidget clone = Instantiate(WidgetManager.m_Instance.EditableModelWidgetPrefab);

            // TODO everything after here and before var "editableModelId"
            // is duplicated with ModelWidget.Clone
            clone.transform.position = transform.position;
            clone.transform.rotation = transform.rotation;
            clone.Model = Model;
            // We're obviously not loading from a sketch.  This is to prevent the intro animation.
            // TODO: Change variable name to something more explicit of what this flag does.
            clone.m_LoadingFromSketch = true;
            clone.Show(true, false);
            clone.transform.parent = transform.parent;
            clone.SetSignedWidgetSize(m_Size);
            EditableModelManager.m_Instance.CloneEditableModel(clone);

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

            if (!clone.Model.m_Valid)
            {
                App.PolyAssetCatalog.CatalogChanged += clone.OnPacCatalogChanged;
                clone.m_PolyCallbackActive = true;
            }
            clone.CloneInitialMaterials(this);
            clone.TrySetCanvasKeywordsFromObject(transform);

            var polyGo = clone.GetId().gameObject;
            var col = polyGo.AddComponent<BoxCollider>();
            col.size = m_BoxCollider.size;

            // TODO
            // Commenting out the following fixed a bug whereby
            // duplicated editable models were slightly bigger than they should have been
            // Still don't fully understand why or whether commenting this out will have side effects...
            var thisId = GetId();
            var newPoly = EditableModelManager.m_Instance.GetPolyMesh(thisId).Duplicate();
            EditableModelManager.m_Instance.RegenerateMesh(clone, newPoly);

            return clone;
        }

        public EditableModelId GetId()
        {
            return gameObject.GetComponentInChildren<EditableModelId>();
        }

        public override void RegisterHighlight()
        {
#if !UNITY_ANDROID
            var mf = GetId().GetComponent<MeshFilter>();
            App.Instance.SelectionEffect.RegisterMesh(mf);
            return;
#endif
            base.RegisterHighlight();
        }

        protected override void UnregisterHighlight()
        {
#if !UNITY_ANDROID
            var mf = GetId().GetComponent<MeshFilter>();
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
                ok = CreateModelsFromRelativePath(
                    modelDatas.FilePath,
                    modelDatas.Transforms, modelDatas.RawTransforms, modelDatas.PinStates,
                    modelDatas.GroupIds);
            }
            else if (modelDatas.AssetId != null)
            {
                CreateEditableModelsFromAssetId(
                    modelDatas.AssetId,
                    modelDatas.RawTransforms, modelDatas.PinStates, modelDatas.GroupIds);
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
            bool[] pinStates, uint[] groupIds)
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
                CreateModel(model, rawXfs[i], pin, isNonRawTransform: false, groupId, assetId: assetId);
            }
        }

    }
} // namespace TiltBrush
