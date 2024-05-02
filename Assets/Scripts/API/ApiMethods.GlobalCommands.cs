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

using System.IO;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        // // Dangerous
        // [ApiEndpoint("save.slot")]
        // public static void Save(int slot)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.Save;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot);
        // }

        [ApiEndpoint("save.overwrite", "Save the current scene overwriting the last save if it exists")]
        public static void SaveOverwrite()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Save;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, -1, -1);
        }

        [ApiEndpoint("save.new", "Saves the current scene in a new slot")]
        public static void SaveNew()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SaveNew;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
        }

        // TODO
        // [ApiEndpoint("upload", "Saves the current scene and uploads it to Poly/Icosa")]
        // public static void SaveAndUpload()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.SaveAndUpload;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        [ApiEndpoint("export.all", "Exports all the scenes in the users's sketch folder")]
        public static void ExportAll()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ExportAll;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("drafting.visible", "Shows all strokes made with the drafting brush fully opaque")]
        public static void DraftingVisible()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DraftingVisibility;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 0);
        }
        [ApiEndpoint("drafting.transparent", "Shows all strokes made with the drafting brush semi-transparent")]
        public static void DraftingTransparent()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DraftingVisibility;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
        }
        [ApiEndpoint("drafting.hidden", "Hides all strokes made with the drafting brush")]
        public static void DraftingHidden()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DraftingVisibility;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 2);
        }

        [ApiEndpoint("load.user", "Loads the sketch from the user's sketch folder given an index (0 being most recent)")]
        public static void LoadUser(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 0);
        }

        [ApiEndpoint("load.curated", "Loads the sketch in the given slot number from the curated sketch list")]
        public static void LoadCurated(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 1);
        }

        [ApiEndpoint("load.liked", "Loads the sketch in the given slot number from the user's liked sketches")]
        public static void LoadLiked(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 2);
        }

        [ApiEndpoint("load.drive", "Loads the sketch in the given slot number from the user's Google Drive")]
        public static void LoadDrive(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 3);
        }

        [ApiEndpoint("load.named", "Loads the sketch with the given name from the user's sketch folder")]
        public static void LoadNamedFile(string filename)
        {
            // TODO do we want to allow arbitrary directories?
            // Does this even check for directory traversal?;
            SketchControlsScript.m_Instance.IssueGlobalCommand(
                SketchControlsScript.GlobalCommands.LoadNamedFile,
                (int)SketchControlsScript.LoadSpeed.Quick,
                -1,
                Path.Combine(App.UserSketchPath(), filename)
            );
            PanelManager.m_Instance.ToggleSketchbookPanels(true);
        }

        [ApiEndpoint("merge.named", "Loads the sketch with the given name from the user's sketch folder")]
        public static void MergeNamedFile(string filename)
        {
            // TODO do we want to allow arbitrary directories?
            // Does this even check for directory traversal?;
            SketchControlsScript.m_Instance.IssueGlobalCommand(
                SketchControlsScript.GlobalCommands.LoadNamedFile,
                (int)SketchControlsScript.LoadSpeed.Quick,
                1,
                Path.Combine(App.UserSketchPath(), filename)
            );
        }

        [ApiEndpoint("new", "Clears the current sketch")]
        public static void NewSketch()
        {
            var rEnum = SketchControlsScript.GlobalCommands.NewSketch;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("symmetry.mirror", "Sets the symmetry mode to 'mirror'")]
        public static void SymmetryPlane()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SymmetryPlane;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("symmetry.multimirror", "Sets the symmetry mode to 'multimirror'")]
        public static void MultiMirror()
        {
            var rEnum = SketchControlsScript.GlobalCommands.MultiMirror;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("twohandeded.toggle", "Toggles painting with both hands at once")]
        public static void SymmetryTwoHanded()
        {
            SketchControlsScript.GlobalCommands rEnum = SketchControlsScript.GlobalCommands.SymmetryTwoHanded;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO on and off explicitly
        [ApiEndpoint("straightedge.toggle", "Toggles the straight edge tool on or off")]
        public static void StraightEdge()
        {
            var rEnum = SketchControlsScript.GlobalCommands.StraightEdge;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO on and off explicitly
        [ApiEndpoint("autoorient.toggle", "Toggles autoorientate on or off")]
        public static void AutoOrient()
        {
            var rEnum = SketchControlsScript.GlobalCommands.AutoOrient;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("undo", "Undoes the last action")]
        public static void Undo()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Undo;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("redo", "Redo the last action")]
        public static void Redo()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Redo;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("panels.reset", "Reset the position of all panels")]
        public static void ResetAllPanels()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ResetAllPanels;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO Test this
        [ApiEndpoint("sketch.origin", "Enables the sketch origin tool")]
        public static void SketchOrigin()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SketchOrigin;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("viewonly.toggle", "Toggles 'view only' mode on or off")]
        public static void ViewOnly()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ViewOnly;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO Is this any use?
        // [ApiEndpoint("save.gallery")]
        // public static void SaveGallery()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.SaveGallery;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        [ApiEndpoint("spectator.toggle", "Toggles the spectator camera")]
        public static void ToggleSpectator()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DropCam;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("spectator.on", "Turns the spectator camera on")]
        public static void EnableSpectator()
        {
            SketchControlsScript.m_Instance.GetDropCampWidget().ShowInstantly(true);
        }

        [ApiEndpoint("spectator.off", "Turns the spectator camera off")]
        public static void DisableSpectator()
        {
            SketchControlsScript.m_Instance.GetDropCampWidget().ShowInstantly(false);
        }

        [ApiEndpoint("autosimplify.toggle", "Toggles 'auto-simplify' mode on or off")]
        public static void ToggleAutosimplification()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleAutosimplification;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("export.current", "Exports the current sketch to the user's Exports folder")]
        public static void ExportRaw()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ExportRaw;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO Do we need API commands to open panels?
        // [ApiEndpoint("camera.options")]
        // public static void CameraOptions()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.CameraOptions;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        [ApiEndpoint("showfolder.sketch", "Opens the user's Sketches folder on the desktop")]
        public static void ShowSketchFolder(int index)
        {
            var rEnum = SketchControlsScript.GlobalCommands.ShowSketchFolder;
            // TODO 0 is User folder. Do we need to support the other SketchSetTypes?
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, 0);
        }

        // TODO Why no "enabled" counterpart?
        [ApiEndpoint("guides.disable", "Disables all guides")]
        public static void StencilsDisable()
        {
            var rEnum = SketchControlsScript.GlobalCommands.StencilsDisabled;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // [ApiEndpoint("straightedge.meter")]
        // public static void StraightEdgeMeterDisplay()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.StraightEdgeMeterDisplay;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // [ApiEndpoint("sketchbook")]
        // public static void Sketchbook()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.Sketchbook;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // TODO Does this even work?
        // [ApiEndpoint("straightedge.shape")]
        // public static void StraightEdgeShape(bool enable)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.StraightEdgeShape;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, enable?1:0);
        // }

        // TODO Dangerous!
        // [ApiEndpoint("sketch.delete")]
        // public static void DeleteSketch(int iParam1, int iParam2)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.DeleteSketch;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1, iParam2);
        // }

        [ApiEndpoint("disco", "Starts a party")]
        public static void Disco()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Disco;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // [ApiEndpoint("view.online.gallery")]
        // public static void ViewOnlineGallery()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.ViewOnlineGallery;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // [ApiEndpoint("cancel.upload")]
        // public static void CancelUpload()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.CancelUpload;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // [ApiEndpoint("view.last.upload")]
        // public static void ViewLastUpload()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.ViewLastUpload;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // [ApiEndpoint("show.google.drive")]
        // public static void ShowGoogleDrive()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.ShowGoogleDrive;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // TODO Does this work?
        // [ApiEndpoint("googledrivesync.toggle", "Toggles syncing to Google Drive")]
        // public static void GoogleDriveSync()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.GoogleDriveSync;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // TODO Test this
        // [ApiEndpoint("google.drive.sync.folder")]
        // public static void GoogleDriveSync_Folder(int iParam1)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.GoogleDriveSync_Folder;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1);
        // }

        [ApiEndpoint("selection.duplicate", "Create a duplicate of the current selection (uses symmetry mirrors if active")]
        public static void DuplicateSelection()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Duplicate;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("selection.delete", "Deletes the current selection")]
        public static void DeleteSelection()
        {
            SelectionManager.m_Instance.DeleteSelection();
        }

        // TODO explicit group/ungroup
        [ApiEndpoint("selection.group", "Groups (or ungroups) the current selection")]
        public static void ToggleGroupStrokesAndWidgets()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleGroupStrokesAndWidgets;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO Test this and maybe choose a better command name
        [ApiEndpoint("export.selected", "Exports the selected strokes to the user's Media Library")]
        public static void SaveModel()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SaveModel;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO Set the list in App.Config.m_FilePatternsToExport
        // [ApiEndpoint("export.listed")]
        // public static void ExportListed()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.ExportListed;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // TDOO Test
        [ApiEndpoint("camerapath.render", "Renders the current camera path to a video")]
        public static void RenderCameraPath()
        {
            var rEnum = SketchControlsScript.GlobalCommands.RenderCameraPath;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("profiling.toggle", "Toggles profiling mode on or off")]
        public static void ToggleProfiling()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleProfiling;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // // TODO Do we need this?
        // [ApiEndpoint("autoprofile", "Runs autoprofile")]
        // public static void DoAutoProfile()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.DoAutoProfile;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // TODO Do we want panel toggles?
        [ApiEndpoint("settings.toggle", "Toggles the settings panel on or off")]
        public static void ToggleSettings()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleSettings;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("mirror.summon", "Summons the mirror origin to the user's position")]
        public static void SummonMirror()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SummonMirror;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("selection.invert", "Inverts the current selection")]
        public static void InvertSelection()
        {
            var rEnum = SketchControlsScript.GlobalCommands.InvertSelection;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("select.all", "Selects all strokes and widgets in the scene")]
        public static void SelectAll()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SelectAll;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("select.none", "Deselects all strokes and widgets in the scene")]
        public static void SelectNone()
        {
            SelectionManager.m_Instance.ClearActiveSelection();
        }

        [ApiEndpoint("selection.flip", "Mirrors the current selection")]
        public static void FlipSelection()
        {
            var rEnum = SketchControlsScript.GlobalCommands.FlipSelection;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO What does this do?
        // [ApiEndpoint("brushlab.toggle")]
        // public static void ToggleBrushLab()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.ToggleBrushLab;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        [ApiEndpoint("postprocessing.toggle", "Toggles post-processing effects on or off")]
        public static void ToggleCameraPostEffects()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleCameraPostEffects;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("watermark.toggle", "Toggles the watermark on or off")]
        public static void ToggleWatermark()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleWatermark;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // [ApiEndpoint("load.confirm.complex.high")]
        // public static void LoadConfirmComplexHigh(int iParam1, int iParam2)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.LoadConfirmComplexHigh;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1, iParam2);
        // }

        // [ApiEndpoint("load.confirm.complex")]
        // public static void LoadConfirmComplex(int iParam1, int iParam2)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.LoadConfirmComplex;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1, iParam2);
        // }

        // [ApiEndpoint("load.confirm.unsaved")]
        // public static void LoadConfirmUnsaved(int iParam1, int iParam2)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.LoadConfirmUnsaved;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1, iParam2);
        // }

        // [ApiEndpoint("load.wait.on.download")]
        // public static void LoadWaitOnDownload(int iParam1, int iParam2)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.LoadWaitOnDownload;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1, iParam2);
        // }

        // [ApiEndpoint("show.quest.side.loading")]
        // public static void ShowQuestSideLoading()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.ShowQuestSideLoading;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        // [ApiEndpoint("unload.reference.image.catalog")]
        // public static void UnloadReferenceImageCatalog()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.UnloadReferenceImageCatalog;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        [ApiEndpoint("camerapath.togglevisuals", "Toggles the camera path visuals on or off")]
        public static void ToggleCameraPathVisuals()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleCameraPathVisuals;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("camerapath.togglepreview", "Toggles the camera path preview on or off")]
        public static void ToggleCameraPathPreview()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleCameraPathPreview;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("camerapath.delete", "Deletes the current camera path")]
        public static void DeleteCameraPath()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DeleteCameraPath;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("camerapath.record", "Starts recording a camera path")]
        public static void RecordCameraPath()
        {
            var rEnum = SketchControlsScript.GlobalCommands.RecordCameraPath;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
    }
}


