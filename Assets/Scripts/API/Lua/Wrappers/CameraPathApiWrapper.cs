using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("A camera path and it's position, speed or FOV knots")]
    [MoonSharpUserData]
    public class CameraPathApiWrapper
    {

        public CameraPathWidget _CameraPathWidget;

        public CameraPathApiWrapper()
        {
            var widget = WidgetManager.m_Instance.CreatePathWidget();
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            _CameraPathWidget = widget;
        }

        public CameraPathApiWrapper(CameraPathWidget cameraPathWidget)
        {
            _CameraPathWidget = cameraPathWidget;
        }

        public int index
        {
            get
            {
                var val = WidgetManager.m_Instance.GetActiveWidgetIndex(_CameraPathWidget);
                return val;
            }
        }

        public override string ToString()
        {
            return $"CameraPath({_CameraPathWidget})";
        }

        public bool active
        {
            get => WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript == _CameraPathWidget;
            set
            {
                if (value)
                {
                    WidgetManager.m_Instance.SetCurrentCameraPath(_CameraPathWidget);
                }
                else if (active)
                {
                    WidgetManager.m_Instance.SetCurrentCameraPath(null);
                }
            }
        }

        public TrTransform transform
        {
            get =>  App.Scene.MainCanvas.AsCanvas[_CameraPathWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_CameraPathWidget.transform] = value;

                // After trying various ways to get the path to update after transforming
                // I gave up and recreated it from scratch.
                CameraPathMetadata metadata = _CameraPathWidget.AsSerializable();
                WidgetManager.m_Instance.DeleteCameraPath(_CameraPathWidget);
                _CameraPathWidget = CameraPathWidget.CreateFromSaveData(metadata);
                _CameraPathWidget.Path.RefreshEntirePath();
            }
        }

        [LuaDocsDescription("The 3D position of the Camera Path (usually but not always it's first position knot)")]
        public Vector3 position
        {
            get => transform.translation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.T(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.translation = newTransform.translation;
                transform = tr_CS;
            }
        }

        [LuaDocsDescription("The 3D orientation of the Brush Camera Path")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.R(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.rotation = newTransform.rotation;
                transform = tr_CS;
            }
        }

        public float scale
        {
            get => transform.scale;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.S(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.scale = newTransform.scale;
                transform = tr_CS;
            }
        }

        public static void RenderActivePath() => ApiMethods.RenderCameraPath();
        public static void ShowAll() => WidgetManager.m_Instance.CameraPathsVisible = true;
        public static void HideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;
        public static void PreviewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;
        public void Delete()
        {
            WidgetManager.m_Instance.DeleteCameraPath(_CameraPathWidget);
        }

        public static CameraPathApiWrapper New()
        {
            var wrapper = new CameraPathApiWrapper();
            wrapper._CameraPathWidget = CameraPathWidget.CreateEmpty();
            return wrapper;
        }

        public static CameraPathApiWrapper FromPath(IPathApiWrapper path, bool looped)
        {
            CameraPathMetadata metadata = new CameraPathMetadata();
            metadata.PathKnots = path.AsSingleTrList().Select(t => new CameraPathPositionKnotMetadata
            {
                Xf = App.Scene.MainCanvas.Pose * t, // convert to canvas space,
                TangentMagnitude = t.scale
            }).ToArray();
            metadata.RotationKnots = Array.Empty<CameraPathRotationKnotMetadata>();
            metadata.FovKnots = Array.Empty<CameraPathFovKnotMetadata>();
            metadata.SpeedKnots = Array.Empty<CameraPathSpeedKnotMetadata>();
            var widget = CameraPathWidget.CreateFromSaveData(metadata);
            if (looped) widget.ExtendPath(Vector3.zero, CameraPathTool.ExtendPathType.Loop);
            widget.Path.RefreshEntirePath();
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            return new CameraPathApiWrapper(widget);
        }

        public PathApiWrapper AsPath(float step)
        {
            return new PathApiWrapper(
                _CameraPathWidget.Path.AsTrList(step)
            );
        }

        public CameraPathWidget Duplicate()
        {
            CameraPathMetadata metadata = _CameraPathWidget.AsSerializable();
            var widget = CameraPathWidget.CreateFromSaveData(metadata);
            widget.Path.RefreshEntirePath();
            return widget;
        }

        public int InsertPosition(Vector3 position, Quaternion rotation, float smoothing)
        {
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(position, out PathT pathT, out Vector3 error))
            {
                return _insertPosition(_CameraPathWidget, pathT, position, rotation, smoothing);
            }
            return -1;
        }

        public int InsertPosition(float t, Quaternion rotation, float smoothing)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var position = _CameraPathWidget.Path.GetPosition(pathT);
            return _insertPosition(_CameraPathWidget, pathT, position, rotation, smoothing);
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

        public int InsertRotation(Vector3 pos, Quaternion rot)
        {
            pos = App.Scene.MainCanvas.Pose.MultiplyPoint(pos);
            rot = App.Scene.MainCanvas.Pose.rotation * rot;
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(pos, out PathT pathT, out Vector3 error))
                return InsertRotation(pathT.T, rot);
            return -1;
        }

        public int InsertRotation(float t, Quaternion rot)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var knot = _CameraPathWidget.Path.CreateRotationKnot(pathT, rot);
            return _CameraPathWidget.Path.AllKnots.IndexOf(knot);
        }

        public int InsertFov(Vector3 pos, float fov)
        {
            pos = App.Scene.MainCanvas.Pose.MultiplyPoint(pos);
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(pos, out PathT pathT, out Vector3 error))
                return InsertFov(pathT.T, fov);
            return -1;
        }

        public int InsertFov(float t, float fov)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var knot = _CameraPathWidget.Path.CreateFovKnot(pathT);
            knot.FovValue = fov;
            return _CameraPathWidget.Path.AllKnots.IndexOf(knot);
        }

        public int InsertSpeed(Vector3 pos, float speed)
        {
            pos = App.Scene.MainCanvas.Pose.MultiplyPoint(pos);
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(pos, out PathT pathT, out Vector3 error))
                return InsertSpeed(pathT.T, speed);
            return -1;
        }
        public int InsertSpeed(float t, float speed)
        {
            PathT pathT = new PathT(t);
            var knot = _CameraPathWidget.Path.CreateSpeedKnot(pathT);
            knot.SpeedValue = speed;
            return _CameraPathWidget.Path.AllKnots.IndexOf(knot);
        }

        public void Extend(Vector3 position, Quaternion rotation, float smoothing, bool atStart = false)
        {
            var extendType = atStart ? CameraPathTool.ExtendPathType.ExtendAtHead : CameraPathTool.ExtendPathType.ExtendAtTail;
            _CameraPathWidget.ExtendPath(App.Scene.MainCanvas.Pose.MultiplyPoint(position), extendType);
            var knotDesc = _CameraPathWidget.Path.LastPlacedKnotInfo;
            var rot = rotation * Vector3.forward;
            rot = App.Scene.MainCanvas.Pose.rotation * rot; // convert to canvas space
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(_CameraPathWidget.Path, knotDesc, smoothing, rot, final: true)
            );
        }

        private static void _Extend(CameraPathWidget pathWidget, Vector3 position, Vector3 tangent, float smoothing, bool atStart = false)
        {
            var extendType = atStart ? CameraPathTool.ExtendPathType.ExtendAtHead : CameraPathTool.ExtendPathType.ExtendAtTail;
            pathWidget.ExtendPath(App.Scene.MainCanvas.Pose.MultiplyPoint(position), extendType);
            var knotDesc = pathWidget.Path.LastPlacedKnotInfo;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(
                    pathWidget.Path, knotDesc, smoothing, tangent,
                    mergesWithCreateCommand: true
                )
            );
        }

        public void Loop()
        {
            _CameraPathWidget.ExtendPath(Vector3.zero, CameraPathTool.ExtendPathType.Loop); // position is ignored
        }

        public static void RecordActivePath()
        {
            // Turn off MultiCam if we're going to record the camera path.
            if (SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.MultiCamTool)
            {
                SketchSurfacePanel.m_Instance.EnableDefaultTool();
            }
            SketchControlsScript.m_Instance.CameraPathCaptureRig.RecordPath();
        }

        public TrTransform Sample(float time, bool loop = true, bool pingpong = false)
        {
            var cameraPath = _CameraPathWidget.Path;
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

        public CameraPathApiWrapper Simplify(float tolerance, float smoothing)
        {
            List<Vector3> inputPoints = _CameraPathWidget.Path.PositionKnots.Select(knot => knot.transform.position).ToList();
            List<Quaternion> inputRots = _CameraPathWidget.Path.PositionKnots.Select(knot => knot.transform.rotation).ToList();
            var newPoints = new List<TrTransform>();

            if (inputPoints.Count < 2)
            {
                Debug.LogError("Not enough input points to create a curve.");
                return null;
            }

            float angleToleranceRad = Mathf.Deg2Rad * tolerance;

            Vector3 prevDirection = (inputPoints[1] - inputPoints[0]).normalized;
            newPoints.Add(TrTransform.TRS(inputPoints[0], inputRots[0], smoothing));

            int lastSplinePointIndex = 0;

            for (int i = 2; i < inputPoints.Count; i++)
            {
                Vector3 currentDirection = (inputPoints[i] - inputPoints[i - 1]).normalized;
                float angleBetween = Vector3.Angle(prevDirection, currentDirection) * Mathf.Deg2Rad;

                if (angleBetween > angleToleranceRad)
                {
                    // Vector3 tangent = (inputPoints[i - 1] - inputPoints[lastSplinePointIndex]).normalized;
                    float segmentLength = (inputPoints[lastSplinePointIndex] - inputPoints[i - 1]).magnitude;
                    newPoints.Add(TrTransform.TRS(inputPoints[i - 1], inputRots[i], segmentLength * smoothing ));

                    prevDirection = currentDirection;
                    lastSplinePointIndex = i - 1;
                }
            }

            float lastSegmentLength = (inputPoints[lastSplinePointIndex] - inputPoints[inputPoints.Count - 1]).magnitude;
            newPoints.Add(TrTransform.TRS(inputPoints[inputPoints.Count - 1], inputRots[inputRots.Count - 1], lastSegmentLength * smoothing ));
            return FromPath(new PathApiWrapper(newPoints), _CameraPathWidget.Path.PathLoops);
        }
    }
}


