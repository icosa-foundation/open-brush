using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{

    [LuaDocsDescription("A collection of settings for the symmetry mode")]
    [MoonSharpUserData]
    public class SymmetrySettingsApiWrapper
    {
        private SymmetryMode _Mode;

        private Vector3 _Position;
        private Quaternion _Rotation;
        private Vector3 _Spin;

        private SymmetryPointType _PointType;
        private int _PointOrder;

        private SymmetryWallpaperType _WallpaperType;
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
                _Mode = SymmetryMode.None;

                switch (PointerManager.m_Instance.CurrentSymmetryMode)
                {
                    case PointerManager.SymmetryMode.MultiMirror:

                        if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Point)
                        {
                            _Mode = SymmetryMode.Point;
                            _PointType = (SymmetryPointType)PointerManager.m_Instance.m_PointSymmetryFamily;
                            _PointOrder = PointerManager.m_Instance.m_PointSymmetryOrder;
                        }
                        else
                        {
                            _Mode = SymmetryMode.Wallpaper;
                            _WallpaperType = (SymmetryWallpaperType)PointerManager.m_Instance.m_WallpaperSymmetryGroup;
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
                        _Mode = SymmetryMode.Scripted;
                        break;
                    case PointerManager.SymmetryMode.SinglePlane:
                        _Mode = SymmetryMode.Standard;
                        break;
                    case PointerManager.SymmetryMode.TwoHanded:
                        _Mode = SymmetryMode.TwoHanded;
                        break;
                    case PointerManager.SymmetryMode.None:
                        _Mode = SymmetryMode.None;
                        break;
                }
                var widgetTr = PointerManager.m_Instance.SymmetryWidget.transform;
                _Position = widgetTr.position;
                _Rotation = widgetTr.rotation;
                _Spin = PointerManager.m_Instance.SymmetryWidget.GetSpin();
                _Current = this;
            }
        }

        [LuaDocsDescription("Creates a copy of these symmetry settings")]
        [LuaDocsExample("newSettings = Symmetry.current:Duplicate()")]
        public SymmetrySettingsApiWrapper Duplicate()
        {
            return new SymmetrySettingsApiWrapper(false)
            {
                _Mode = _Mode,

                _Position = _Position,
                _Rotation = _Rotation,
                _Spin = _Spin,

                _PointType = _PointType,
                _PointOrder = _PointOrder,

                _WallpaperType = _WallpaperType,
                _WallpaperRepeatX = _WallpaperRepeatX,
                _WallpaperRepeatY = _WallpaperRepeatY,
                _WallpaperScale = _WallpaperScale,
                _WallpaperScaleX = _WallpaperScaleX,
                _WallpaperScaleY = _WallpaperScaleY,
                _WallpaperSkewX = _WallpaperSkewX,
                _WallpaperSkewY = _WallpaperSkewY,
            };
        }

        [LuaDocsDescription("The symmetry mode")]
        public SymmetryMode mode
        {
            get => _Mode;
            set
            {
                _Mode = value;
                if (_IsCurrent)
                {
                    switch (value)
                    {
                        case SymmetryMode.None:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.None);
                            break;
                        case SymmetryMode.Standard:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.SinglePlane);
                            break;
                        case SymmetryMode.Scripted:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.ScriptedSymmetryMode);
                            break;
                        case SymmetryMode.TwoHanded:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.TwoHanded);
                            break;
                        case SymmetryMode.Point:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
                            break;
                        case SymmetryMode.Wallpaper:
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
                            break;
                    }
                }
            }
        }

        [LuaDocsDescription("The transform of the symmetry widget")]
        public TransformApiWrapper transform
        {
            get => new(TrTransform.TR(_Position, _Rotation));
            set
            {
                _Position = value._TrTransform.translation;
                _Rotation = value._TrTransform.rotation;
                if (_IsCurrent) _SetTransform(value._TrTransform);
            }
        }

        [LuaDocsDescription("The position of the symmetry widget")]
        public Vector3ApiWrapper position
        {
            get => new(_Position);
            set
            {
                _Position = value._Vector3;
                if (_IsCurrent) _SetTransform(TrTransform.TR(value._Vector3, _Rotation));
            }
        }

        [LuaDocsDescription("The rotation of the symmetry widget")]
        public RotationApiWrapper rotation
        {
            get => new(_Rotation);
            set
            {
                _Rotation = value._Quaternion;
                if (_IsCurrent) _SetTransform(TrTransform.TR(_Position, value._Quaternion));
            }
        }

        private void _SetTransform(TrTransform tr)
        {
            SymmetryWidget widget = PointerManager.m_Instance.SymmetryWidget;
            widget.LocalTransform = tr;
        }

        [LuaDocsDescription("How fast the symmetry widget is spinning in each axis")]
        public Vector3ApiWrapper spin
        {
            get => new(_Spin);
            set
            {
                _Spin = value._Vector3;
                if (_IsCurrent) PointerManager.m_Instance.SymmetryWidget.Spin(value.x, value.y, value.z);
            }
        }

        [LuaDocsDescription("The type of point symmetry")]
        public SymmetryPointType pointType
        {
            get => _PointType;
            set
            {
                _PointType = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_PointSymmetryFamily = (PointSymmetry.Family)value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The order of point symmetry (how many times it repeats around it's axis)")]
        public int pointOrder
        {
            get => _PointOrder;
            set
            {
                _PointOrder = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_PointSymmetryOrder = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The type of wallpaper symmetry")]
        public SymmetryWallpaperType wallpaperType
        {
            get => _WallpaperType;
            set
            {
                _WallpaperType = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetryGroup = (SymmetryGroup.R)value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("How many times the wallpaper symmetry repeats in the X axis")]
        public int wallpaperRepeatX
        {
            get => _WallpaperRepeatX;
            set
            {
                _WallpaperRepeatX = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetryX = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("How many times the wallpaper symmetry repeats in the Y axis")]
        public int wallpaperRepeatY
        {
            get => _WallpaperRepeatY;
            set
            {
                _WallpaperRepeatY = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetryY = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The overall scale of the wallpaper symmetry")]
        public float wallpaperScale
        {
            get => _WallpaperScale;
            set
            {
                _WallpaperScale = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetryScale = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The scale of the wallpaper symmetry in the X axis")]
        public float wallpaperScaleX
        {
            get => _WallpaperScaleX;
            set
            {
                _WallpaperScaleX = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetryScaleX = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The scale of the wallpaper symmetry in the Y axis")]
        public float wallpaperScaleY
        {
            get => _WallpaperScaleY;
            set
            {
                _WallpaperScaleY = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetryScaleY = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The skew of the wallpaper symmetry in the X axis")]
        public float wallpaperSkewX
        {
            get => _WallpaperSkewX;
            set
            {
                _WallpaperSkewX = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetrySkewX = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("The skew of the wallpaper symmetry in the Y axis")]
        public float wallpaperSkewY
        {
            get => _WallpaperSkewY;
            set
            {
                _WallpaperSkewY = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_WallpaperSymmetrySkewY = value;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [MoonSharpHidden]
        public static void _WriteToScene(SymmetrySettingsApiWrapper settings)
        {
            var newMode = PointerManager.SymmetryMode.None;
            switch (settings._Mode)
            {
                case SymmetryMode.None:
                    newMode = PointerManager.SymmetryMode.None;
                    break;
                case SymmetryMode.Standard:
                    newMode = PointerManager.SymmetryMode.SinglePlane;
                    break;
                case SymmetryMode.Scripted:
                    newMode = PointerManager.SymmetryMode.ScriptedSymmetryMode;
                    break;
                case SymmetryMode.TwoHanded:
                    newMode = PointerManager.SymmetryMode.TwoHanded;
                    break;
                case SymmetryMode.Point:
                    PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                    PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
                    PointerManager.m_Instance.m_PointSymmetryFamily = (PointSymmetry.Family)settings._PointType;
                    PointerManager.m_Instance.m_PointSymmetryOrder = settings._PointOrder;
                    break;
                case SymmetryMode.Wallpaper:
                    PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                    PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
                    PointerManager.m_Instance.m_WallpaperSymmetryGroup = (SymmetryGroup.R)settings._WallpaperType;
                    PointerManager.m_Instance.m_WallpaperSymmetryX = settings._WallpaperRepeatX;
                    PointerManager.m_Instance.m_WallpaperSymmetryY = settings._WallpaperRepeatY;
                    PointerManager.m_Instance.m_WallpaperSymmetryScale = settings._WallpaperScale;
                    PointerManager.m_Instance.m_WallpaperSymmetryScaleX = settings._WallpaperScaleX;
                    PointerManager.m_Instance.m_WallpaperSymmetryScaleY = settings._WallpaperScaleY;
                    PointerManager.m_Instance.m_WallpaperSymmetrySkewX = settings._WallpaperSkewX;
                    PointerManager.m_Instance.m_WallpaperSymmetrySkewY = settings._WallpaperSkewY;
                    break;
            }
            var widget = PointerManager.m_Instance.SymmetryWidget;
            var tr = TrTransform.TR(settings._Position, settings._Rotation);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
            );
            PointerManager.m_Instance.SymmetryWidget.Spin(settings._Spin.x, settings._Spin.y, settings._Spin.z);

            // Set mode last so we recalculate mirrors etc with correct settings
            PointerManager.m_Instance.SetSymmetryMode(newMode);
        }
    }
}
