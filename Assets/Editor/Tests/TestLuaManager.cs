// Copyright 2026 The Open Brush Authors
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
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestLuaManager
    {
        [Test]
        public void VoxelCrossStamp_PaintsSevenCellsOncePerTriggerAndSkipsPreview()
        {
            var script = new MoonSharp.Interpreter.Script();
            script.DoString(@"
                Brush = { triggerIsPressed = false, position = {}, colorRgb = {} }
                Tool = { isPreview = false }
                Vector3 = { New = function(self, x, y, z) return { x=x, y=y, z=z } end }
                painted = 0
                visualUpdates = 0
                hitAvailable = true
                local doc = { SetAutoVisuals = function(self, enabled)
                    if enabled then visualUpdates = visualUpdates + 1 end
                end }
                local model = {
                    isValid = true, document = doc,
                    VoxelToCanvas = function(self, cell) return cell end,
                    PaintAt = function(self, position, color)
                        assert(color == Brush.colorRgb)
                        painted = painted + 1
                        return true
                    end
                }
                Vox = { FindHit = function(self, position, radius)
                    if hitAvailable then return { model=model, cell={ x=1, y=1, z=1 } } end
                end }");
            script.DoString(System.IO.File.ReadAllText(
                "Assets/Resources/LuaScriptExamples/ToolScript.VoxelCrossStamp.lua"));
            script.DoString(@"
                Start()
                Main()
                assert(painted == 0)
                Brush.triggerIsPressed = true
                Tool.isPreview = true
                Main()
                assert(painted == 0)
                Tool.isPreview = false
                Main()
                Main()
                assert(painted == 7 and visualUpdates == 1)
                Brush.triggerIsPressed = false
                Main()
                Brush.triggerIsPressed = true
                hitAvailable = false
                Main()
                assert(painted == 7)
                hitAvailable = true
                Main()
                assert(painted == 14 and visualUpdates == 2)");
        }

        [Test]
        public void VoxelScriptPreviewCannotChangeCellsPaletteOrModelStructure()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("preview", UnityEngine.Vector3Int.one);
            var wrapper = new VoxDocumentApiWrapper(document);
            var voxel = wrapper.Model();
            var originalColor = document.Palette[0];

            using (VoxScriptEditGroup.BeginExecution(preview: true))
            {
                Assert.IsFalse(voxel.SetVoxel(0, 0, 0, 1));
                Assert.IsFalse(wrapper.SetPalette(1, 12, 34, 56));
                Assert.IsNull(wrapper.AddModel(2, 2, 2));
                Assert.IsFalse(voxel.Remove());
                // A nested final invocation must not escape an outer preview.
                using (VoxScriptEditGroup.BeginExecution(preview: false))
                    Assert.IsFalse(voxel.SetVoxel(0, 0, 0, 1));
            }

            Assert.AreEqual(0, model.Voxels.Count);
            Assert.AreEqual(1, document.Models.Count);
            Assert.AreEqual(originalColor, document.Palette[0]);
            Assert.IsTrue(voxel.SetVoxel(0, 0, 0, 1));
            Assert.AreEqual(1, model.Voxels.Count);
        }

        [Test]
        public void CopiedPenroseSettingsMapSourceTileToOtherTiles()
        {
            var settings = new SymmetrySettingsApiWrapper(false)
            {
                mode = SymmetryMode.Penrose,
                penroseSubdivisions = 2,
                penroseRadius = 1.7f,
                penroseTiles = SymmetryPenroseTiles.Thin
            };
            var native = new PenroseTiling(2, 1.7f, PenroseTiling.TileSelection.Thin);
            var matrices = settings.Duplicate().matrices._Matrices;
            Assert.AreEqual(native.matrices.Count, matrices.Count);
            Assert.AreEqual(Matrix4x4.identity, matrices[0]);
            var sourceCenter = native.matrices[0].MultiplyPoint3x4(Vector3.zero);
            var targetCenter = native.matrices[1].MultiplyPoint3x4(Vector3.zero);
            Assert.Less((matrices[1].MultiplyPoint3x4(sourceCenter) - targetCenter).sqrMagnitude, 1e-8f);
        }

        [Test]
        public void CopiedLineSettingsAdvanceAlongWidgetYAxis()
        {
            var settings = new SymmetrySettingsApiWrapper(false)
            {
                mode = SymmetryMode.LineGroup,
                lineFamily = SymmetryLineFamily.ScrewRotations,
                lineOrder = 3,
                lineRepeats = 2,
                lineAdvance = 0.7f,
                lineAngle = 25f
            };
            var matrices = settings.Duplicate().matrices._Matrices;
            Assert.AreEqual(6, matrices.Count);
            Assert.Less((matrices[3].MultiplyPoint3x4(Vector3.zero) - Vector3.up * 0.7f).sqrMagnitude, 1e-8f);
        }

        [Test]
        public void CopiedSpaceGroupSettingsIncludeRepeatedCells()
        {
            var settings = new SymmetrySettingsApiWrapper(false)
            {
                mode = SymmetryMode.SpaceGroup,
                spaceGroup = 19,
                spaceRepeatX = 2,
                spaceRepeatY = 1,
                spaceRepeatZ = 1,
                spaceCellSize = 1.3f
            };
            var matrices = settings.Duplicate().matrices._Matrices;
            Assert.AreEqual(8, matrices.Count);
            Assert.AreEqual(Matrix4x4.identity, matrices[0]);
            Assert.Less((matrices[4].MultiplyPoint3x4(Vector3.zero) - Vector3.right * 1.3f).sqrMagnitude, 1e-8f);
        }

        [Test]
        public void FindFirstDrawableToolScriptPathIndexSkipsShortPaths()
        {
            var paths = new List<List<TrTransform>>
            {
                null,
                new(),
                new() { TrTransform.identity },
                new() { TrTransform.identity, TrTransform.identity },
                new() { TrTransform.identity, TrTransform.identity, TrTransform.identity }
            };

            Assert.AreEqual(4, LuaManager.FindFirstDrawableToolScriptPathIndex(paths));
        }

        [Test]
        public void FindFirstDrawableToolScriptPathIndexReturnsMinusOneWithoutDrawablePath()
        {
            var paths = new List<List<TrTransform>>
            {
                new(),
                new() { TrTransform.identity, TrTransform.identity }
            };

            Assert.AreEqual(-1, LuaManager.FindFirstDrawableToolScriptPathIndex(paths));
            Assert.AreEqual(-1, LuaManager.FindFirstDrawableToolScriptPathIndex(null));
        }

        [Test]
        public void ToolScriptFinalExecutionIsNeverThrottled()
        {
            Assert.IsTrue(ScriptedTool.ShouldExecuteToolScript(
                isPreviewExecution: false, previewInterval: 1f,
                currentTime: 0f, nextPreviewTime: 1f));
        }

        [Test]
        public void ToolScriptPreviewWithoutIntervalIsNeverThrottled()
        {
            Assert.IsTrue(ScriptedTool.ShouldExecuteToolScript(
                isPreviewExecution: true, previewInterval: 0f,
                currentTime: 0f, nextPreviewTime: 1f));
        }

        [Test]
        public void ToolScriptPreviewWaitsUntilItsNextUpdate()
        {
            Assert.IsFalse(ScriptedTool.ShouldExecuteToolScript(
                isPreviewExecution: true, previewInterval: 0.1f,
                currentTime: 0.99f, nextPreviewTime: 1f));
            Assert.IsTrue(ScriptedTool.ShouldExecuteToolScript(
                isPreviewExecution: true, previewInterval: 0.1f,
                currentTime: 1f, nextPreviewTime: 1f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void ToolScriptStrokePreviewRejectsNonPositiveScale(float strokeScale)
        {
            Assert.IsFalse(PointerScript.IsValidToolScriptPreviewScale(strokeScale));
        }

        [Test]
        public void ToolScriptStrokePreviewRejectsNonFiniteScale()
        {
            Assert.IsFalse(PointerScript.IsValidToolScriptPreviewScale(float.NaN));
            Assert.IsFalse(PointerScript.IsValidToolScriptPreviewScale(float.PositiveInfinity));
            Assert.IsFalse(PointerScript.IsValidToolScriptPreviewScale(float.NegativeInfinity));
        }

        [Test]
        public void ToolScriptStrokePreviewAcceptsPositiveScale()
        {
            Assert.IsTrue(PointerScript.IsValidToolScriptPreviewScale(0.000001f));
            Assert.IsTrue(PointerScript.IsValidToolScriptPreviewScale(1f));
        }

        [Test]
        public void ToolScriptStrokePreviewIsIdleOutsideGesture()
        {
            Assert.AreEqual(
                ScriptedTool.StrokePreviewExecutionPhase.Idle,
                ScriptedTool.GetStrokePreviewExecutionPhase(
                    strokePreviewRequested: true, gestureIsActive: false,
                    triggerIsHeld: false));
        }

        [Test]
        public void ToolScriptStrokePreviewUsesPreviewPhaseWhileHeld()
        {
            Assert.AreEqual(
                ScriptedTool.StrokePreviewExecutionPhase.Preview,
                ScriptedTool.GetStrokePreviewExecutionPhase(
                    strokePreviewRequested: true, gestureIsActive: true,
                    triggerIsHeld: true));
        }

        [Test]
        public void ToolScriptStrokePreviewUsesFinalPhaseWhenGestureEnds()
        {
            Assert.AreEqual(
                ScriptedTool.StrokePreviewExecutionPhase.Final,
                ScriptedTool.GetStrokePreviewExecutionPhase(
                    strokePreviewRequested: true, gestureIsActive: true,
                    triggerIsHeld: false));
        }

        [TestCase(ScriptCoordSpace.Default, true)]
        [TestCase(ScriptCoordSpace.Pointer, true)]
        [TestCase(ScriptCoordSpace.Canvas, false)]
        public void ToolScriptAngleSnapOnlyAppliesToPlacedSpaces(
            ScriptCoordSpace space, bool expected)
        {
            Assert.AreEqual(
                expected,
                LuaManager.ShouldApplyToolScriptAngleSnap(
                    angleSnapEnabled: true, space: space));
        }
    }
}
