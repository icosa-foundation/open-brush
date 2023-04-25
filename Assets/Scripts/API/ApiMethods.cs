// Copyright 2021 The Open Brush Authors
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
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // ReSharper disable once UnusedType.Global
    public static partial class ApiMethods
    {

        // Example of calling a command and recording an undo step
        // [ApiEndpoint("foo", "")]
        // public static void FooCommand()
        // {
        //     FooCommand cmd = new FooCommand();
        //     SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        // }

        [ApiEndpoint("listenfor.strokes", "Adds the url of an app that wants to receive the data for a stroke as each one is finished")]
        public static void AddListener(string url)
        {
            ApiManager.Instance.AddOutgoingCommandListener(new Uri(url));
        }

        [ApiEndpoint("showfolder.scripts", "Opens the user's Scripts folder on the desktop")]
        public static void OpenUserScriptsFolder()
        {
            OpenUserFolder(ApiManager.Instance.UserScriptsPath());
        }

        [ApiEndpoint("showfolder.exports", "Opens the user's Exports folder on the desktop")]
        public static void OpenExportFolder()
        {
            OpenUserFolder(App.UserExportPath());
        }

        private static void OpenUserFolder(string path)
        {
            // Launch external window and tell the user we did so
            // TODO This call is windows only
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    "Folder opened on desktop", fPopScalar: 0.5f
                );
                System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
            }
        }

        [ApiEndpoint("spectator.move.to", "Moves the spectator camera to the given position")]
        public static void MoveSpectatorTo(Vector3 position)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.position = position;
        }

        [ApiEndpoint("user.move.to", "Moves the user to the given position")]
        public static void MoveUserTo(Vector3 position)
        {
            TrTransform pose = App.Scene.Pose;
            pose.translation = position;
            float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
            App.Scene.Pose = pose;
        }

        [ApiEndpoint("spectator.move.by", "Moves the spectator camera by the given amount")]
        public static void MoveSpectatorBy(Vector3 amount)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.position += amount;
        }

        [ApiEndpoint("user.move.by", "Moves the user by the given amount")]
        public static void MoveUserBy(Vector3 amount)
        {
            TrTransform pose = App.Scene.Pose;
            pose.translation -= amount;
            float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
            App.Scene.Pose = pose;
        }

        [ApiEndpoint("spectator.turn.y", "Rotates the spectator camera left or right.")]
        public static void SpectatorYaw(float angle)
        {
            _ChangeSpectatorBearing(angle, Vector3.up);
        }

        [ApiEndpoint("spectator.turn.x", "Rotates the spectator camera up or down.")]
        public static void SpectatorPitch(float angle)
        {
            _ChangeSpectatorBearing(angle, Vector3.left);
        }

        [ApiEndpoint("spectator.turn.z", "Tilts the angle of the spectator camera clockwise or anticlockwise.")]
        public static void SpectatorRoll(float angle)
        {
            _ChangeSpectatorBearing(angle, Vector3.forward);
        }

        // [ApiEndpoint("user.turn.y", "Rotates the user camera left or right.")]
        // public static void UserYaw(float angle)
        // {
        //     ChangeUserBearing(angle, Vector3.up);
        // }

        // [ApiEndpoint("user.turn.x", "Rotates the user camera up or down. (monoscopic mode only)")]
        // public static void UserPitch(float angle)
        // {
        //     ChangeUserBearing(angle, Vector3.left);
        // }

        // [ApiEndpoint("user.turn.z", "Tilts the angle of the user camera clockwise or anticlockwise. (monoscopic mode only)")]
        // public static void UserRoll(float angle)
        // {
        //     ChangeUserBearing(angle, Vector3.forward);
        // }

        [ApiEndpoint("spectator.direction", "Points the spectator camera to look in the specified direction. Angles are given in x,y,z degrees")]
        public static void SpectatorDirection(Vector3 direction)
        {
            TrTransform lookPose = App.Scene.Pose;
            Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
            lookPose.rotation = qNewRotation;
            App.Scene.Pose = lookPose;
        }

        // [ApiEndpoint("user.direction", "Points the user camera to look in the specified direction. Angles are given in x,y,z degrees. (Monoscopic mode only)")]
        // public static void UserDirection(Vector3 direction)
        // {
        //     TrTransform lookPose = App.Scene.Pose;
        //     Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
        //     lookPose.rotation = qNewRotation;
        //     App.Scene.Pose = lookPose;
        // }

        [ApiEndpoint("spectator.look.at", "Points the spectator camera towards a specific point")]
        public static void SpectatorLookAt(Vector3 position)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.LookAt(position);
        }

        // [ApiEndpoint("user.look.at", "Points the user camera towards a specific point (In VR this only changes the y axis. In monoscopic mode it changes all 3 axes)")]
        // public static void UserLookAt(Vector3 position)
        // {
        //     TrTransform lookPose = App.Scene.Pose;
        //     Quaternion qNewRotation = Quaternion.Euler(direction.x, direction.y, direction.z);
        //     lookPose.rotation = qNewRotation;
        //     App.Scene.Pose = lookPose;
        // }

        [ApiEndpoint("spectator.mode", "Sets the spectator camera mode to one of Stationary, SlowFollow, Wobble, Circular")]
        public static void SpectatorMode(string mode)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            switch (mode.ToLower())
            {
                case "stationary":
                    cam.SetMode(DropCamWidget.Mode.Stationary);
                    break;
                case "slowfollow":
                    cam.SetMode(DropCamWidget.Mode.SlowFollow);
                    break;
                case "wobble":
                    cam.SetMode(DropCamWidget.Mode.Wobble);
                    break;
                case "circular":
                    cam.SetMode(DropCamWidget.Mode.Circular);
                    break;
            }
        }

        [ApiEndpoint("spectator.show", "Unhides the chosen type of elements from the spectator camera (widgets, strokes, selection, headset, panels, ui")]
        public static void SpectatorShow(string thing)
        {
            _SpectatorShowHide(thing, true);
        }

        [ApiEndpoint("spectator.hide", "Hides the chosen type of elements from the spectator camera (widgets, strokes, selection, headset, panels, ui")]
        public static void SpectatorHide(string thing)
        {
            _SpectatorShowHide(thing, false);
        }

        [ApiEndpoint("brush.move.to", "Moves the brush to the given coordinates")]
        public static void BrushMoveTo(Vector3 position)
        {
            ApiManager.Instance.BrushPosition = position;
        }

        [ApiEndpoint("brush.move.to.hand", "Moves the brush to the given hand (l or r")]
        public static void BrushMoveToHand(string hand, bool alsoRotate = false)
        {
            Transform tr;
            if (hand.ToLower().StartsWith("l"))
            {
                tr = InputManager.Wand.Transform;

            }
            else
            {
                tr = PointerManager.m_Instance.MainPointer.transform;
            }

            ApiManager.Instance.BrushPosition = tr.position;

            if (alsoRotate)
            {
                ApiManager.Instance.BrushRotation = tr.rotation;
            }
        }

        [ApiEndpoint("brush.move.by", "Moves the brush by the given amount")]
        public static void BrushMoveBy(Vector3 offset)
        {
            ApiManager.Instance.BrushPosition += offset;
        }

        [ApiEndpoint("brush.move", "Moves the brush forward by 'distance' without drawing a line")]
        public static void BrushMove(float distance)
        {
            var currentPosition = ApiManager.Instance.BrushPosition;
            Vector3 directionVector = ApiManager.Instance.BrushRotation * Vector3.forward;
            var newPosition = currentPosition + (directionVector * distance);
            ApiManager.Instance.BrushPosition = newPosition;
        }

        [ApiEndpoint("brush.draw", "Moves the brush forward by 'distance' and draws a line")]
        public static void BrushDraw(float distance)
        {
            Vector3 directionVector = ApiManager.Instance.BrushRotation * Vector3.forward;
            var end = directionVector * distance;
            var path = new List<List<TrTransform>> {new List<TrTransform>{TrTransform.identity, TrTransform.T(end)}};
            DrawStrokes.DrawNestedTrList(path, TrTransform.T(ApiManager.Instance.BrushPosition));
            ApiManager.Instance.BrushPosition += end;
        }

        [ApiEndpoint("brush.turn.y", "Changes the brush direction to the left or right. Angle is measured in degrees")]
        public static void BrushYaw(float angle)
        {
            _ChangeBrushBearing(angle, Vector3.up);
        }

        [ApiEndpoint("brush.turn.x", "Changes the brush direction up or down. Angle is measured in degrees")]
        public static void BrushPitch(float angle)
        {
            _ChangeBrushBearing(angle, Vector3.left);
        }

        [ApiEndpoint("brush.turn.z", "Rotates the brush clockwise or anticlockwise. Angle is measured in degrees")]
        public static void BrushRoll(float angle)
        {
            _ChangeBrushBearing(angle, Vector3.forward);
        }

        [ApiEndpoint("brush.look.at", "Changes the brush direction to look at the specified point")]
        public static void BrushLookAt(Vector3 direction)
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(direction, Vector3.up);
        }

        [ApiEndpoint("brush.look.forwards", "Changes the brush direction to look forwards")]
        public static void BrushLookForwards()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.forward, Vector3.up);
        }

        [ApiEndpoint("brush.look.up", "Changes the brush direction to look upwards")]
        public static void BrushLookUp()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.up, Vector3.up);
        }

        [ApiEndpoint("brush.look.down", "Changes the brush direction to look downwards")]
        public static void BrushLookDown()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.down, Vector3.up);
        }

        [ApiEndpoint("brush.look.left", "Changes the brush direction to look to the left")]
        public static void BrushLookLeft()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.left, Vector3.up);
        }

        [ApiEndpoint("brush.look.right", "Changes the brush direction to look to the right")]
        public static void BrushLookRight()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.right, Vector3.up);
        }

        [ApiEndpoint("brush.look.backwards", "Changes the brush direction to look backwards")]
        public static void BrushLookBackwards()
        {
            ApiManager.Instance.BrushRotation.SetLookRotation(Vector3.back, Vector3.up);
        }

        [ApiEndpoint("brush.home.reset", "Resets the brush position and direction")]
        public static void BrushHome()
        {
            ApiManager.Instance.ResetBrushTransform();
        }

        [ApiEndpoint("brush.home.set", "Sets the current brush position and direction as the new home. This persists in new sketches")]
        public static void BrushSetHome()
        {
            ApiManager.Instance.BrushOrigin = ApiManager.Instance.BrushPosition;
            ApiManager.Instance.BrushInitialRotation = ApiManager.Instance.BrushRotation;
        }

        [ApiEndpoint("brush.transform.push", "Stores the current brush position and direction on to a stack")]
        public static void BrushTransformPush()
        {
            ApiManager.Instance.BrushTransformStack.Push((ApiManager.Instance.BrushPosition, ApiManager.Instance.BrushRotation));
        }

        [ApiEndpoint("brush.transform.pop", "Pops the most recent current brush position and direction from the stack")]
        public static void BrushTransformPop()
        {
            var (pos, rot) = ApiManager.Instance.BrushTransformStack.Pop();
            BrushMoveTo(pos);
            ApiManager.Instance.BrushRotation = rot;
        }

        [ApiEndpoint("debug.brush", "Logs some info about the brush")]
        public static void DebugBrush()
        {
            Debug.Log($"Brush position: {ApiManager.Instance.BrushPosition}");
            Debug.Log($"Brush rotation: {ApiManager.Instance.BrushRotation.eulerAngles}");
        }

        private static ReferenceImage _LoadReferenceImage(string location)
        {
            location = Path.Combine(App.MediaLibraryPath(), "Images", location);
            var image = new ReferenceImage(location);
            image.SynchronousLoad();
            return image;
        }

        [ApiEndpoint("image.import", "Imports an image given a url or a filename in Media Library\\Images (Images loaded from a url are saved locally first)")]
        public static void ImportImage(string location)
        {
            if (location.StartsWith("http://") || location.StartsWith("https://"))
            {
                location = _DownloadMediaFileFromUrl(location, "Images");
            }

            ReferenceImage image = _LoadReferenceImage(location);
            var tr = new TrTransform();
            tr.translation = ApiManager.Instance.BrushPosition;
            tr.rotation = ApiManager.Instance.BrushRotation;
            var cmd = new CreateWidgetCommand(WidgetManager.m_Instance.ImageWidgetPrefab, tr);

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            var imageWidget = cmd.Widget as ImageWidget;
            if (imageWidget != null)
            {
                imageWidget.ReferenceImage = image;
                imageWidget.Show(true);
                cmd.SetWidgetCost(imageWidget.GetTiltMeterCost());
            }

            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);
        }

        [ApiEndpoint("environment.type", "Sets the current environment")]
        public static void SetEnvironment(string name)
        {
            Environment env = EnvironmentCatalog.m_Instance.AllEnvironments.First(x => x.name == name);
            SceneSettings.m_Instance.SetDesiredPreset(env, false, true);
        }

        [ApiEndpoint("layer.add", "Adds a new layer")]
        public static void AddLayer()
        {
            AddLayerCommand cmd = new AddLayerCommand(makeActive: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("layer.clear", "Clears the contents of a layer")]
        public static void ClearLayer(int layer)
        {
            ClearLayerCommand cmd = new ClearLayerCommand(layer);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);

        }

        [ApiEndpoint("layer.delete", "Deletes a layer")]
        public static void DeleteLayer(int layer)
        {
            DeleteLayerCommand cmd = new DeleteLayerCommand(layer);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("layer.squash", "Move everything from one layer to another then removes the empty layer")]
        public static void SquashLayer(int squashedLayer, int destinationLayer)
        {
            Debug.Log($"squashedLayer {squashedLayer} destinationLayer {destinationLayer}");
            SquashLayerCommand cmd = new SquashLayerCommand(squashedLayer, destinationLayer);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("layer.activate", "Make a layer the active layer")]
        public static void ActivateLayer(int layer)
        {
            ActivateLayerCommand cmd = new ActivateLayerCommand(App.Scene.GetCanvasByLayerIndex(layer));
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("layer.show", "Make a layer visible")]
        public static void ShowLayer(int layer)
        {
            App.Scene.ShowLayer(layer);
        }

        [ApiEndpoint("layer.hide", "Hide a layer")]
        public static void HideLayer(int layer)
        {
            App.Scene.HideLayer(layer);
        }

        [ApiEndpoint("layer.toggle", "Toggles a layer between visible and hidden")]
        public static void ToggleLayer(int layer)
        {
            App.Scene.ToggleLayerVisibility(layer);
        }

        [ApiEndpoint("model.select", "Selects a 3d model by index.")]
        public static void SelectModel(int index)
        {
            SelectWidget(_GetActiveModel(index));
        }

        private static void SelectWidget(GrabWidget widget)
        {
            SelectionManager.m_Instance.SelectWidget(widget);
        }

        [ApiEndpoint("model.set.position", "Move a 3d model to the given coordinates")]
        public static void PositionModel(int index, Vector3 position)
        {
            _SetWidgetTransform(_GetActiveModel(index), position);
        }

        [ApiEndpoint("symmetry.set.position", "Move the symmetry widget to the given coordinates")]
        public static void SymmetrySetPosition(Vector3 position)
        {
            var widget = PointerManager.m_Instance.SymmetryWidget;
            _SetWidgetTransform(widget, position);
        }

        [ApiEndpoint("symmetry.set.rotation", "Sets the symmetry widget rotation")]
        public static void SymmetrySetRotation(Quaternion rotation)
        {
            var widget = PointerManager.m_Instance.SymmetryWidget;
            _SetWidgetTransform(widget, Coords.AsCanvas[widget.transform].translation, rotation);
        }

        [ApiEndpoint("symmetry.set.transform", "Sets the position and rotation of the symmetry widget")]
        public static void SymmetrySetTransform(Vector3 position, Vector3 rotation)
        {
            SymmetrySetTransform(position, Quaternion.Euler(rotation));
        }
        public static void SymmetrySetTransform(Vector3 position, Quaternion rotation)
        {
            var widget = PointerManager.m_Instance.SymmetryWidget;
            _SetWidgetTransform(widget, position, rotation);
        }

        [ApiEndpoint("brush.force.painting.on", "Start painting even if the trigger isn't pressed")]
        public static void ForcePaintingOn(bool active)
        {
            if (active)
            {
                ApiManager.Instance.ForcePainting = ApiManager.ForcePaintingMode.ForcedOn;
            }
            else
            {
                ApiManager.Instance.ForcePainting = ApiManager.ForcePaintingMode.None;
            }
        }

        [ApiEndpoint("brush.force.painting.off", "Stop painting even if the trigger is pressed")]
        public static void ForcePaintingOff(bool active)
        {
            if (active)
            {
                ApiManager.Instance.ForcePainting = ApiManager.ForcePaintingMode.ForcedOff;
            }
            else
            {
                ApiManager.Instance.ForcePainting = ApiManager.ForcePaintingMode.None;
            }
        }

        [ApiEndpoint("brush.new.stroke", "Ends the current stroke and starts a new one next frame")]
        public static void ForceNewStroke()
        {
            ApiManager.Instance.PreviousForcePaintingMode = ApiManager.Instance.ForcePainting;
            ApiManager.Instance.ForcePainting = ApiManager.ForcePaintingMode.ForceNewStroke;
        }

        [ApiEndpoint("image.select", "Selects an image by index.")]
        public static void SelectImage(int index)
        {
            SelectWidget(_GetActiveImage(index));
        }

        [ApiEndpoint("image.position", "Move an image to the given coordinates")]
        public static void PositionImage(int index, Vector3 position)
        {
            _SetWidgetTransform(_GetActiveImage(index), position);
        }

        [ApiEndpoint("image.formEncode", "Converts an image to a string suitable for use in a form")]
        public static string FormEncodeImage(int index)
        {
            var path = _GetActiveImage(index).ReferenceImage.FileFullPath;
            return Convert.ToBase64String(File.ReadAllBytes(path));
        }

        [ApiEndpoint("image.base64Decode", "Saves an image based on a base64 encoded string")]
        public static string SaveBase64(string base64, string filename)
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length > 4 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G')
            {
                if (!filename.ToLower().EndsWith(".png"))
                {
                    filename += ".png";
                }
            }
            else if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                if (!filename.ToLower().EndsWith(".jpg") && !filename.ToLower().EndsWith(".jpeg"))
                {
                    filename += ".jpg";
                }
            }
            var path = Path.Combine(App.ReferenceImagePath(), filename);
            File.WriteAllBytes(path, bytes);
            return path;
        }


        [ApiEndpoint("scripts.toolscript.activate", "Activate the given tool script")]
        public static void ActivateToolScript(string scriptName)
        {
            LuaManager.Instance.SetActiveScriptByName(LuaApiCategory.ToolScript, scriptName);
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ScriptedTool);
        }

        [ApiEndpoint("scripts.toolscript.deactivate", "Dectivate the tool script")]
        public static void DectivateToolScript()
        {
            SketchSurfacePanel.m_Instance.EnableDefaultTool();
        }

        [ApiEndpoint("scripts.symmetryscript.activate", "Activate the given symmetry script")]
        public static void ActivateSymmetryScript(string scriptName)
        {
            LuaManager.Instance.SetActiveScriptByName(LuaApiCategory.SymmetryScript, scriptName);
            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.ScriptedSymmetryMode);
        }

        [ApiEndpoint("scripts.symmetryscript.deactivate", "Dectivate the symmetry script")]
        public static void DectivateSymmetryScript()
        {
            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.None);
        }

        [ApiEndpoint("scripts.pointerscript.activate", "Activate the given pointer script")]
        public static void ActivatePointerScript(string scriptName)
        {
            LuaManager.Instance.SetActiveScriptByName(LuaApiCategory.PointerScript, scriptName);
            LuaManager.Instance.PointerScriptsEnabled = true;
        }

        [ApiEndpoint("scripts.pointerscript.deactivate", "Dectivate the pointer script")]
        public static void DectivatePointerScript()
        {
            LuaManager.Instance.PointerScriptsEnabled = false;
        }

        // WIP
        // [ApiEndpoint("video.import", "Imports a video given a url or a filename in Media Library\\Videos")]
        // public static void ImportVideo(string location)
        // {
        //     if (location.StartsWith("http://") || location.StartsWith("https://"))
        //     {
        //         location = _DownloadMediaFileFromUrl(location, "Videos");
        //     }
        //     location = DownloadMediaFileFromUrl(location, "Videos");
        //
        //     location = Path.Combine("Videos", location);
        //     var video = new TiltVideo();
        //     video.FilePath = location;
        //     VideoWidget.FromTiltVideo(video);
        //     // var tr = new TrTransform();
        //     // tr.translation = ApiManager.Instance.BrushPosition;
        //     // tr.rotation = ApiManager.Instance.BrushRotation;
        //     // CreateWidgetCommand createCommand = new CreateWidgetCommand(
        //     //     WidgetManager.m_Instance.ImageWidgetPrefab, tr);
        //     // SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
        //     // videoWidget.Show(true);
        //     // createCommand.SetWidgetCost(videoWidget.GetTiltMeterCost());
        //     //
        //     // WidgetManager.m_Instance.WidgetsDormant = false;
        //     // SketchControlsScript.m_Instance.EatGazeObjectInput();
        //     // SelectionManager.m_Instance.RemoveFromSelection(false);
        // }

        [ApiEndpoint("guide.add", "Adds a guide to the scene")]
        public static void AddGuide(string type)
        {
            StencilType stencilType;

            switch (type)
            {
                case "cube":
                    stencilType = StencilType.Cube;
                    break;
                case "sphere":
                    stencilType = StencilType.Sphere;
                    break;
                case "capsule":
                    stencilType = StencilType.Capsule;
                    break;
                case "cone":
                    stencilType = StencilType.Cone;
                    break;
                case "ellipsoid":
                    stencilType = StencilType.Ellipsoid;
                    break;
                default:
                    stencilType = StencilType.Sphere;
                    break;
            }

            var tr = _CurrentTransform();
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(stencilType), tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
        }

        [ApiEndpoint("guide.select", "Selects a guide by index.")]
        public static void SelectGuide(int index)
        {
            SelectWidget(_GetActiveStencil(index));
        }

        [ApiEndpoint("guide.position", "Move a guide to the given coordinates")]
        public static void PositionGuide(int index, Vector3 position)
        {
            _SetWidgetTransform(_GetActiveStencil(index), position);
        }

        [ApiEndpoint("guide.scale", "Sets the (non-uniform) scale of a guide")]
        public static void ScaleGuide(int index, Vector3 scale)
        {
            var stencil = _GetActiveStencil(index);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(stencil, stencil.LocalTransform, scale));
        }
    }
}
