using System;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("Represents the settings for the symmetry mode")]
    [MoonSharpUserData]
    public class SymmetrySettingsApiWrapper
    {
        private ApiSymmetryMode _Mode;

        private Vector3 _Position;
        private Quaternion _Rotation;
        private Vector3 _Spin;

        private PointSymmetry.Family _PointType;
        private int _PointOrder;

        private SymmetryGroup.R _WallpaperType;
        private int _WallpaperRepeatX;
        private int _WallpaperRepeatY;
        private float _WallpaperScale;
        private float _WallpaperScaleX;
        private float _WallpaperScaleY;
        private float _WallpaperSkewX;
        private float _WallpaperSkewY;

        private SymmetrySettingsApiWrapper _Current;
        private bool _IsCurrent => ReferenceEquals(_Current, this);

        public SymmetrySettingsApiWrapper(bool isCurrent)
        {
            if (isCurrent)
            {
                _Mode = ApiSymmetryMode.None;

                switch (PointerManager.m_Instance.CurrentSymmetryMode)
                {
                    case PointerManager.SymmetryMode.MultiMirror:

                        if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Point)
                        {
                            _Mode = ApiSymmetryMode.Point;
                            _PointType = PointerManager.m_Instance.m_PointSymmetryFamily;
                            _PointOrder = PointerManager.m_Instance.m_PointSymmetryOrder;
                        }
                        else
                        {
                            _Mode = ApiSymmetryMode.Wallpaper;
                            _WallpaperType = PointerManager.m_Instance.m_WallpaperSymmetryGroup;
                            _WallpaperRepeatX = PointerManager.m_Instance.m_WallpaperSymmetryX;
                            _WallpaperRepeatY = PointerManager.m_Instance.m_WallpaperSymmetryY;
                            _WallpaperScale = PointerManager.m_Instance.m_WallpaperSymmetryScale;
                            _WallpaperScaleX = PointerManager.m_Instance.m_WallpaperSymmetryScaleX;
                            _WallpaperScaleY = PointerManager.m_Instance.m_WallpaperSymmetryScaleY;
                            _WallpaperSkewX = PointerManager.m_Instance.m_WallpaperSymmetrySkewX;
                            _WallpaperSkewY = PointerManager.m_Instance.m_WallpaperSymmetrySkewY;
                        }
                        break;
                    case PointerManager.SymmetryMode.ScriptedSymmetryMode:
                        _Mode = ApiSymmetryMode.Scripted;
                        break;
                    case PointerManager.SymmetryMode.SinglePlane:
                        _Mode = ApiSymmetryMode.Standard;
                        break;
                    case PointerManager.SymmetryMode.TwoHanded:
                        _Mode = ApiSymmetryMode.TwoHanded;
                        break;
                    case PointerManager.SymmetryMode.None:
                        _Mode = ApiSymmetryMode.None;
                        break;
                }
                var widgetTr = PointerManager.m_Instance.SymmetryWidget.transform;
                _Position = widgetTr.position;
                _Rotation = widgetTr.rotation;
                _Spin = PointerManager.m_Instance.SymmetryWidget.GetSpin();
                _Current = this;
            }
        }

        [LuaDocsDescription("The current symmetry settings")]
        public static SymmetrySettingsApiWrapper current
        {
            get => new(isCurrent: true);
            set
            {
                switch (value._Mode)
                {
                    case ApiSymmetryMode.None:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.None);
                        break;
                    case ApiSymmetryMode.Standard:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.SinglePlane);
                        break;
                    case ApiSymmetryMode.Scripted:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.ScriptedSymmetryMode);
                        break;
                    case ApiSymmetryMode.TwoHanded:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.TwoHanded);
                        break;
                    case ApiSymmetryMode.Point:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                        PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
                        PointerManager.m_Instance.m_PointSymmetryFamily = value._PointType;
                        PointerManager.m_Instance.m_PointSymmetryOrder = value._PointOrder;
                        break;
                    case ApiSymmetryMode.Wallpaper:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                        PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
                        PointerManager.m_Instance.m_WallpaperSymmetryGroup = value._WallpaperType;
                        PointerManager.m_Instance.m_WallpaperSymmetryX = value._WallpaperRepeatX;
                        PointerManager.m_Instance.m_WallpaperSymmetryY = value._WallpaperRepeatY;
                        PointerManager.m_Instance.m_WallpaperSymmetryScale = value._WallpaperScale;
                        PointerManager.m_Instance.m_WallpaperSymmetryScaleX = value._WallpaperScaleX;
                        PointerManager.m_Instance.m_WallpaperSymmetryScaleY = value._WallpaperScaleY;
                        PointerManager.m_Instance.m_WallpaperSymmetrySkewX = value._WallpaperSkewX;
                        PointerManager.m_Instance.m_WallpaperSymmetrySkewY = value._WallpaperSkewY;
                        break;
                }
                var widget = PointerManager.m_Instance.SymmetryWidget;
                var tr = TrTransform.TR(value._Position, value._Rotation);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
                );
                PointerManager.m_Instance.SymmetryWidget.Spin(value._Spin.x, value._Spin.y, value._Spin.z);
            }
        }

        public string mode
        {
            get => _Mode.ToString();
            set {
                var success = Enum.TryParse(value, ignoreCase: true, out ApiSymmetryMode mode);
                if (!success)
                {
                    string validList = string.Join(", ", Enum.GetNames(typeof(ApiSymmetryMode)));
                    LuaManager.Instance.LogLuaMessage($"Unknown Symmetry Mode: {value} (valid values are: {validList})");
                    return;
                }
                _Mode = mode;
                if (_IsCurrent)
                {
                    switch (mode)
                    {
                        case ApiSymmetryMode.None:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.None);
                            break;
                        case ApiSymmetryMode.Standard:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.SinglePlane);
                            break;
                        case ApiSymmetryMode.Scripted:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.ScriptedSymmetryMode);
                            break;
                        case ApiSymmetryMode.TwoHanded:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.TwoHanded);
                            break;
                        case ApiSymmetryMode.Point:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
                            break;
                        case ApiSymmetryMode.Wallpaper:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
                            break;
                    }
                }
            }
        }

        public Vector3ApiWrapper position
        {
            get => new(_Position);
            set {
                position = value;
                if (_IsCurrent) _SetTransform(TrTransform.TR(value._Vector3, rotation._Quaternion));
            }
        }

        public RotationApiWrapper rotation
        {
            get => new(_Rotation);
            set {
                rotation = value;
                if (_IsCurrent) _SetTransform(TrTransform.TR(position._Vector3, value._Quaternion));
            }
        }

        public void _SetTransform(TrTransform tr)
        {
            var widget = PointerManager.m_Instance.SymmetryWidget;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
            );
        }

        public Vector3ApiWrapper spin
        {
            get => new(_Spin);
            set {
                spin = value;
                if (_IsCurrent) PointerManager.m_Instance.SymmetryWidget.Spin(value.x, value.y, value.z);
            }
        }

        public string pointType
        {
            get => _PointType.ToString();
            set {
                var success = Enum.TryParse(value, ignoreCase: true, out PointSymmetry.Family pointSymmetryType);
                if (!success)
                {
                    string validList = string.Join(", ", Enum.GetNames(typeof(PointSymmetry.Family)));
                    LuaManager.Instance.LogLuaMessage($"Unknown Point Symmetry Type: {value} (valid values are: {validList})");
                    return;
                }
                _PointType = pointSymmetryType;
                if (_IsCurrent) PointerManager.m_Instance.m_PointSymmetryFamily = pointSymmetryType;
            }
        }

        public int pointOrder
        {
            get => _PointOrder;
            set {
                _PointOrder = value;
                if (_IsCurrent) PointerManager.m_Instance.m_PointSymmetryOrder = value;
            }
        }

        public string wallpaperType
        {
            get => _WallpaperType.ToString();
            set {
                var success = Enum.TryParse(value, ignoreCase: true, out SymmetryGroup.R wallpaperType);
                if (!success)
                {
                    string validList = string.Join(", ", Enum.GetNames(typeof(SymmetryGroup.R)));
                    LuaManager.Instance.LogLuaMessage($"Unknown Wallpaper Type: {value} (valid values are: {validList})");
                    return;
                }
                _WallpaperType = wallpaperType;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetryGroup = wallpaperType;
            }
        }

        public int wallpaperRepeatX
        {
            get => _WallpaperRepeatX;
            set {
                _WallpaperRepeatX = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetryX = value;
            }
        }

        public int wallpaperRepeatY
        {
            get => _WallpaperRepeatY;
            set {
                _WallpaperRepeatY = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetryY = value;
            }
        }

        public float wallpaperScale
        {
            get => _WallpaperScale;
            set {
                _WallpaperScale = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetryScale = value;
            }
        }

        public float wallpaperScaleX
        {
            get => _WallpaperScaleX;
            set {
                _WallpaperScaleX = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetryScaleX = value;
            }
        }

        public float wallpaperScaleY
        {
            get => _WallpaperScaleY;
            set {
                _WallpaperScaleY = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetryScaleY = value;
            }
        }

        public float wallpaperSkewX
        {
            get => _WallpaperSkewX;
            set {
                _WallpaperSkewX = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetrySkewX = value;
            }
        }

        public float wallpaperSkewY
        {
            get => _WallpaperSkewY;
            set {
                _WallpaperSkewY = value;
                if (_IsCurrent) PointerManager.m_Instance.m_WallpaperSymmetrySkewY = value;
            }
        }

    }
}
