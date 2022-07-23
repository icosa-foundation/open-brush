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

namespace TiltBrush
{

    public class PolyhydraPopUpWindowOperators : PolyhydraPopUpWindowBase
    {
        [NonSerialized]
        public List<PolyMesh.Operation> m_ValidOps = new()
        {
            // PolyMesh.Operation.Identity,
            PolyMesh.Operation.Kis,
            PolyMesh.Operation.Ambo,
            PolyMesh.Operation.Zip,
            PolyMesh.Operation.Expand,
            PolyMesh.Operation.Bevel,
            PolyMesh.Operation.Join,
            // PolyMesh.Operation.Needle,
            PolyMesh.Operation.Ortho,
            PolyMesh.Operation.Ortho3,
            PolyMesh.Operation.Subdiv,
            PolyMesh.Operation.Meta,
            PolyMesh.Operation.Truncate,
            PolyMesh.Operation.Dual,
            PolyMesh.Operation.Gyro,
            PolyMesh.Operation.Snub,
            PolyMesh.Operation.Subdivide,
            PolyMesh.Operation.Loft,
            PolyMesh.Operation.Chamfer,
            PolyMesh.Operation.Quinto,
            PolyMesh.Operation.Lace,
            PolyMesh.Operation.JoinedLace,
            PolyMesh.Operation.OppositeLace,
            PolyMesh.Operation.JoinKisKis,
            PolyMesh.Operation.Stake,
            PolyMesh.Operation.JoinStake,
            PolyMesh.Operation.Medial,
            PolyMesh.Operation.EdgeMedial,
            PolyMesh.Operation.Propeller,
            PolyMesh.Operation.Whirl,
            // PolyMesh.Operation.Volute,
            // PolyMesh.Operation.Exalt,
            // PolyMesh.Operation.Yank,
            // PolyMesh.Operation.Squall,
            // PolyMesh.Operation.JoinSquall,
            PolyMesh.Operation.Cross,

            // PolyMesh.Operation.SplitFaces,
            PolyMesh.Operation.Gable,

            PolyMesh.Operation.Extrude,
            PolyMesh.Operation.Shell,
            PolyMesh.Operation.Segment,
            PolyMesh.Operation.Skeleton,

            PolyMesh.Operation.ScaleX,
            PolyMesh.Operation.ScaleY,
            PolyMesh.Operation.ScaleZ,
            PolyMesh.Operation.Recenter,
            PolyMesh.Operation.SitLevel,

            PolyMesh.Operation.FaceOffset,
            PolyMesh.Operation.FaceScale,
            PolyMesh.Operation.FaceRotateX,
            PolyMesh.Operation.FaceRotateY,
            PolyMesh.Operation.FaceRotateZ,
            // PolyMesh.Operation.FaceSlide,

            // PolyMesh.Operation.VertexScale,
            // PolyMesh.Operation.VertexRotate,
            // PolyMesh.Operation.VertexOffset,
            // PolyMesh.Operation.VertexStellate,

            PolyMesh.Operation.FaceRemove,
            // PolyMesh.Operation.VertexRemove,

            // PolyMesh.Operation.FillHoles,

            // PolyMesh.Operation.Weld,
            PolyMesh.Operation.ConvexHull,

            PolyMesh.Operation.Spherize,
            PolyMesh.Operation.Cylinderize,
            PolyMesh.Operation.Bulge,
            PolyMesh.Operation.Wave,
            PolyMesh.Operation.Canonicalize,
            PolyMesh.Operation.PerlinNoiseX,
            PolyMesh.Operation.PerlinNoiseY,
            PolyMesh.Operation.PerlinNoiseZ,

            // PolyMesh.Operation.AddTag,
            // PolyMesh.Operation.RemoveTag,
            // PolyMesh.Operation.ClearTags,

            // PolyMesh.Operation.Sweep,
        };

        public override void Init(GameObject rParent, string sText)
        {
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            FirstButtonIndex = ParentPanel.CurrentOperatorPage * ButtonsPerPage;
            base.Init(rParent, sText);
        }

        protected override List<string> GetItemsList()
        {
            return GetValidOps()
                .Skip(FirstButtonIndex)
                .Take(ButtonsPerPage)
                .ToList();
        }

        private IEnumerable<string> GetValidOps()
        {
            return m_ValidOps.Select(o => o.ToString());
        }

        public override Texture2D GetButtonTexture(string action)
        {
            return ParentPanel.GetButtonTexture(PolyhydraButtonTypes.OperatorType, action);
        }

        public override void HandleButtonPress(string action, bool isFolder)
        {
            PolyhydraPanel.FriendlyOpLabels.TryGetValue(action, out string friendlyLabel);
            ParentPanel.SetButtonTextAndIcon(PolyhydraButtonTypes.OperatorType, action, friendlyLabel);
            ParentPanel.ChangeCurrentOpType(action);
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < GetValidOps().Count())
            {
                FirstButtonIndex += ButtonsPerPage;
                CreateButtons();
            }
            ParentPanel.CurrentOperatorPage = FirstButtonIndex / ButtonsPerPage;
        }

        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
            ParentPanel.CurrentOperatorPage = FirstButtonIndex / ButtonsPerPage;
        }

    }
} // namespace TiltBrush
