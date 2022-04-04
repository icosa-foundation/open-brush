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
using UnityEngine;
using Polyhydra.Core;
using TiltBrush.MeshEditing;

namespace TiltBrush
{

    public class EditableModelWidget : ModelWidget
    {

        override public GrabWidget Clone()
        {
            var clone = base.Clone();
            var editableModelId = clone.GetComponentInChildren<EditableModelId>();
            editableModelId.guid = Guid.NewGuid().ToString();
            var polyMesh = EditableModelManager.m_Instance.GetPolyMesh(gameObject).Duplicate();
            
            // TODO clean this up
            var colMethod = EditableModelManager.m_Instance.GetColorMethod(gameObject);
            var mat = clone.gameObject.GetComponentInChildren<EditableModelId>().gameObject.GetComponent<MeshRenderer>().material;
            
            EditableModelManager.m_Instance.GenerateMesh(clone.gameObject, polyMesh, mat, colMethod, true);
            return clone;
        }

        public override void RegisterHighlight()
        {
#if !UNITY_ANDROID
            var mf = GetComponentInChildren<EditableModelId>().GetComponent<MeshFilter>();
            App.Instance.SelectionEffect.RegisterMesh(mf);
            return;
#endif
            base.RegisterHighlight();
        }

        protected override void UnregisterHighlight()
        {
#if !UNITY_ANDROID
            var mf = GetComponentInChildren<EditableModelId>().GetComponent<MeshFilter>();
            App.Instance.SelectionEffect.UnregisterMesh(mf);
            return;
#endif
            base.UnregisterHighlight();
        }
    }
} // namespace TiltBrush
