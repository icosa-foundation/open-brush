using UnityEngine;

namespace TiltBrush
{
    public static class GlobalCommandsApi
    {
        // // Dangerous
        // [ApiEndpoint("save.slot")]
        // public static void Save(int slot)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.Save;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot);
        // }
        
        [ApiEndpoint("save.overwrite")]
        public static void SaveOverwrite()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Save;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, -1, -1);
        }
        
        [ApiEndpoint("save")]
        public static void SaveNew()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SaveNew;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
        }

        // TODO 
        [ApiEndpoint("upload")]
        public static void SaveAndUpload()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SaveAndUpload;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("export.all")]
        public static void ExportAll()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ExportAll;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("drafting.visible")]
        public static void DraftingVisible()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DraftingVisibility;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 0);
        }
        [ApiEndpoint("drafting.transparent")]
        public static void DraftingTransparent()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DraftingVisibility;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
        }
        [ApiEndpoint("drafting.hidden")]
        public static void DraftingHidden()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DraftingVisibility;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 2);
        }
        
        [ApiEndpoint("load.user")]
        public static void LoadUser(int index)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, 0);
        }
        
        [ApiEndpoint("load.curated")]
        public static void LoadCurated(int index)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, 1);
        }
        
        [ApiEndpoint("load.liked")]
        public static void LoadLiked(int index)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, 2);
        }
        
        [ApiEndpoint("load.liked")]
        public static void LoadDrive(int index)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, 3);
        }
        
        [ApiEndpoint("load.named")]
        public static void LoadNamedFile(string pathName)
        {
            var rEnum = SketchControlsScript.GlobalCommands.LoadNamedFile;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, -1, -1, pathName);
        }
        
        [ApiEndpoint("new")]
        public static void NewSketch()
        {
            var rEnum = SketchControlsScript.GlobalCommands.NewSketch;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("symmetry.mirror")]
        public static void SymmetryPlane()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SymmetryPlane;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("symmetry.doublemirror")]
        public static void SymmetryFour()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SymmetryFour;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        // TODO on and off explicitly
        [ApiEndpoint("straightedge.toggle")]
        public static void StraightEdge()
        {
            var rEnum = SketchControlsScript.GlobalCommands.StraightEdge;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        // TODO on and off explicitly
        [ApiEndpoint("autoorient.toggle")]
        public static void AutoOrient()
        {
            var rEnum = SketchControlsScript.GlobalCommands.AutoOrient;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("undo")]
        public static void Undo()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Undo;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("redo")]
        public static void Redo()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Redo;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("panels.reset")]
        public static void ResetAllPanels()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ResetAllPanels;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        // TODO Test this
        [ApiEndpoint("sketch.origin")]
        public static void SketchOrigin()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SketchOrigin;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("viewonly")]
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
        
        [ApiEndpoint("dropcam")]
        public static void DropCam()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DropCam;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("autosimplify.toggle")]
        public static void ToggleAutosimplification()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleAutosimplification;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("export")]
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
        
        [ApiEndpoint("showfolder.sketch")]
        public static void ShowSketchFolder(int index)
        {
            var rEnum = SketchControlsScript.GlobalCommands.ShowSketchFolder;
            // TODO 0 is User folder. Do we need to support the other SketchSetTypes?
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, 0);
        }
        
        // TODO Why no "enabled" counterpart?
        [ApiEndpoint("stencils.disabled")]
        public static void StencilsDisabled()
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
        
        // Does this even work?
        [ApiEndpoint("straightedge.shape")]
        public static void StraightEdgeShape(bool enable)
        {
            var rEnum = SketchControlsScript.GlobalCommands.StraightEdgeShape;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, enable?1:0);
        }
        
        // TODO Dangerous!
        // [ApiEndpoint("sketch.delete")]
        // public static void DeleteSketch(int iParam1, int iParam2)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.DeleteSketch;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1, iParam2);
        // }
        
        [ApiEndpoint("disco")]
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
        
        [ApiEndpoint("googledrivesync.toggle")]
        public static void GoogleDriveSync()
        {
            var rEnum = SketchControlsScript.GlobalCommands.GoogleDriveSync;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        // TODO Test this
        // [ApiEndpoint("google.drive.sync.folder")]
        // public static void GoogleDriveSync_Folder(int iParam1)
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.GoogleDriveSync_Folder;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, iParam1);
        // }
        
        [ApiEndpoint("selection.duplicate")]
        public static void Duplicate()
        {
            var rEnum = SketchControlsScript.GlobalCommands.Duplicate;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        // TODO explicit group/ungroup
        [ApiEndpoint("selection.group")]
        public static void ToggleGroupStrokesAndWidgets()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleGroupStrokesAndWidgets;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        // TODO Test this and maybe choose a better command name
        [ApiEndpoint("export.selected")]
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
        
        [ApiEndpoint("camerapath.render")]
        public static void RenderCameraPath()
        {
            var rEnum = SketchControlsScript.GlobalCommands.RenderCameraPath;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("profiling.toggle")]
        public static void ToggleProfiling()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleProfiling;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("autoprofile")]
        public static void DoAutoProfile()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DoAutoProfile;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("settings.toggle")]
        public static void ToggleSettings()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleSettings;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("mirror.summon")]
        public static void SummonMirror()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SummonMirror;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("selection.invert")]
        public static void InvertSelection()
        {
            var rEnum = SketchControlsScript.GlobalCommands.InvertSelection;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("select.all")]
        public static void SelectAll()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SelectAll;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("selection.flip")]
        public static void FlipSelection()
        {
            var rEnum = SketchControlsScript.GlobalCommands.FlipSelection;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("brushlab.toggle")]
        public static void ToggleBrushLab()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleBrushLab;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("postprocessing.toggle")]
        public static void ToggleCameraPostEffects()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleCameraPostEffects;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("watermark.toggle")]
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
        
        [ApiEndpoint("camerapath.togglevisuals")]
        public static void ToggleCameraPathVisuals()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleCameraPathVisuals;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("camerapath.togglepreview")]
        public static void ToggleCameraPathPreview()
        {
            var rEnum = SketchControlsScript.GlobalCommands.ToggleCameraPathPreview;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("camerapath.delete")]
        public static void DeleteCameraPath()
        {
            var rEnum = SketchControlsScript.GlobalCommands.DeleteCameraPath;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
        
        [ApiEndpoint("camerapath.record")]
        public static void RecordCameraPath()
        {
            var rEnum = SketchControlsScript.GlobalCommands.RecordCameraPath;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }
    }
}


