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
using Org.OpenAPITools.Api;
using Org.OpenAPITools.Client;
using UnityEngine;

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

        [ApiEndpoint(
            "save.as",
            "Saves the current scene under a new filename in the user's Sketches folder. Directory separators, rooted paths, and parent-directory traversal are rejected. The .tilt suffix is optional",
            "newSketch"

        )]
        public static void SaveAs(string filename)
        {
            string suffix = SaveLoadScript.TILT_SUFFIX;
            if (filename.EndsWith(suffix))
            {
                filename = filename.Substring(0, filename.Length - suffix.Length);
            }
            ValidateSafeFilename(filename, "sketch filename");
            var rEnum = SketchControlsScript.GlobalCommands.SaveAs;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, sParam: filename);
        }

        [ApiEndpoint("save.new", "Saves the current scene in a new slot")]
        public static void SaveNew()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SaveNew;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
        }

        [ApiEndpoint("save.selected", "Saves the current selected strokes in a new slot")]
        public static void SaveSelected()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SaveSelected;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
        }

        [ApiEndpoint(
            "postprod.metadata.save",
            "Saves ONLY the post-production preview's metadata (offsets, crop, camera setup) into the named .tilt file in the user's Sketches folder - never touches stroke data, copying it verbatim from the existing file instead of re-deriving it from live scene state. Safe regardless of scrub/playback state, unlike save.overwrite/save.new - those were found to silently truncate strokes when saved while sketch.scrub's playback timeline was mid-position (a real incident, 2026-09-08: num_strokes dropped 50 -> 14). Check query.postprod.metadata.save.status for the real result - HTTP 200 alone doesn't confirm success. Directory separators, rooted paths, and parent-directory traversal are rejected. The .tilt suffix is optional",
            "Untitled_17"
        )]
        public static void SavePostProductionMetadata(string filename)
        {
            if (!filename.EndsWith(SaveLoadScript.TILT_SUFFIX))
            {
                filename += SaveLoadScript.TILT_SUFFIX;
            }
            string path = GetSafePathInDirectory(App.UserSketchPath(), filename, "sketch filename");
            PostProductionMetadataSaver.Save(path);
        }

#if UNITY_EDITOR
        // Editor only for now to help with testing/debugging
        // Unsure whether including these in builds would be a security risk.
        [ApiEndpoint("icosa.devicelogin", "Login to the Icosa Gallery using a device code")]
        public static void IcosaDeviceLogin(string code)
        {
            VrAssetService.m_Instance.IcosaDeviceLogin(code);
        }

        [ApiEndpoint("icosa.logout", "Logout of the Icosa Gallery")]
        public static void IcosaLogout()
        {
            App.IcosaUserName = null;
            App.IcosaUserId = null;
            App.IcosaUserIcon = null;
            App.Instance.IcosaToken = null;
        }
#endif

        [ApiEndpoint("icosa.upload", "Uploads it to the Icosa Gallery")]
        public static void IcosaUpload()
        {
            var rEnum = SketchControlsScript.GlobalCommands.UploadToGenericCloud;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, (int)Cloud.Icosa);
        }

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

        [ApiEndpoint(
            "load.user",
            "Loads the sketch from the user's sketch folder given an index (0 being most recent)",
            "2"
        )]
        public static void LoadUser(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 0);
        }

        [ApiEndpoint("load.featured",
            "Loads the sketch in the given slot number from the featured sketch list",
            "2"
        )]
        public static void LoadFeatured(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 1);
        }

        [ApiEndpoint(
            "load.liked",
            "Loads the sketch in the given slot number from the user's liked sketches",
            "2"
        )]
        public static void LoadLiked(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 2);
        }

        [ApiEndpoint(
            "load.drive",
            "Loads the sketch in the given slot number from the user's Google Drive",
            "2"
        )]
        public static void LoadDrive(int slot)
        {
            var rEnum = SketchControlsScript.GlobalCommands.Load;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, slot, 3);
        }

        [ApiEndpoint(
            "load.named",
            "Loads a sketch filename from the user's Sketches folder. Directory separators, rooted paths, and parent-directory traversal are rejected. The .tilt suffix is optional",
            "Untitled_1"
        )]
        public static void LoadNamedFile(string filename)
        {
            LoadNamedFile(filename, realTime: false);
        }

        [ApiEndpoint(
            "load.realtime",
            "Loads a sketch filename from the user's Sketches folder using recorded wall-clock timing. Sketches without complete wall-clock metadata fall back to ordinary timestamp playback. Directory separators, rooted paths, and parent-directory traversal are rejected. The .tilt suffix is optional",
            "Untitled_1"
        )]
        public static void LoadNamedFileRealTime(string filename)
        {
            LoadNamedFile(filename, realTime: true);
        }

        [ApiEndpoint(
            "sketch.currenttime",
            "Sets the current sketch playback time, in milliseconds. Strokes are added or removed to match what existed in the sketch at that moment, using the same timeline as each stroke's control-point timestamps. Requires sketch.scrub to have been enabled, otherwise there is no playback timeline to seek",
            "5000"
        )]
        public static void SetCurrentSketchTimeMs(int ms)
        {
            PostProductionExport.ThrowIfExporting("sketch.currenttime");
            // Only IScenePlayback.Update() acts on CurrentSketchTime, and the playback object
            // is torn down once a sketch finishes loading - so without it this call would set a
            // value that nothing reads and appear to do nothing. Fail loudly instead.
            if (!SketchMemoryScript.m_Instance.IsPlayingBack)
            {
                throw new System.InvalidOperationException(
                    "sketch.currenttime: there is no active playback timeline to seek. " +
                    "Call sketch.scrub=1 first (it works before or after loading a sketch).");
            }
            App.Instance.CurrentSketchTime = ms / 1000.0;
        }

        [ApiEndpoint(
            "postprod.preview.videotime",
            "Sets the sketch clock so its mapped C006 video time equals videoTimeMs (inverse of the sketch-time-to-video mapping, using postprod.offset.time.global) - lets you drive the tilt FROM a real-footage moment instead of always scrubbing the tilt and reading off the resulting video time. Requires sketch.scrub=1, same as sketch.currenttime",
            "49167"
        )]
        public static void SetPostProdPreviewVideoTimeMs(int videoTimeMs)
        {
            double sketchTimeMs = videoTimeMs - PostProductionSettings.GlobalTimeOffsetMs;
            SetCurrentSketchTimeMs(Mathf.RoundToInt((float)sketchTimeMs));
        }

        [ApiEndpoint(
            "postprod.preview.videoframe",
            "Sets the sketch clock so its mapped C006 video frame equals c006Frame - same as postprod.preview.videotime but in native (60fps) frame numbers, matching the depth_frames/color_frames naming convention. Requires sketch.scrub=1, same as sketch.currenttime",
            "2950"
        )]
        public static void SetPostProdPreviewVideoFrame(int c006Frame)
        {
            double sketchTimeMs = PostProductionSettings.C006FrameToSketchTimeMs(c006Frame);
            SetCurrentSketchTimeMs(Mathf.RoundToInt((float)sketchTimeMs));
        }

        [ApiEndpoint(
            "postprod.crop.start",
            "Sets the video crop start frame (C006 native frame number) - the first frame of the real-footage plate actually used for the final render. Part of the in-app post-production compositing preview; not yet consumed by anything (stage 9 final render isn't built), but persisted with the sketch (stage 8). See postprod.crop.clear to reset back to \"use the full available range\"",
            "2950"
        )]
        public static void SetPostProdCropStartFrame(int frame)
        {
            PostProductionExport.ThrowIfExporting("postprod.crop.start");
            PostProductionSettings.CropStartFrame = frame;
        }

        [ApiEndpoint(
            "postprod.crop.end",
            "Sets the video crop end frame (C006 native frame number) - the last frame of the real-footage plate actually used for the final render. See postprod.crop.start",
            "8830"
        )]
        public static void SetPostProdCropEndFrame(int frame)
        {
            PostProductionExport.ThrowIfExporting("postprod.crop.end");
            PostProductionSettings.CropEndFrame = frame;
        }

        [ApiEndpoint(
            "postprod.crop.clear",
            "Clears both video crop start/end frames back to \"use the full available range\"",
            ""
        )]
        public static void ClearPostProdCrop()
        {
            PostProductionExport.ThrowIfExporting("postprod.crop.clear");
            PostProductionSettings.CropStartFrame = null;
            PostProductionSettings.CropEndFrame = null;
        }

        [ApiEndpoint(
            "sketch.scrub",
            "Keeps the sketch playback timeline alive so sketch.currenttime can seek to any moment, forwards or backwards. Normally the timeline is discarded once a sketch has finished loading, which makes seeking silently do nothing. Safe to call before or after loading: if the timeline has already been discarded it is rebuilt. Seeks are incremental, so stepping forward one frame only renders the strokes that began in that interval",
            "1"
        )]
        public static void SetSketchScrubbing(bool enable)
        {
            if (enable) { _ = PostProductionPreviewController.Instance; }
            var sketchMemory = SketchMemoryScript.m_Instance;
            sketchMemory.m_KeepPlaybackAliveForScrubbing = enable;

            // If a sketch is already loaded its timeline has been torn down, so rebuild it -
            // otherwise enabling this after loading would appear to work but still not seek.
            if (enable && !sketchMemory.IsPlayingBack && sketchMemory.GetMemoryList.Count > 0)
            {
                sketchMemory.RestartScrubPlayback();
            }
        }

        [ApiEndpoint(
            "app.timescale",
            "Sets Unity's global time scale. Use 0 to pause real-time-driven systems (including realtime sketch playback drift) so sketch.currenttime holds exactly where it's set between calls, instead of continuing to advance with wall-clock time. Use 1 to resume normal real-time playback. Same technique already used internally by the multi-cam capture rig for precise, non-drifting captures",
            "0"
        )]
        public static void SetTimeScale(float scale)
        {
            UnityEngine.Time.timeScale = scale;
        }

        [ApiEndpoint(
            "postprod.offset.time.global",
            "Sets the global temporal offset (milliseconds) applied when mapping sketch time to the real-footage plate/depth frame, on top of each layer's own offset. May be negative if the tilt is ahead of the video. Part of the in-app post-production compositing preview",
            "-120"
        )]
        public static void SetPostProdGlobalTimeOffset(int ms)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.time.global");
            PostProductionSettings.GlobalTimeOffsetMs = ms;
        }

        [ApiEndpoint(
            "postprod.offset.time.layer",
            "Sets a per-layer (per-dancer) time offset in milliseconds, on top of the global offset: video time = sketch time + global + layer. The layer's strokes are redrawn shifted by this amount (the video plate follows the global offset only). May be negative. Rebuilds the scrub timeline, keeping the current sketch time",
            "1,-40"
        )]
        public static void SetPostProdLayerTimeOffset(int layer, int ms)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.time.layer");
            if (PostProductionSettings.GetLayerTimeOffsetMs(layer) == ms) { return; }
            PostProductionSettings.SetLayerTimeOffsetMs(layer, ms);
            SketchMemoryScript.m_Instance.RebuildPlaybackKeepingTime();
        }

        [ApiEndpoint(
            "postprod.offset.spatial.global",
            "Sets the global spatial offset (position, Euler rotation, uniform scale) composed with the scene's real pose when previewing/rendering the post-production composite. Never modifies the scene's actual pose",
            "0,0,0,0,0,0,1"
        )]
        public static void SetPostProdGlobalSpatialOffset(Vector3 position, Vector3 rotation, float scale)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.global");
            PostProductionSettings.GlobalSpatialOffset =
                TrTransform.TRS(position, Quaternion.Euler(rotation), scale);
        }

        [ApiEndpoint(
            "postprod.offset.spatial.global.quat",
            "Same as postprod.offset.spatial.global but takes a quaternion (x,y,z,w) instead of Euler angles",
            "0,0,0,0,0,0,1,1"
        )]
        public static void SetPostProdGlobalSpatialOffsetQuat(Vector3 position, Quaternion rotation, float scale)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.global.quat");
            PostProductionSettings.GlobalSpatialOffset = TrTransform.TRS(position, rotation, scale);
        }

        [ApiEndpoint(
            "postprod.offset.spatial.layer",
            "Sets a per-layer spatial offset (position, Euler rotation, uniform scale), composed with the global offset and that layer's real pose when previewing/rendering it in the post-production composite. Never modifies the layer's actual pose",
            "1,0,0,0,0,0,0,1"
        )]
        public static void SetPostProdLayerSpatialOffset(
            int layer, Vector3 position, Vector3 rotation, float scale)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.layer");
            PostProductionSettings.SetLayerSpatialOffset(
                layer, TrTransform.TRS(position, Quaternion.Euler(rotation), scale));
        }

        [ApiEndpoint(
            "postprod.offset.spatial.layer.quat",
            "Same as postprod.offset.spatial.layer but takes a quaternion (x,y,z,w) instead of Euler angles",
            "1,0,0,0,0,0,0,1,1"
        )]
        public static void SetPostProdLayerSpatialOffsetQuat(
            int layer, Vector3 position, Quaternion rotation, float scale)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.layer.quat");
            PostProductionSettings.SetLayerSpatialOffset(layer, TrTransform.TRS(position, rotation, scale));
        }

        [ApiEndpoint(
            "postprod.plate.load",
            "Loads the real-footage preview proxy video and depth EXR sequence for the post-production compositing preview. videoPath and depthFramesDir are absolute paths",
            "D:/Alison-Goodyear-Postproduction/OpenBrushMedia/C006/proxy_c006.mp4,D:/Alison-Goodyear-Postproduction/OpenBrushMedia/C006/depth_frames"
        )]
        public static void LoadPostProdPlate(string videoPath, string depthFramesDir)
        {
            PostProductionMediaLoader.Instance.LoadClip(videoPath, depthFramesDir);
        }

        [ApiEndpoint(
            "postprod.plate.seek",
            "Seeks the post-production preview's proxy video and loads the matching real-footage depth EXR frame, addressed by C006 native video frame number",
            "2960"
        )]
        public static void SeekPostProdPlate(int c006Frame)
        {
            PostProductionMediaLoader.Instance.SeekToC006Frame(c006Frame);
        }

        [ApiEndpoint(
            "postprod.preview",
            "Toggles the post-production live preview on the camera already painting the window (stage 6): applies the calibrated intrinsics/clip set via camera.intrinsics/camera.clip. 0 restores normal viewing",
            "1"
        )]
        public static void SetPostProdPreview(bool enable)
        {
            PostProductionPreviewController.Instance.SetEnabled(enable);
        }

        [ApiEndpoint(
            "postprod.preview.camera",
            "Sets which camera postprod.preview drives, by exact GameObject name (see query.postprod.debug.allcameras for a list). Empty name reverts to the default, App.VrSdk.GetVrCamera()",
            "CenterEyeAnchor"
        )]
        public static void SetPostProdPreviewCameraName(string name)
        {
            PostProductionPreviewController.SetTargetCameraName(name);
        }

        [ApiEndpoint(
            "postprod.preview.pose",
            "Sets the calibrated extrinsics (real-camera position, quaternion x,y,z,w) for postprod.preview's target camera - applied the same way app.snapshot.quat positions the capture rig (App.Scene.AsScene, bypassing SanitizePose's tilt protection), reapplied every frame",
            "-0.4743,14.2147,-11.5124,0.070337,0.003137,0.001319,0.997518"
        )]
        public static void SetPostProdPreviewPose(Vector3 position, Quaternion rotation)
        {
            PostProductionExport.ThrowIfExporting("postprod.preview.pose");
            PostProductionPreviewController.SetTargetPose(position, rotation);
        }

        [ApiEndpoint(
            "postprod.preview.refreshcg",
            "Captures a fresh CG colour+depth snapshot from the calibrated pose (set via postprod.preview.pose) at the current sketch time, to a fixed filename (see query.postprod.preview.status for the paths). Reuses the existing snapshot mechanism (app.snapshot.quat) rather than a separate live-capture path - see POSTPROD_PREVIEW_PLAN.md's stage 6 notes",
            "4096,2160"
        )]
        public static void RefreshPostProdCgCapture(int width, int height)
        {
            PostProductionPreviewController.RefreshCgCapture(width, height);
        }

        [ApiEndpoint(
            "postprod.preview.plate",
            "Sets the plate colour/depth file paths the live preview composites against. Once set (and a CG capture exists via postprod.preview.refreshcg), postprod.preview shows the full depth-aware composite instead of just the raw calibrated CG view",
            "C:/plate.png,C:/plate_depth.exr"
        )]
        public static void SetPostProdPreviewPlate(string colorPath, string depthPath)
        {
            PostProductionPreviewController.SetPlate(colorPath, depthPath);
        }

        [ApiEndpoint(
            "postprod.preview.autoplate",
            "Stage 2: keeps the plate synced to the current sketch time automatically (using postprod.offset.time.global and the clip's depth_frames/manifest.json), instead of computing and setting postprod.preview.plate by hand for every scrub. clipDir must contain color_frames/ and depth_frames/ subfolders using the true-frame-number naming convention. Empty string disables it",
            "D:/Alison-Goodyear-Postproduction/OpenBrushMedia/C006"
        )]
        public static void SetPostProdPreviewAutoPlate(string clipDir)
        {
            PostProductionPreviewController.SetAutoPlateClipDir(clipDir);
        }

        [ApiEndpoint(
            "postprod.clip.load",
            "Loads a clip's whole setup from <clipDir>/clip.json (clipDir relative to the Media Library, e.g. PostProd/C006, as listed by query.postprod.list.clips, or absolute), written by preprocessing (make_clip_manifest.py): camera intrinsics, lens distortion, camera pose (ChArUco board corner as origin), near/far, source fps, and the plate folders for auto-plate and fast plates. The sketch saves only the folder (postprod.metadata.save) and re-reads the manifest when loaded. Result in query.postprod.clip.status",
            "PostProd/C006"
        )]
        public static void LoadPostProdClip(string clipDir)
        {
            PostProductionExport.ThrowIfExporting("postprod.clip.load");
            if (PostProductionClip.Load(clipDir) == null)
            {
                PostProductionPreviewController.StartPreviewForClip();
            }
        }

        [ApiEndpoint(
            "postprod.load",
            "Loads a sketch from the user's Sketches folder for post-production: real-time playback, scrubbing on (sketch.scrub), and no load animation (a quick load). Once loaded, the playhead goes to the export range start, else the first frame of the clip that can be composited, else the start of the sketch. A sketch saved with a clip opens straight into the preview. The .tilt suffix is optional",
            "Untitled_17"
        )]
        public static void LoadPostProdSketch(string filename)
        {
            PostProductionExport.ThrowIfExporting("postprod.load");
            // Before the load, so the new playback is kept alive (no need to rebuild the old one).
            SketchMemoryScript.m_Instance.m_KeepPlaybackAliveForScrubbing = true;
            LoadNamedFile(filename, realTime: true, quickload: true);
            PostProductionPreviewController.MovePlayheadAfterLoad();
        }

        [ApiEndpoint(
            "postprod.export.start",
            "Exports the depth-aware composite at full quality over the trim range (postprod.crop.start/end), matching the original lens: the plate is decoded from the clip's source video, the CG is rendered at full resolution and distorted back into the real lens. Written to <clipDir>/exports, with the source's audio for the same frames. format: source (match the source: its ProRes flavour, or ProRes 422 HQ if it isn't ProRes, with its colour, timecode and audio), prores422hq or h264. Progress, time remaining and errors in query.postprod.export.status",
            "source"
        )]
        public static void StartPostProdExport(string format)
        {
            string error = PostProductionExport.Start(format);
            if (error != null) { throw new System.InvalidOperationException("postprod.export.start: " + error); }
        }

        [ApiEndpoint(
            "postprod.export.reveal",
            "Shows the last finished post-production export selected in Explorer, or the clip's exports folder (Windows)",
            ""
        )]
        public static void RevealPostProdExport()
        {
            PostProductionExport.Reveal();
        }

        [ApiEndpoint(
            "postprod.export.cancel",
            "Cancels a running post-production export and deletes the partial file",
            ""
        )]
        public static void CancelPostProdExport()
        {
            PostProductionExport.Cancel();
        }

        [ApiEndpoint(
            "postprod.preview.naivemode",
            "Toggles the live preview's compositing mode: naive (CG always shown where it has geometry, no depth test) vs depth-aware (default, off)",
            "1"
        )]
        public static void SetPostProdPreviewNaiveMode(bool naive)
        {
            PostProductionPreviewController.SetNaiveMode(naive);
        }

        [ApiEndpoint(
            "postprod.preview.livecg",
            "Fast preview: renders the calibrated CG colour and depth live every frame into low-res render textures and composites them directly (no snapshot files), instead of using the last postprod.preview.refreshcg capture. 0 returns to the snapshot path. See POSTPROD_FAST_PREVIEW_DESIGN.md",
            "1"
        )]
        public static void SetPostProdPreviewLiveCg(bool enabled)
        {
            PostProductionLiveCg.Enabled = enabled;
            if (!enabled) { PostProductionLiveCg.LastStatus = "live CG off"; }
        }

        [ApiEndpoint(
            "postprod.preview.livecg.resolution",
            "Sets a fixed live CG render resolution. By default (or with 0,0) it matches the displayed camera's pixel size, which avoids stair-stepped stroke edges from upscaling",
            "0,0"
        )]
        public static void SetPostProdPreviewLiveCgResolution(int width, int height)
        {
            PostProductionLiveCg.SetResolution(width, height);
        }

        [ApiEndpoint(
            "postprod.offset.spatial.nudge",
            "Nudges a spatial offset relative to the calibrated camera's view. target: -1 = global (whole sketch), 0..N-1 = that layer. forward/back (metres, along the camera's view direction flattened onto the floor), right/left (metres, camera's horizontal right axis), yaw (degrees, about the vertical axis through a floor pivot under the centre of the target's strokes). Composes onto the stored offset. Readout in query.postprod.offset.spatial.readout=<target>",
            "-1,0.05,0,0"
        )]
        public static void NudgePostProdSpatialOffset(int target, float forwardMetres, float rightMetres, float yawDegrees)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.nudge");
            PostProductionAlignment.Nudge(target, forwardMetres, rightMetres, yawDegrees);
        }

        [ApiEndpoint(
            "postprod.preview.shuttle",
            "Plays the sketch clock at a rate driven inside Open Brush each frame: 1 = real time, 0.25 = quarter speed, negative = reverse, 0 = hold still. Only the sketch clock is controlled; Unity's game clock keeps running (and is set back to normal speed), so lighting and animation behave. Seeks from elsewhere are adopted. Requires sketch.scrub=1",
            "0.25"
        )]
        public static void SetPostProdPreviewShuttle(float rate)
        {
            PostProductionExport.ThrowIfExporting("postprod.preview.shuttle");
            if (rate != 0f && !SketchMemoryScript.m_Instance.IsPlayingBack)
            {
                throw new System.InvalidOperationException(
                    "postprod.preview.shuttle: no active playback timeline. Call sketch.scrub=1 first.");
            }
            PostProductionPreviewController.SetShuttleRate(rate);
        }

        [ApiEndpoint(
            "postprod.offset.spatial.revert",
            "Reverts a spatial offset to what it was when the sketch was loaded or last saved with postprod.metadata.save (to back out experimental nudges). target: -1 = global, 0..N-1 = that layer",
            "-1"
        )]
        public static void RevertPostProdSpatialOffset(int target)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.revert");
            PostProductionAlignment.Revert(target);
        }

        [ApiEndpoint(
            "postprod.offset.spatial.reset",
            "Clears a spatial offset to none. target: -1 = global, 0..N-1 = that layer",
            "-1"
        )]
        public static void ResetPostProdSpatialOffset(int target)
        {
            PostProductionExport.ThrowIfExporting("postprod.offset.spatial.reset");
            PostProductionAlignment.Reset(target);
        }

        [ApiEndpoint(
            "postprod.lens",
            "Sets the real camera's lens calibration (OpenCV convention): fx,fy,cx,cy in pixels at the camera.intrinsics reference resolution, then distortion k1,k2,p1,p2,k3. Used to undistort the plate (postprod.preview.lens.undistort) into an overscanned working frame. Status in query.postprod.lens.status",
            "3163.53165,3165.07069,2289.44663,1102.92551,-0.0241422,-0.0144468,0.00272838,0.00588623,0.01740761"
        )]
        public static void SetPostProdLens(float fx, float fy, float cx, float cy,
            float k1, float k2, float p1, float p2, float k3)
        {
            PostProductionExport.ThrowIfExporting("postprod.lens");
            PostProductionLens.SetCalibration(fx, fy, cx, cy, k1, k2, p1, p2, k3);
        }

        [ApiEndpoint(
            "postprod.preview.lens.undistort",
            "Works fully undistorted (1): the live CG renders a slightly larger (overscanned) pinhole frame and the plate colour and depth are undistorted into it, so nothing from the real frame is lost. Needs postprod.lens, camera.intrinsics and live CG. 0 composites over the raw distorted plate",
            "1"
        )]
        public static void SetPostProdLensUndistort(bool enabled)
        {
            PostProductionLens.Undistort = enabled;
            PostProductionLens.RecomputeWorkingFrame();
        }

        [ApiEndpoint(
            "postprod.preview.fastplates",
            "Fast preview plates: serves the plate from a RAM cache of the low-res preview set (<clipDir>/preview_1280x676 colour + depth_frames EXRs) around the playhead, instead of loading full-res files on every frame change. Needs postprod.preview.autoplate set first. 0 returns to full-res plates. Result in query.postprod.preview.fastplates.status",
            "1"
        )]
        public static void SetPostProdPreviewFastPlates(bool enabled)
        {
            string error = PostProductionPlateCache.SetEnabled(
                enabled, PostProductionPreviewController.AutoPlateClipDir);
            if (error != null) { PostProductionPlateCache.LastStatus = "error: " + error; }
        }

        [ApiEndpoint(
            "postprod.preview.fastplates.decoders",
            "Sets how many depth frames the fast plate cache decodes at once (each is ~110ms on a fast machine; 1x playback needs 30 a second). 0 restores the default, a quarter of the machine's processor threads, between 2 and 8. Applied straight away if fast plates are on. The count in use is shown in query.postprod.preview.fastplates.status",
            "8"
        )]
        public static void SetPostProdFastPlateDecoders(int count)
        {
            PostProductionPlateCache.SetDepthDecoders(count);
            if (PostProductionPlateCache.Enabled)
            {
                string error = PostProductionPlateCache.SetEnabled(true, PostProductionPreviewController.AutoPlateClipDir);
                if (error != null) { PostProductionPlateCache.LastStatus = "error: " + error; }
            }
        }

        [ApiEndpoint(
            "postprod.preview.livecg.bloom",
            "Turns bloom on (1) or off (0) for the live CG, using the snapshot camera's own bloom effect and settings. Off by default",
            "1"
        )]
        public static void SetPostProdPreviewLiveCgBloom(bool enabled)
        {
            PostProductionExport.ThrowIfExporting("postprod.preview.livecg.bloom");
            PostProductionLiveCg.Bloom = enabled;
        }

        [ApiEndpoint(
            "postprod.depth.filter",
            "How the real footage's depth is sampled, in the preview and the export: nearest (the default: never blends across a depth edge, which would change what is occluded) or bilinear (smoother, for depth that was upscaled smoothly). Saved with the sketch (postprod.metadata.save). Read back in query.postprod.preview.settings",
            "nearest"
        )]
        public static void SetPostProdDepthFilter(string filter)
        {
            PostProductionExport.ThrowIfExporting("postprod.depth.filter");
            switch (filter)
            {
                case "nearest": PostProductionSettings.DepthNearest = true; break;
                case "bilinear": PostProductionSettings.DepthNearest = false; break;
                default:
                    throw new System.ArgumentException(
                        $"postprod.depth.filter: \"{filter}\" is not nearest or bilinear");
            }
        }

        [ApiEndpoint(
            "postprod.debug.livecg.dump",
            "TEMP parity test: writes the current live CG frame to <basePath>.png and <basePath>_depth.exr (metres), decoded exactly as the snapshot path does, for comparison against a snapshot taken at the same resolution. Result in query.postprod.preview.livecg.status",
            "D:/Alison-Goodyear-Postproduction/livecg_parity/live"
        )]
        public static void DumpPostProdLiveCg(string basePath)
        {
            PostProductionLiveCg.LastDumpStatus = PostProductionLiveCg.Dump(basePath);
        }

        [ApiEndpoint(
            "postprod.preview.cgonly",
            "Shows the live calibrated CG view only, with no real-footage plate composited behind it - ignores whatever plate is currently configured (without discarding it, so toggling this off returns to compositing against it). 0 restores normal compositing",
            "1"
        )]
        public static void SetPostProdPreviewCgOnly(bool cgOnly)
        {
            PostProductionPreviewController.SetCgOnlyMode(cgOnly);
        }

        [ApiEndpoint(
            "postprod.debug.dumpcameras",
            "TEMP DEBUG: captures a listing of every enabled Camera in the scene (read via query.postprod.debug.allcameras). Not part of the real feature surface",
            ""
        )]
        public static void DumpAllCameras()
        {
            PostProductionPreviewController.CaptureAllCamerasReport();
        }

        [ApiEndpoint(
            "postprod.compositor.test",
            "TEMP DEBUG / regression test: runs the stage-5 depth-aware compositor on four explicit file paths (cgColorPng, cgDepthExr, plateColorPng, plateDepthExr) and writes the result to outputPng, for comparison against the existing make_composite_video.py output. Not part of the real feature surface",
            "C:/cg.png,C:/cg_depth.exr,C:/plate.png,C:/plate_depth.exr,C:/out.png,false"
        )]
        public static void TestPostProdCompositor(
            string cgColorPath, string cgDepthPath, string plateColorPath, string plateDepthPath,
            string outputPath, bool naiveMode)
        {
            PostProdCompositor.TestFromFiles(
                cgColorPath, cgDepthPath, plateColorPath, plateDepthPath, outputPath, naiveMode);
        }

        [ApiEndpoint(
            "postprod.compositor.debugalign",
            "TEMP DEBUG: renders the depth/colour sampling-alignment visualization (red=CG depth texture says geometry, green=CG colour texture shows a bright stroke, sampled through the real GPU pipeline) to outputPng. Not part of the real feature surface",
            "C:/cg.png,C:/cg_depth.exr,C:/align.png"
        )]
        public static void DebugPostProdCompositorAlignment(
            string cgColorPath, string cgDepthPath, string outputPath)
        {
            PostProdCompositor.DebugDepthColorAlignment(cgColorPath, cgDepthPath, outputPath);
        }

        private static void LoadNamedFile(string filename, bool realTime, bool quickload = false)
        {
            PostProductionExport.ThrowIfExporting("load");
            if (!filename.EndsWith(SaveLoadScript.TILT_SUFFIX))
            {
                filename += SaveLoadScript.TILT_SUFFIX;
            }
            string path = GetSafePathInDirectory(App.UserSketchPath(), filename, "sketch filename");
            // A playback kept alive by sketch.scrub must be finished while the old strokes'
            // layers still exist, as New Sketch does: the load resets the layers before its own
            // ClearMemory, which would then finish the playback into destroyed geometry.
            var sketchMemory = SketchMemoryScript.m_Instance;
            if (sketchMemory.m_KeepPlaybackAliveForScrubbing && sketchMemory.IsPlayingBack)
            {
                sketchMemory.ClearMemory();
            }
            if (realTime)
            {
                SketchControlsScript.m_Instance.LoadNamedFileWithPlaybackMode(
                    path, SketchMemoryScript.PlaybackMode.RealTime, quickload);
            }
            else
            {
                SketchControlsScript.m_Instance.IssueGlobalCommand(
                    SketchControlsScript.GlobalCommands.LoadNamedFile,
                    (int)SketchControlsScript.LoadSpeed.Quick,
                    -1,
                    path
                );
            }
            PanelManager.m_Instance.ToggleSketchbookPanels(true);
        }

        [ApiEndpoint(
            "merge.named",
            "Merges a sketch filename from the user's Sketches folder into the current sketch. Directory separators, rooted paths, and parent-directory traversal are rejected. The .tilt suffix is optional",
            "Untitled_1"
        )]
        public static void MergeNamedFile(string filename)
        {
            if (!filename.EndsWith(SaveLoadScript.TILT_SUFFIX))
            {
                filename += SaveLoadScript.TILT_SUFFIX;
            }
            string path = GetSafePathInDirectory(App.UserSketchPath(), filename, "sketch filename");
            SketchControlsScript.m_Instance.IssueGlobalCommand(
                SketchControlsScript.GlobalCommands.LoadNamedFile,
                (int)SketchControlsScript.LoadSpeed.Quick,
                1,
                path
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
            var spectator = SketchControlsScript.m_Instance.GetDropCampWidget();
            ShowSpectatorInstantly(spectator, !spectator.gameObject.activeSelf);
        }

        [ApiEndpoint("spectator.on", "Turns the spectator camera on")]
        public static void EnableSpectator()
        {
            ShowSpectatorInstantly(SketchControlsScript.m_Instance.GetDropCampWidget(), true);
        }

        [ApiEndpoint("spectator.off", "Turns the spectator camera off")]
        public static void DisableSpectator()
        {
            ShowSpectatorInstantly(SketchControlsScript.m_Instance.GetDropCampWidget(), false);
        }

        private static void ShowSpectatorInstantly(DropCamWidget spectator, bool show)
        {
            spectator.ClearVelocities();
            spectator.ShowInstantly(show);
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
            // TODO Do we need to support the other SketchSetTypes?
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, index, (int)SketchSetType.User);
        }

        [ApiEndpoint("guides.disable", "Toggles guides on and off")]
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

        [ApiEndpoint(
            "profiling.start",
            "Starts profiling. Mode can be standard, light, or deep. Optional second value sets the profile label.",
            "standard,rendergraph_normal_core")]
        public static string StartProfiling(string mode = "standard", string profileName = null)
        {
            ProfilingManager profilingManager = ProfilingManager.Instance;
            if (profilingManager.IsProfiling)
            {
                return "Profiling is already running.";
            }

            ProfilingManager.Mode profilingMode = ParseProfilingMode(mode);
            profilingManager.StartProfiling(profilingMode, profileName);
            return string.IsNullOrEmpty(profileName)
                ? $"Started {profilingMode} profiling."
                : $"Started {profilingMode} profiling for '{profileName}'.";
        }

        [ApiEndpoint("profiling.stop", "Stops profiling and writes the summary output")]
        public static string StopProfiling()
        {
            ProfilingManager profilingManager = ProfilingManager.Instance;
            if (!profilingManager.IsProfiling)
            {
                return "Profiling is not running.";
            }

            profilingManager.StopProfiling();
            return "Stopped profiling.";
        }

        private static ProfilingManager.Mode ParseProfilingMode(string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                return ProfilingManager.Mode.Standard;
            }

            if (System.Enum.TryParse(mode, ignoreCase: true, out ProfilingManager.Mode parsedMode))
            {
                return parsedMode;
            }

            UnityEngine.Debug.LogWarning($"[OB_PERF] Unknown profiling mode '{mode}', using Standard.");
            return ProfilingManager.Mode.Standard;
        }

        // // TODO Do we need this?
        // [ApiEndpoint("autoprofile", "Runs autoprofile")]
        // public static void DoAutoProfile()
        // {
        //     var rEnum = SketchControlsScript.GlobalCommands.DoAutoProfile;
        //     SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        // }

        [ApiEndpoint("bloom.amount", "Sets bloom amount from 0 (off) to 1 for this session", "1")]
        public static string SetBloomAmount(float amount)
        {
            if (UrpPostProcessingController.Instance == null)
                return "error: URP post-processing is not available.";
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f || amount > 1f)
                return "error: Bloom amount must be finite and between 0 and 1.";

            UrpPostProcessingController.Instance.SetBloomAmount(amount);
            return $"Bloom amount={amount}.";
        }

        [ApiEndpoint("bloom.threshold", "Sets a non-negative bloom threshold for this session", "0.5")]
        public static string SetBloomThreshold(float threshold)
        {
            if (UrpPostProcessingController.Instance == null)
                return "error: URP post-processing is not available.";
            try
            {
                UrpPostProcessingController.Instance.SetBloomThreshold(threshold);
            }
            catch (System.ArgumentException e)
            {
                return $"error: {e.Message}";
            }
            return $"Bloom threshold={threshold}.";
        }

        [ApiEndpoint("quality.get", "Returns the current quality level")]
        public static string GetQualityLevel()
        {
            if (QualityControls.m_Instance == null)
            {
                return "QualityControls is not available.";
            }

            var quality = QualityControls.m_Instance;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var display = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager?.activeLoader
                ?.GetLoadedSubsystem<UnityEngine.XR.XRDisplaySubsystem>();
            var foveationFeature = UnityEngine.XR.OpenXR.OpenXRSettings.Instance
                ?.GetFeature<UnityEngine.XR.OpenXR.Features.FoveatedRenderingFeature>();
            return $"Quality level={quality.QualityLevel} levels={quality.AppQualityLevels.Length} " +
                $"automatic={quality.AutomaticQualityEnabled} fps={quality.FramesInLastSecond} " +
                $"viewportRequested={UnityEngine.XR.XRSettings.renderViewportScale} " +
                $"viewportApplied={UnityEngine.XR.XRSettings.appliedRenderViewportScale} " +
                $"foveationFeature={foveationFeature?.enabled} foveationLevel={display?.foveatedRenderingLevel} " +
                $"foveationCaps={UnityEngine.SystemInfo.foveatedRenderingCaps} " +
                $"sessionMsaa={quality.MSAALevel} pipelineMsaa={pipeline?.msaaSampleCount} " +
                $"unityMsaa={UnityEngine.QualitySettings.antiAliasing} " +
                $"xrCachedMsaa={UnityEngine.Experimental.Rendering.XRSystem.GetDisplayMSAASamples()}.";
        }

        [ApiEndpoint("quality.auto", "Enables or pauses automatic quality on mobile hardware", "false")]
        public static string SetAutomaticQuality(bool enabled)
        {
            if (QualityControls.m_Instance == null) return "QualityControls is not available.";
            if (enabled && !QualityControls.m_Instance.SupportsAutomaticQuality)
                return "error: Automatic quality is only available on mobile hardware.";
            QualityControls.m_Instance.AutomaticQualityEnabled = enabled;
            return GetQualityLevel();
        }

        [ApiEndpoint("quality.configure", "Sets a level's foveation; MSAA must match the fixed session value", "3,4,0")]
        public static string ConfigureQualityLevel(int level, int msaa, int foveation)
        {
            if (QualityControls.m_Instance == null) return "QualityControls is not available.";
            try
            {
                QualityControls.m_Instance.ConfigureQualityLevel(level, msaa, foveation);
            }
            catch (System.ArgumentException e)
            {
                return $"error: {e.Message}";
            }
            catch (System.InvalidOperationException e)
            {
                return $"error: {e.Message}";
            }
            return $"Configured level={level} foveation={foveation}; session MSAA remains {msaa}x.";
        }

        [ApiEndpoint("quality.thresholds", "Sets lower/higher FPS and frame counts for this session", "65,70,30,45")]
        public static string ConfigureQualityThresholds(float lowerFps, float higherFps,
            int lowerFrames, int higherFrames)
        {
            if (QualityControls.m_Instance == null) return "QualityControls is not available.";
            try
            {
                QualityControls.m_Instance.ConfigureQualityThresholds(lowerFps, higherFps, lowerFrames, higherFrames);
            }
            catch (System.ArgumentException e)
            {
                return $"error: {e.Message}";
            }
            return $"Configured lower={lowerFps}/{lowerFrames} higher={higherFps}/{higherFrames} for this session.";
        }

        [ApiEndpoint("quality.set", "Sets the current quality level by index", "2")]
        public static string SetQualityLevel(int level)
        {
            if (QualityControls.m_Instance == null)
            {
                return "QualityControls is not available.";
            }

            int maxLevel = QualityControls.m_Instance.AppQualityLevels.Length - 1;
            if (level < 0 || level > maxLevel)
            {
                return $"Quality level {level} is out of range 0..{maxLevel}.";
            }

            QualityControls.m_Instance.AutomaticQualityEnabled = false;
            QualityControls.m_Instance.QualityLevel = level;
            return $"Quality level set to {QualityControls.m_Instance.QualityLevel}.";
        }
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

        [ApiEndpoint("select.all", "Selects all strokes and widgets on the current layer")]
        public static void SelectAll()
        {
            var rEnum = SketchControlsScript.GlobalCommands.SelectAll;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum);
        }

        [ApiEndpoint("select.none", "Deselects all strokes and widgets on the current layer")]
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

        [ApiEndpoint("camerapath.setactive", "Sets the active camera path")]
        public static void SetActiveCameraPath(int index)
        {
            var widget = _GetActiveCameraPath(index);
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
        }
    }
}
