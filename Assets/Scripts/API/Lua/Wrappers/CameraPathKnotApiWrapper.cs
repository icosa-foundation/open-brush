﻿using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A position, rotation, speed, or field-of-view knot on a camera path")]
    [MoonSharpUserData]
    public class CameraPathKnotApiWrapper
    {
        [MoonSharpHidden] public CameraPathKnot _Knot;
        [MoonSharpHidden] public CameraPathWidget _CameraPathWidget;

        public CameraPathKnotApiWrapper(CameraPathKnot knot, CameraPathWidget cameraPathWidget)
        {
            _Knot = knot;
            _CameraPathWidget = cameraPathWidget;
        }

        private CameraPath Path => _CameraPathWidget.Path;

        private void RefreshPath()
        {
            _Knot.RefreshVisuals();
            Path.RefreshEntirePath();
        }

        [LuaDocsDescription("The knot type: Position, Rotation, Speed, Fov, or Invalid")]
        public string type => _Knot.KnotType.ToString();

        [LuaDocsDescription("The knot's transform")]
        public TrTransform transform
        {
            get => _CameraPathWidget.Canvas.Pose.inverse * TrTransform.FromTransform(_Knot.transform);
            set
            {
                var tr = _CameraPathWidget.Canvas.Pose * value;
                _Knot.transform.position = tr.translation;
                _Knot.transform.rotation = tr.rotation;
                RefreshPath();
            }
        }

        [LuaDocsDescription("The knot's position")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TR(value, transform.rotation);
        }

        [LuaDocsDescription("The knot's rotation")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TR(transform.translation, value);
        }

        [LuaDocsDescription("The knot's time along the camera path")]
        public float pathT
        {
            get => _Knot is CameraPathPositionKnot positionKnot
                ? Path.PositionKnots.IndexOf(positionKnot)
                : _Knot.PathT.T;
            set
            {
                if (_Knot.KnotType == CameraPathKnot.Type.Position) return;
                _Knot.PathT = new PathT(value);
                _Knot.SetPosition(Path.GetPosition(_Knot.PathT));
                RefreshPath();
            }
        }

        [LuaDocsDescription("Position-knot tangent magnitude. Returns 0 for non-position knots")]
        public float tangent
        {
            get => _Knot is CameraPathPositionKnot knot ? knot.TangentMagnitude : 0;
            set
            {
                if (_Knot is CameraPathPositionKnot knot)
                {
                    knot.TangentMagnitude = value;
                    RefreshPath();
                }
            }
        }

        [LuaDocsDescription("Speed-knot camera speed. Returns the sampled path speed for non-speed knots")]
        public float speed
        {
            get => _Knot is CameraPathSpeedKnot knot ? knot.CameraSpeed : Path.GetSpeed(_Knot.PathT);
            set
            {
                if (_Knot is CameraPathSpeedKnot knot)
                {
                    knot.SpeedValue = value;
                    RefreshPath();
                }
            }
        }

        [LuaDocsDescription("FOV-knot camera field of view. Returns the sampled path FOV for non-FOV knots")]
        public float fov
        {
            get => _Knot is CameraPathFovKnot knot ? knot.CameraFov : Path.GetFov(_Knot.PathT);
            set
            {
                if (_Knot is CameraPathFovKnot knot)
                {
                    knot.FovValue = value;
                    RefreshPath();
                }
            }
        }
    }
}
