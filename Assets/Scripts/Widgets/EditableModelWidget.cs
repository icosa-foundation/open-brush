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
using System.Linq;
using UnityEngine;
using Polyhydra.Core;
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
            clone.SetSignedWidgetSize(this.m_Size);
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
            var thisId = GetId();
            var newPoly = EditableModelManager.m_Instance.GetPolyMesh(thisId).Duplicate();
            var col = polyGo.AddComponent<BoxCollider>();
            col.size = m_BoxCollider.size;
            var colMethod = EditableModelManager.m_Instance.GetColorMethod(thisId);
            EditableModelManager.m_Instance.RegenerateMesh(clone, newPoly);
            EditableModelManager.m_Instance.RegisterEditableMesh(polyGo, newPoly, colMethod);
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
    }
} // namespace TiltBrush
