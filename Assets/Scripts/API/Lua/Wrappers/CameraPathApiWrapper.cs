using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A camera path and its position, speed or FOV knots")]
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

        [LuaDocsDescription("Returns the index of this Camera Path in Sketch.cameraPaths")]
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

        [LuaDocsDescription("The layer the camera path is on")]
        public LayerApiWrapper layer
        {
            get => _CameraPathWidget != null ? new LayerApiWrapper(_CameraPathWidget.Canvas) : null;
            set => _CameraPathWidget.SetCanvas(value._CanvasScript);
        }

        [LuaDocsDescription("The group this camera path is part of")]
        public GroupApiWrapper group
        {
            get => _CameraPathWidget != null ? new GroupApiWrapper(_CameraPathWidget.Group, layer._CanvasScript) : null;
            set => _CameraPathWidget.Group = value._Group;
        }

        [LuaDocsDescription("Gets or sets whether this Camera Path is active")]
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

        [LuaDocsDescription("The transform of the camera path")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_CameraPathWidget.transform];
            set
            {
                value = _CameraPathWidget.Canvas.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_CameraPathWidget.transform] = value;

                // After trying various ways to get the path to update after transforming
                // I gave up and recreated it from scratch.
                CameraPathMetadata metadata = _CameraPathWidget.AsSerializable();
                WidgetManager.m_Instance.DeleteCameraPath(_CameraPathWidget);
                _CameraPathWidget = CameraPathWidget.CreateFromSaveData(metadata);
                _CameraPathWidget.Path.RefreshEntirePath();
            }
        }

        [LuaDocsDescription("The 3D position of the Camera Path (usually but not always its first position knot)")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription("The 3D orientation of the Brush Camera Path")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }

        [LuaDocsDescription("The scale of the camera path")]
        public float scale
        {
            get => transform.scale;
            set => transform = TrTransform.TRS(transform.translation, transform.rotation, value);
        }

        [LuaDocsDescription("Renders the currently active path")]
        [LuaDocsExample("CameraPath:RenderActivePath()")]
        public static void RenderActivePath() => ApiMethods.RenderCameraPath();

        [LuaDocsDescription("Shows all camera paths")]
        [LuaDocsExample("CameraPath:ShowAll()")]
        public static void ShowAll() => WidgetManager.m_Instance.CameraPathsVisible = true;

        [LuaDocsDescription("Hides all camera paths")]
        [LuaDocsExample("CameraPath:HideAll()")]
        public static void HideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;

        [LuaDocsDescription("Turns previews on or off for the active path")]
        [LuaDocsParameter("active", "On is true, off is false")]
        [LuaDocsExample("CameraPath:PreviewActivePath(true)")]
        public static void PreviewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;

        [LuaDocsDescription("Deletes a camera path")]
        [LuaDocsExample("mycameraPath:Delete()")]
        public void Delete()
        {
            WidgetManager.m_Instance.DeleteCameraPath(_CameraPathWidget);
        }

        [LuaDocsDescription("Creates a new empty camera path")]
        [LuaDocsExample("CameraPath:New()")]
        [LuaDocsReturnValue("The new CameraPath")]
        public static CameraPathApiWrapper New()
        {
            var wrapper = new CameraPathApiWrapper();
            wrapper._CameraPathWidget = CameraPathWidget.CreateEmpty();
            return wrapper;
        }

        [LuaDocsDescription("Creates a camera path from a Path")]
        [LuaDocsParameter("path", "The Path to convert")]
        [LuaDocsParameter("looped", "Whether the resulting CameraPath should loop")]
        [LuaDocsExample("myCameraPath = Camera:FromPath(myPath, false)")]
        [LuaDocsReturnValue("A new CameraPath")]
        public static CameraPathApiWrapper FromPath(PathApiWrapper path, bool looped)
        {
            CameraPathMetadata metadata = new CameraPathMetadata();
            metadata.PathKnots = path.AsSingleTrList().Select(t => new CameraPathPositionKnotMetadata
            {
                Xf = App.Scene.ActiveCanvas.Pose * t, // convert to canvas space,
                TangentMagnitude = t.scale
            }).ToArray();
            metadata.RotationKnots = Array.Empty<CameraPathRotationKnotMetadata>();
            metadata.FovKnots = Array.Empty<CameraPathFovKnotMetadata>();
            metadata.SpeedKnots = Array.Empty<CameraPathSpeedKnotMetadata>();
            var widget = CameraPathWidget.CreateFromSaveData(metadata);
            widget.SetCanvas(App.Scene.ActiveCanvas);
            if (looped) widget.ExtendPath(Vector3.zero, CameraPathTool.ExtendPathType.Loop);
            widget.Path.RefreshEntirePath();
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            return new CameraPathApiWrapper(widget);
        }

        [LuaDocsDescription("Converts the camera path to a path by sampling it at regular time intervals")]
        [LuaDocsParameter("step", "The time step is use for each sample")]
        [LuaDocsExample("myPath = myCameraPath:AsPath(0.5)")]
        [LuaDocsReturnValue("The new Path")]
        public PathApiWrapper AsPath(float step)
        {
            return new PathApiWrapper(
                _CameraPathWidget.Path.AsTrList(step)
            );
        }

        [LuaDocsDescription("Duplicates the camera path")]
        [LuaDocsExample("mynewPath = myOldPath:Duplicate()")]
        [LuaDocsReturnValue("The copy of the specied CameraPath")]
        public CameraPathApiWrapper Duplicate()
        {
            CameraPathMetadata metadata = _CameraPathWidget.AsSerializable();
            var widget = CameraPathWidget.CreateFromSaveData(metadata);
            widget.Path.RefreshEntirePath();
            return new CameraPathApiWrapper(widget);
        }

        [LuaDocsDescription("Inserts a new position knot. (Position must be close to the existing path)")]
        [LuaDocsExample("myCameraPath:InsertPosition(pos, rot, 0.5")]
        [LuaDocsParameter("position", "The position of the new knot")]
        [LuaDocsParameter("rotation", "The rotation of the new knot")]
        [LuaDocsParameter("smoothing", "Controls the spline curvature for this knot")]
        [LuaDocsReturnValue("The index of the new knot, or -1 if the position is too far from the path")]

        public int InsertPosition(Vector3 position, Quaternion rotation, float smoothing)
        {
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(position, out PathT pathT, out Vector3 error))
            {
                return _insertPosition(_CameraPathWidget, pathT, position, rotation, smoothing);
            }
            return -1;
        }

        [LuaDocsDescription("Inserts a new position knot into the path at the specified time")]
        [LuaDocsExample("myCameraPath:InsertPositionAtTime(1.5, rot, 0.5")]
        [LuaDocsParameter("t", "The time along the path to insert the new knot")]
        [LuaDocsParameter("rotation", "The rotation of the new knot")]
        [LuaDocsParameter("smoothing", "Controls the spline curvature for this knot")]
        [LuaDocsReturnValue("The index of the new knot")]
        public int InsertPositionAtTime(float t, Quaternion rotation, float smoothing)
        {
            var pathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(index);
            PathT pathT = new PathT(t);
            var position = _CameraPathWidget.Path.GetPosition(pathT);
            return _insertPosition(_CameraPathWidget, pathT, position, rotation, smoothing);
        }

        private static int _insertPosition(CameraPathWidget pathWidget, PathT pathT, Vector3 position, Quaternion rotation, float smoothing)
        {
            position = pathWidget.Canvas.Pose.MultiplyPoint(position);
            rotation = pathWidget.Canvas.Pose.rotation * rotation;
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

        [LuaDocsDescription("Inserts a rotation knot at the specified position close to the existing path")]
        [LuaDocsExample("myCameraPath:InsertRotation(pos, rot")]
        [LuaDocsParameter("position", "The position of the new knot")]
        [LuaDocsParameter("rotation", "The rotation of the new knot")]
        [LuaDocsReturnValue("The index of the new knot, or -1 if the position is too far from the path")]
        public int InsertRotation(Vector3 position, Quaternion rotation)
        {
            position = _CameraPathWidget.Canvas.Pose.MultiplyPoint(position);
            rotation = _CameraPathWidget.Canvas.Pose.rotation * rotation;
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(position, out PathT pathT, out Vector3 error))
                return InsertRotationAtTime(pathT.T, rotation);
            return -1;
        }

        [LuaDocsDescription("Inserts a rotation knot at the specified time")]
        [LuaDocsExample("myCameraPath:InsertRotationAtTime(1.5, rot")]
        [LuaDocsParameter("t", "The time along the path to insert the new knot")]
        [LuaDocsParameter("rotation", "The rotation of the new knot")]
        [LuaDocsReturnValue("The index of the new knot")]

        public int InsertRotationAtTime(float t, Quaternion rotation)
        {
            PathT pathT = new PathT(t);
            var knot = _CameraPathWidget.Path.CreateRotationKnot(pathT, rotation);
            return _CameraPathWidget.Path.AllKnots.IndexOf(knot);
        }

        [LuaDocsDescription("Inserts a field of view knot at the specified position close to the existing path")]
        [LuaDocsParameter("position", "The position of the new knot")]
        [LuaDocsParameter("fov", "The field of view of the new knot")]
        [LuaDocsExample("myCameraPath:InsertFov(pos, 45")]
        [LuaDocsReturnValue("The index of the new knot, or -1 if the position is too far from the path")]
        public int InsertFov(Vector3 position, float fov)
        {
            position = _CameraPathWidget.Canvas.Pose.MultiplyPoint(position);
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(position, out PathT pathT, out Vector3 error))
                return InsertFovAtTime(pathT.T, fov);
            return -1;
        }

        [LuaDocsDescription("Inserts a fov knot at the specified time")]
        [LuaDocsParameter("t", "The time along the path to insert the new knot")]
        [LuaDocsParameter("fov", "The field of view of the new knot")]
        [LuaDocsExample("myCameraPath:InsertFovAtTime(2.5, 45")]
        [LuaDocsReturnValue("The index of the new knot")]

        public int InsertFovAtTime(float t, float fov)
        {
            PathT pathT = new PathT(t);
            var knot = _CameraPathWidget.Path.CreateFovKnot(pathT);
            knot.FovValue = fov;
            return _CameraPathWidget.Path.AllKnots.IndexOf(knot);
        }

        [LuaDocsDescription("Inserts a speed knot at the specified position close to the existing path")]
        [LuaDocsParameter("position", "The position of the new knot")]
        [LuaDocsParameter("speed", "The speed of the new knot")]
        [LuaDocsExample("myCameraPath:InsertSpeed(position, 1.5")]
        [LuaDocsReturnValue("The index of the new knot, or -1 if the position is too far from the path")]

        public int InsertSpeed(Vector3 position, float speed)
        {
            position = _CameraPathWidget.Canvas.Pose.MultiplyPoint(position);
            if (_CameraPathWidget.Path.ProjectPositionOnToPath(position, out PathT pathT, out Vector3 error))
                return InsertSpeedAtTime(pathT.T, speed);
            return -1;
        }

        [LuaDocsDescription("Inserts a speed knot at the specified time")]
        [LuaDocsParameter("t", "The time along the path to insert the new knot")]
        [LuaDocsParameter("speed", "The speed of the new knot")]
        [LuaDocsExample("myCameraPath:InsertSpeedAtTime(2.5, 2")]
        [LuaDocsReturnValue("The index of the new knot")]

        public int InsertSpeedAtTime(float t, float speed)
        {
            PathT pathT = new PathT(t);
            var knot = _CameraPathWidget.Path.CreateSpeedKnot(pathT);
            knot.SpeedValue = speed;
            return _CameraPathWidget.Path.AllKnots.IndexOf(knot);
        }

        [LuaDocsDescription("Extends the camera path")]
        [LuaDocsExample("myCameraPath:Extend(pos, rot, 1.2, true")]
        [LuaDocsParameter("position", "The position to extend the camera path to")]
        [LuaDocsParameter("rotation", "The rotation of the camera path at the extended position")]
        [LuaDocsParameter("smoothing", "The smoothing factor applied to the new point")]
        [LuaDocsParameter("atStart", "Determines whether the extension is done at the start or end of the camera path. True=start, false=end")]
        public void Extend(Vector3 position, Quaternion rotation, float smoothing, bool atStart = false)
        {
            var extendType = atStart ? CameraPathTool.ExtendPathType.ExtendAtHead : CameraPathTool.ExtendPathType.ExtendAtTail;
            _CameraPathWidget.ExtendPath(_CameraPathWidget.Canvas.Pose.MultiplyPoint(position), extendType);
            var knotDesc = _CameraPathWidget.Path.LastPlacedKnotInfo;
            var rot = rotation * Vector3.forward;
            rot = _CameraPathWidget.Canvas.Pose.rotation * rot; // convert to canvas space
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(_CameraPathWidget.Path, knotDesc, smoothing, rot, final: true)
            );
        }

        private static void _Extend(CameraPathWidget pathWidget, Vector3 position, Vector3 tangent, float smoothing, bool atStart = false)
        {
            var extendType = atStart ? CameraPathTool.ExtendPathType.ExtendAtHead : CameraPathTool.ExtendPathType.ExtendAtTail;
            pathWidget.ExtendPath(pathWidget.Canvas.Pose.MultiplyPoint(position), extendType);
            var knotDesc = pathWidget.Path.LastPlacedKnotInfo;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(
                    pathWidget.Path, knotDesc, smoothing, tangent,
                    mergesWithCreateCommand: true
                )
            );
        }

        [LuaDocsDescription("Loops the camera path")]
        [LuaDocsExample("myCameraPath:Loop")]
        public void Loop()
        {
            _CameraPathWidget.ExtendPath(Vector3.zero, CameraPathTool.ExtendPathType.Loop); // position is ignored
        }

        [LuaDocsDescription("Records the active camera path")]
        [LuaDocsExample("CameraPath:")]
        public static void RecordActivePath()
        {
            // Turn off MultiCam if we're going to record the camera path.
            if (SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.MultiCamTool)
            {
                SketchSurfacePanel.m_Instance.EnableDefaultTool();
            }
            SketchControlsScript.m_Instance.CameraPathCaptureRig.RecordPath();
        }

        [LuaDocsDescription("Samples the camera path at the specified time")]
        [LuaDocsExample("myTransform = myCameraPath:Sample(2.5, true, false)")]
        [LuaDocsParameter("time", "The time at which to sample the camera path")]
        [LuaDocsParameter("loop", "Determines whether the camera path should loop")]
        [LuaDocsParameter("pingpong", "Determines whether the camera path should pingpong (reverse direction every loop")]
        [LuaDocsReturnValue("The sampled transform of the camera at the specified time")]
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
            var tr = _CameraPathWidget.Canvas.Pose.inverse * TrTransform.TR(
                cameraPath.GetPosition(t) - origin,
                cameraPath.GetRotation(t)
            );
            return tr;
        }

        [LuaDocsDescription("Simplifies the camera path")]
        [LuaDocsExample("newPath = oldPath:Simplify(1.2, 1)")]
        [LuaDocsParameter("tolerance", "The tolerance used for simplification")]
        [LuaDocsParameter("smoothing", "The smoothing factor used for simplification")]
        [LuaDocsReturnValue("A new simplified Camera Path")]
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
                    newPoints.Add(TrTransform.TRS(inputPoints[i - 1], inputRots[i], segmentLength * smoothing));

                    prevDirection = currentDirection;
                    lastSplinePointIndex = i - 1;
                }
            }

            float lastSegmentLength = (inputPoints[lastSplinePointIndex] - inputPoints[inputPoints.Count - 1]).magnitude;
            newPoints.Add(TrTransform.TRS(inputPoints[inputPoints.Count - 1], inputRots[inputRots.Count - 1], lastSegmentLength * smoothing));
            return FromPath(new PathApiWrapper(newPoints), _CameraPathWidget.Path.PathLoops);
        }
    }
}
