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
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public enum OpCategories
    {
        ConwayOperators,
        ExtendedOperators,
        FaceTransforms,
        ObjectTransforms,
        Thickening,
        Topology,
        Deformations,
        Duplication,
    }

    public class PolyhydraPopUpWindowOperators : PolyhydraPopUpWindowBase
    {

        public TextMeshPro m_CategoryLabel;

        [NonSerialized]
        public Dictionary<OpCategories, List<PolyMesh.Operation>> m_ValidOps = new()
        {
            {
                OpCategories.ConwayOperators,
                new()
                {
                    // PolyMesh.Operation.Identity,
                    PolyMesh.Operation.Kis,
                    PolyMesh.Operation.Ambo,
                    PolyMesh.Operation.Zip,
                    PolyMesh.Operation.Expand,
                    PolyMesh.Operation.Bevel,
                    PolyMesh.Operation.Join,
                    PolyMesh.Operation.Ortho,
                    PolyMesh.Operation.Meta,
                    PolyMesh.Operation.Truncate,
                    PolyMesh.Operation.Dual,
                    PolyMesh.Operation.Gyro,
                    PolyMesh.Operation.Snub,
                    PolyMesh.Operation.Subdivide,
                    PolyMesh.Operation.Loft,
                    PolyMesh.Operation.Chamfer,
                }
            },
            {
                OpCategories.ExtendedOperators,
                new()
                {
                    PolyMesh.Operation.Needle,
                    PolyMesh.Operation.Ortho3,
                    PolyMesh.Operation.Subdiv,
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
                    PolyMesh.Operation.Volute,
                    PolyMesh.Operation.Exalt,
                    PolyMesh.Operation.Yank,
                    PolyMesh.Operation.Squall,
                    PolyMesh.Operation.JoinSquall,
                    PolyMesh.Operation.Zellige,
                    PolyMesh.Operation.Girih,
                    PolyMesh.Operation.Cross,
                    PolyMesh.Operation.SubdivideEdges,
                    PolyMesh.Operation.SplitFaces,
                    PolyMesh.Operation.Gable,
                }
            },
            {
                OpCategories.Thickening,
                new()
                {
                    PolyMesh.Operation.Extrude,
                    PolyMesh.Operation.Shell,
                    PolyMesh.Operation.Segment,
                    PolyMesh.Operation.Skeleton,
                }
            },
            {
                OpCategories.ObjectTransforms,
                new()
                {
                    PolyMesh.Operation.ScaleX,
                    PolyMesh.Operation.ScaleY,
                    PolyMesh.Operation.ScaleZ,
                    PolyMesh.Operation.Recenter,
                    PolyMesh.Operation.SitLevel,
                }
            },
            {
                OpCategories.FaceTransforms,
                new()
                {
                    PolyMesh.Operation.FaceOffset,
                    PolyMesh.Operation.FaceScale,
                    PolyMesh.Operation.FaceInset,
                    PolyMesh.Operation.FaceRotateX,
                    PolyMesh.Operation.FaceRotateY,
                    PolyMesh.Operation.FaceRotateZ,
                    PolyMesh.Operation.FaceSlide,
                    PolyMesh.Operation.VertexScale,
                    PolyMesh.Operation.VertexRotate,
                    PolyMesh.Operation.VertexOffset,
                }
            },
            {
                OpCategories.Topology,
                new()
                {
                    PolyMesh.Operation.FaceRemove,
                    // PolyMesh.Operation.VertexRemove,
                    PolyMesh.Operation.FillHoles,
                    PolyMesh.Operation.Weld,
                    PolyMesh.Operation.ConvexHull,
                    PolyMesh.Operation.MergeCoplanar,
                }
            },
            {
                OpCategories.Deformations,
                new()
                {
                    PolyMesh.Operation.TaperX,
                    PolyMesh.Operation.TaperY,
                    PolyMesh.Operation.TaperZ,
                    PolyMesh.Operation.Spherize,
                    PolyMesh.Operation.Cylinderize,
                    PolyMesh.Operation.Bulge,
                    PolyMesh.Operation.Wave,
                    PolyMesh.Operation.Canonicalize,
                    PolyMesh.Operation.PerlinNoiseX,
                    PolyMesh.Operation.PerlinNoiseY,
                    PolyMesh.Operation.PerlinNoiseZ,
                }
            },
            {
                OpCategories.Duplication,
                new()
                {
                    PolyMesh.Operation.DuplicateX,
                    PolyMesh.Operation.DuplicateY,
                    PolyMesh.Operation.DuplicateZ,
                    PolyMesh.Operation.MirrorX,
                    PolyMesh.Operation.MirrorY,
                    PolyMesh.Operation.MirrorZ,
                    PolyMesh.Operation.AddDual,
                }
            }
            // Store/Recall
            // PolyMesh.Operation.AddTag,
            // PolyMesh.Operation.RemoveTag,
            // PolyMesh.Operation.ClearTags,

            // Generator Ops
            // PolyMesh.Operation.Sweep,
        };


        public override void Init(GameObject rParent, string sText)
        {
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            FirstButtonIndex = ParentPanel.CurrentOperatorPage * ButtonsPerPage;
            base.Init(rParent, sText);
            UpdateCategoryLabel();
        }

        protected override ItemListResults GetItemsList()
        {
            var allItems = GetValidOps();
            int nextPageButtonIndex = FirstButtonIndex + ButtonsPerPage;
            bool nextPageExists = nextPageButtonIndex <= allItems.Count();

            return new ItemListResults(
                allItems.Skip(FirstButtonIndex)
                .Take(ButtonsPerPage)
                .ToList(), nextPageExists);
        }

        private IEnumerable<string> GetValidOps()
        {
            return m_ValidOps[ParentPanel.CurrentOpCategory].Select(o => o.ToString());
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

        private void UpdateCategoryLabel()
        {
            m_CategoryLabel.text = PolyhydraPanel.LabelFormatter(ParentPanel.CurrentOpCategory.ToString());
        }

        public void NextCategory()
        {
            if (ParentPanel.CurrentOpCategoryIndex < Enum.GetNames(typeof(OpCategories)).Length - 1)
            {
                ParentPanel.CurrentOpCategoryIndex++;
                UpdateCategoryLabel();
                ParentPanel.CurrentOperatorPage = 0;
                FirstButtonIndex = 0;
                CreateButtons();
            }
        }

        public void PrevCategory()
        {
            if (ParentPanel.CurrentOpCategoryIndex > 0)
            {
                ParentPanel.CurrentOpCategoryIndex--;
                UpdateCategoryLabel();
                ParentPanel.CurrentOperatorPage = 0;
                FirstButtonIndex = 0;
                CreateButtons();
            }
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
