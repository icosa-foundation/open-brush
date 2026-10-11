using System.Collections.Generic;
using System.Linq;
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
        private int _PenroseSubdivisions = 4;
        private float _PenroseRadius = 2f;
        private SymmetryPenroseTiles _PenroseTiles = SymmetryPenroseTiles.Both;
        private SymmetryLineFamily _LineFamily = SymmetryLineFamily.ScrewHalfTurns;
        private int _LineOrder = 5;
        private int _LineRepeats = 8;
        private float _LineAdvance = 0.5f;
        private float _LineAngle = 27f;
        private int _SpaceGroupNumber = 19;
        private Vector3Int _SpaceGroupRepeats = new Vector3Int(2, 2, 2);
        private float _SpaceGroupCellSize = 1f;
        private int _HelicalCopies = 8;
        private float _HelicalAngle = 30f;
        private float _HelicalAdvance = 0.5f;

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
                        else if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Penrose)
                        {
                            _Mode = SymmetryMode.Penrose;
                            _PenroseSubdivisions = PointerManager.m_Instance.m_PenroseSubdivisions;
                            _PenroseRadius = PointerManager.m_Instance.m_PenroseRadius;
                            _PenroseTiles = (SymmetryPenroseTiles)PointerManager.m_Instance.m_PenroseTiles;
                        }
                        else if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.LineGroup)
                        {
                            _Mode = SymmetryMode.LineGroup;
                            _LineFamily = (SymmetryLineFamily)PointerManager.m_Instance.m_LineFamily;
                            _LineOrder = PointerManager.m_Instance.m_LineOrder;
                            _LineRepeats = PointerManager.m_Instance.m_LineRepeats;
                            _LineAdvance = PointerManager.m_Instance.m_LineAdvance;
                            _LineAngle = PointerManager.m_Instance.m_LineAngle;
                        }
                        else if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.SpaceGroup)
                        {
                            _Mode = SymmetryMode.SpaceGroup;
                            _SpaceGroupNumber = PointerManager.m_Instance.m_SpaceGroupNumber;
                            _SpaceGroupRepeats = PointerManager.m_Instance.m_SpaceGroupRepeats;
                            _SpaceGroupCellSize = PointerManager.m_Instance.m_SpaceGroupCellSize;
                        }
                        else if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Helical)
                        {
                            _Mode = SymmetryMode.Helical;
                            _HelicalCopies = PointerManager.m_Instance.m_HelicalCopies;
                            _HelicalAngle = PointerManager.m_Instance.m_HelicalAngle;
                            _HelicalAdvance = PointerManager.m_Instance.m_HelicalAdvance;
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

        [LuaDocsDescription("Creates a new set of symmetry settings based on point symmetry")]
        [LuaDocsExample("mySymSettings = Symmetry:NewPoint()")]
        [LuaDocsParameter("type", "The type of point symmetry")]
        [LuaDocsParameter("order", "The number of repeats around the axis")]
        public static SymmetrySettingsApiWrapper NewPointSymmetry(SymmetryPointType type, int order)
        {
            var instance = new SymmetrySettingsApiWrapper(false)
            {
                _Mode = SymmetryMode.Point,
                _PointType = type,
                _PointOrder = order
            };
            return instance;
        }

        [LuaDocsDescription("Creates a new set of symmetry settings based on wallpaper symmetry")]
        [LuaDocsExample("mySymSettings = Symmetry:NewPoint()")]
        [LuaDocsParameter("type", "The type of point symmetry")]
        [LuaDocsParameter("order", "The number of repeats around the axis")]
        public static SymmetrySettingsApiWrapper NewWallpaperSymmetry(
            SymmetryWallpaperType type,
            int repeatX, int repeatY,
            float scale = 1, float scaleX = 1, float scaleY = 1,
            float skewX = 0, float skewY = 0)
        {
            var instance = new SymmetrySettingsApiWrapper(false)
            {
                _Mode = SymmetryMode.Wallpaper,
                _WallpaperType = type,
                _WallpaperRepeatX = repeatX,
                _WallpaperRepeatY = repeatY,
                _WallpaperScale = scale,
                _WallpaperScaleX = scaleX,
                _WallpaperScaleY = scaleY,
                _WallpaperSkewX = skewX,
                _WallpaperSkewY = skewY
            };
            return instance;
        }

        [LuaDocsDescription("The list of transform matrices for this symmetry mode")]
        public MatrixListApiWrapper matrices
        {
            get
            {
                List<Matrix4x4> matrices;
                if (_IsCurrent)
                {
                    matrices = PointerManager.m_Instance.CustomMirrorMatrices;
                }
                else
                {
                    if (mode == SymmetryMode.Wallpaper)
                    {
                        var wallpaperSym = new WallpaperSymmetry((SymmetryGroup.R)wallpaperType,
                            wallpaperRepeatX, wallpaperRepeatY,
                            wallpaperScale, wallpaperScaleX, wallpaperScaleY,
                            wallpaperSkewX, wallpaperSkewY
                        );
                        matrices = wallpaperSym.matrices;
                    }
                    else if (mode == SymmetryMode.Helical)
                    {
                        matrices = PointerManager.CreateHelicalMatrices(helicalCopies, helicalAngle, helicalAdvance);
                    }
                    else if (mode == SymmetryMode.Point)
                    {
                        var pointSym = new PointSymmetry((PointSymmetry.Family)pointType, pointOrder);
                        matrices = pointSym.matrices;
                    }
                    else
                    {
                        matrices = new List<Matrix4x4>();
                    }
                }
                return new MatrixListApiWrapper(matrices);
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
                _PenroseSubdivisions = _PenroseSubdivisions,
                _PenroseRadius = _PenroseRadius,
                _PenroseTiles = _PenroseTiles,
                _LineFamily = _LineFamily,
                _LineOrder = _LineOrder,
                _LineRepeats = _LineRepeats,
                _LineAdvance = _LineAdvance,
                _LineAngle = _LineAngle,
                _SpaceGroupNumber = _SpaceGroupNumber,
                _SpaceGroupRepeats = _SpaceGroupRepeats,
                _SpaceGroupCellSize = _SpaceGroupCellSize,
                _HelicalCopies = _HelicalCopies,
                _HelicalAngle = _HelicalAngle,
                _HelicalAdvance = _HelicalAdvance,

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
                        case SymmetryMode.Penrose:
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Penrose;
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.CalculateMirrors();
                            break;
                        case SymmetryMode.LineGroup:
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.LineGroup;
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.CalculateMirrors();
                            break;
                        case SymmetryMode.SpaceGroup:
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.SpaceGroup;
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.CalculateMirrors();
                            break;
                        case SymmetryMode.Helical:
                            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Helical;
                            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                            PointerManager.m_Instance.CalculateMirrors();
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

        [LuaDocsDescription("Penrose subdivision depth (1–7)")]
        public int penroseSubdivisions
        {
            get => _PenroseSubdivisions;
            set
            {
                _PenroseSubdivisions = Mathf.Clamp(value, 1, 7);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_PenroseSubdivisions = _PenroseSubdivisions;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Penrose seed radius in canvas units")]
        public float penroseRadius
        {
            get => _PenroseRadius;
            set
            {
                _PenroseRadius = Mathf.Max(0.01f, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_PenroseRadius = _PenroseRadius;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Penrose tile types receiving copies")]
        public SymmetryPenroseTiles penroseTiles
        {
            get => _PenroseTiles;
            set
            {
                _PenroseTiles = (SymmetryPenroseTiles)Mathf.Clamp((int)value, 0, 2);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_PenroseTiles = (PenroseTiling.TileSelection)_PenroseTiles;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Line-group family (1–13)")]
        public SymmetryLineFamily lineFamily
        {
            get => _LineFamily;
            set
            {
                _LineFamily = (SymmetryLineFamily)Mathf.Clamp((int)value, 1, 13);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_LineFamily = (LineGroupSymmetry.Family)_LineFamily;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Axial rotation order, including noncrystallographic orders")]
        public int lineOrder
        {
            get => _LineOrder;
            set
            {
                _LineOrder = Mathf.Max(1, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_LineOrder = _LineOrder;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Number of generalized steps along Z")]
        public int lineRepeats
        {
            get => _LineRepeats;
            set
            {
                _LineRepeats = Mathf.Max(1, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_LineRepeats = _LineRepeats;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Advance per generalized step in canvas units")]
        public float lineAdvance
        {
            get => _LineAdvance;
            set
            {
                _LineAdvance = Mathf.Max(0f, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_LineAdvance = _LineAdvance;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Twist per step in degrees for families 1 and 5")]
        public float lineAngle
        {
            get => _LineAngle;
            set
            {
                _LineAngle = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_LineAngle = _LineAngle;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Space group number (1–230)")]
        public int spaceGroup
        {
            get => _SpaceGroupNumber;
            set
            {
                _SpaceGroupNumber = Mathf.Clamp(value, 1, 230);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_SpaceGroupNumber = _SpaceGroupNumber;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Cell repeats along X")]
        public int spaceRepeatX
        {
            get => _SpaceGroupRepeats.x;
            set
            {
                _SpaceGroupRepeats.x = Mathf.Max(1, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_SpaceGroupRepeats = _SpaceGroupRepeats;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Cell repeats along Y")]
        public int spaceRepeatY
        {
            get => _SpaceGroupRepeats.y;
            set
            {
                _SpaceGroupRepeats.y = Mathf.Max(1, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_SpaceGroupRepeats = _SpaceGroupRepeats;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Cell repeats along Z")]
        public int spaceRepeatZ
        {
            get => _SpaceGroupRepeats.z;
            set
            {
                _SpaceGroupRepeats.z = Mathf.Max(1, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_SpaceGroupRepeats = _SpaceGroupRepeats;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Space group cell size in canvas units")]
        public float spaceCellSize
        {
            get => _SpaceGroupCellSize;
            set
            {
                _SpaceGroupCellSize = Mathf.Max(0.01f, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_SpaceGroupCellSize = _SpaceGroupCellSize;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Number of helical copies, including the original placement")]
        public int helicalCopies
        {
            get => _HelicalCopies;
            set
            {
                _HelicalCopies = Mathf.Max(1, value);
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_HelicalCopies = _HelicalCopies;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Rotation per helical copy in degrees")]
        public float helicalAngle
        {
            get => _HelicalAngle;
            set
            {
                _HelicalAngle = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_HelicalAngle = _HelicalAngle;
                    PointerManager.m_Instance.CalculateMirrors();
                }
            }
        }

        [LuaDocsDescription("Advance per helical copy along the mirror Y axis in canvas units")]
        public float helicalAdvance
        {
            get => _HelicalAdvance;
            set
            {
                _HelicalAdvance = value;
                if (_IsCurrent)
                {
                    PointerManager.m_Instance.m_HelicalAdvance = _HelicalAdvance;
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
                case SymmetryMode.Penrose:
                    newMode = PointerManager.SymmetryMode.MultiMirror;
                    PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Penrose;
                    PointerManager.m_Instance.m_PenroseSubdivisions = settings._PenroseSubdivisions;
                    PointerManager.m_Instance.m_PenroseRadius = settings._PenroseRadius;
                    PointerManager.m_Instance.m_PenroseTiles = (PenroseTiling.TileSelection)settings._PenroseTiles;
                    break;
                case SymmetryMode.LineGroup:
                    newMode = PointerManager.SymmetryMode.MultiMirror;
                    PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.LineGroup;
                    PointerManager.m_Instance.m_LineFamily = (LineGroupSymmetry.Family)settings._LineFamily;
                    PointerManager.m_Instance.m_LineOrder = settings._LineOrder;
                    PointerManager.m_Instance.m_LineRepeats = settings._LineRepeats;
                    PointerManager.m_Instance.m_LineAdvance = settings._LineAdvance;
                    PointerManager.m_Instance.m_LineAngle = settings._LineAngle;
                    break;
                case SymmetryMode.SpaceGroup:
                    newMode = PointerManager.SymmetryMode.MultiMirror;
                    PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.SpaceGroup;
                    PointerManager.m_Instance.m_SpaceGroupNumber = settings._SpaceGroupNumber;
                    PointerManager.m_Instance.m_SpaceGroupRepeats = settings._SpaceGroupRepeats;
                    PointerManager.m_Instance.m_SpaceGroupCellSize = settings._SpaceGroupCellSize;
                    break;
                case SymmetryMode.Helical:
                    newMode = PointerManager.SymmetryMode.MultiMirror;
                    PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Helical;
                    PointerManager.m_Instance.m_HelicalCopies = settings._HelicalCopies;
                    PointerManager.m_Instance.m_HelicalAngle = settings._HelicalAngle;
                    PointerManager.m_Instance.m_HelicalAdvance = settings._HelicalAdvance;
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
            if (newMode == PointerManager.SymmetryMode.MultiMirror)
            { PointerManager.m_Instance.CalculateMirrors(); }
        }
    }
}
