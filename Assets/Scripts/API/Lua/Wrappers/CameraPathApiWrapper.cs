using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class CameraPathApiWrapper
    {
        public static void renderActivePath() => ApiMethods.RenderCameraPath();
        public static void showAll() => WidgetManager.m_Instance.CameraPathsVisible = true;
        public static void hideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;
        public static void previewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;
        public static void delete(int index)
        {
            try
            {
                var cameraPathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
                WidgetManager.m_Instance.DeleteCameraPath(cameraPathWidget);
            }
            catch (ArgumentOutOfRangeException e)
            { }
        }

        public static int create()
        {
            var widget = WidgetManager.m_Instance.CreatePathWidget();
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            return active;
        }

        public static int createFromPath(List<TrTransform> path)
        {
            CameraPathMetadata metadata = new CameraPathMetadata();
            metadata.PathKnots = path.Select(t => new CameraPathPositionKnotMetadata
            {
                Xf = App.Scene.MainCanvas.Pose * t, // convert to canvas space,
                TangentMagnitude = t.scale
            }).ToArray();
            metadata.RotationKnots = Array.Empty<CameraPathRotationKnotMetadata>();
            metadata.FovKnots = Array.Empty<CameraPathFovKnotMetadata>();
            metadata.SpeedKnots = Array.Empty<CameraPathSpeedKnotMetadata>();
            var widget = CameraPathWidget.CreateFromSaveData(metadata);
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            WidgetManager.m_Instance.CameraPathsVisible = true;
            return active;
        }

        public static int duplicate(int index)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            CameraPathMetadata metadata = pathWidget.AsSerializable();
            var widget = CameraPathWidget.CreateFromSaveData(metadata);
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            WidgetManager.m_Instance.CameraPathsVisible = true;
            return active;
        }


        public static int insertPosition(int index, Vector3 position, Quaternion rotation, float smoothing)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            if (pathWidget.Path.ProjectPositionOnToPath(position, out PathT pathT, out Vector3 error))
            {
                return _insertPosition(pathWidget, pathT, position, rotation, smoothing);
            }
            return -1;
        }

        public static int insertPosition(int index, float t, Quaternion rotation, float smoothing)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var position = pathWidget.Path.GetPosition(pathT);
            return _insertPosition(pathWidget, pathT, position, rotation, smoothing);
        }

        private static int _insertPosition(CameraPathWidget pathWidget, PathT pathT, Vector3 position, Quaternion rotation, float smoothing)
        {
            position = App.Scene.MainCanvas.Pose.MultiplyPoint(position);
            rotation = App.Scene.MainCanvas.Pose.rotation * rotation;
            CameraPathPositionKnot knot = pathWidget.Path.CreatePositionKnot(position);
            int knotIndex = pathT.Floor();
            if (pathWidget.Path.IsPositionNearHead(knot.transform.position) &&
                knotIndex == pathWidget.Path.NumPositionKnots)
            {
                CameraPathPositionKnot cppkCreated = knot;
                CameraPathPositionKnot cppkHead = pathWidget.Path.PositionKnots[0];
                cppkCreated.transform.rotation = cppkHead.transform.rotation;
                cppkCreated.TangentMagnitude = 1;
            }
            pathWidget.Path.InsertPositionKnot(knot, knotIndex);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(
                    pathWidget.Path, pathWidget.Path.LastPlacedKnotInfo, smoothing, rotation * Vector3.forward,
                    mergesWithCreateCommand: true));
            return knotIndex;
        }

        public static int insertRotation(int index, Vector3 pos, Quaternion rot)
        {
            pos = App.Scene.MainCanvas.Pose.MultiplyPoint(pos);
            rot = App.Scene.MainCanvas.Pose.rotation * rot;
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            if (pathWidget.Path.ProjectPositionOnToPath(pos, out PathT pathT, out Vector3 error))
                return insertRotation(index, pathT.T, rot);
            return -1;
        }

        public static int insertRotation(int index, float t, Quaternion rot)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var knot = pathWidget.Path.CreateRotationKnot(pathT, rot);
            return pathWidget.Path.AllKnots.IndexOf(knot);
        }

        public static int insertFov(int index, Vector3 pos, float fov)
        {
            pos = App.Scene.MainCanvas.Pose.MultiplyPoint(pos);
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            if (pathWidget.Path.ProjectPositionOnToPath(pos, out PathT pathT, out Vector3 error))
                return insertFov(index, pathT.T, fov);
            return -1;
        }

        public static int insertFov(int index, float t, float fov)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var knot = pathWidget.Path.CreateFovKnot(pathT);
            knot.FovValue = fov;
            return pathWidget.Path.AllKnots.IndexOf(knot);
        }

        public static int insertSpeed(int index, Vector3 pos, float speed)
        {
            pos = App.Scene.MainCanvas.Pose.MultiplyPoint(pos);
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            if (pathWidget.Path.ProjectPositionOnToPath(pos, out PathT pathT, out Vector3 error))
                return insertSpeed(index, pathT.T, speed);
            return -1;
        }
        public static int insertSpeed(int index, float t, float speed)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var knot = pathWidget.Path.CreateSpeedKnot(pathT);
            knot.SpeedValue = speed;
            return pathWidget.Path.AllKnots.IndexOf(knot);
        }

        public static void extend(int index, Vector3 position, Quaternion rotation, float smoothing, bool atStart = false)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            var extendType = atStart ? CameraPathTool.ExtendPathType.ExtendAtHead : CameraPathTool.ExtendPathType.ExtendAtTail;
            pathWidget.ExtendPath(App.Scene.MainCanvas.Pose.MultiplyPoint(position), extendType);
            var knotDesc = pathWidget.Path.LastPlacedKnotInfo;
            var rot = rotation * Vector3.forward;
            rot = App.Scene.MainCanvas.Pose.rotation * rot; // convert to canvas space
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(
                    pathWidget.Path, knotDesc, smoothing, rot,
                    mergesWithCreateCommand: true
                )
            );
        }

        public static void loop(int index)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            pathWidget.ExtendPath(Vector3.zero, CameraPathTool.ExtendPathType.Loop); // position is ignored
        }

        public static int active
        {
            get
            {
                var activePath = WidgetManager.m_Instance.GetCurrentCameraPath();
                if (activePath == null) return -1;
                return WidgetManager.m_Instance.GetIndexOfCameraPath(activePath.WidgetScript).Value;
            }

            set
            {
                var path = WidgetManager.m_Instance.GetNthActiveCameraPath(value);
                WidgetManager.m_Instance.SetCurrentCameraPath(path);
            }
        }

        public static void recordActivePath()
        {
            // Turn off MultiCam if we're going to record the camera path.
            if (SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.MultiCamTool)
            {
                SketchSurfacePanel.m_Instance.EnableDefaultTool();
            }
            SketchControlsScript.m_Instance.CameraPathCaptureRig.RecordPath();
        }

        public static TrTransform sample(int index, float time, bool loop = true, bool pingpong = false)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            var cameraPath = pathWidget.Path;
            if (cameraPath == null) return TrTransform.identity;
            var t = new PathT(time);
            var maxT = new PathT(time);
            maxT.Clamp(cameraPath.NumPositionKnots);
            var origin = cameraPath.GetPosition(new PathT(0));
            if (t > maxT && loop)
            {
                if (pingpong)
                {
                    int numLoops = Mathf.FloorToInt(t.T / maxT.T);
                    if (numLoops % 2 == 0)
                    {
                        t = new PathT(t.T % maxT.T);
                    }
                    else
                    {
                        t = new PathT(maxT.T - (t.T % maxT.T));
                    }
                }
                else
                {
                    t = new PathT(t.T % maxT.T);
                }
            }
            var tr = TrTransform.TR(
                cameraPath.GetPosition(t) - origin,
                cameraPath.GetRotation(t)
            );
            return tr;
        }

        public static TrTransform getTransform(int index) => App.Scene.ActiveCanvas.AsCanvas[WidgetManager.m_Instance.GetNthActiveCameraPath(index).transform];
        public static void setTransform(int index, TrTransform tr)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            App.Scene.ActiveCanvas.AsCanvas[pathWidget.transform] = tr;
            pathWidget.Path.RefreshEntirePath();
        }

        public static Vector3 getPosition(int index) => getTransform(index).translation;
        public static void setPosition(int index, Vector3 position)
        {
            setTransform(index, TrTransform.T(position));
        }

        public static Quaternion getRotation(int index) => getTransform(index).rotation;
        public static void setRotation(int index, Quaternion rotation)
        {
            setTransform(index, TrTransform.R(rotation));
        }
    }

}
