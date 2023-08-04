using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    public enum ApiSymmetryMode
    {
        None = PointerManager.SymmetryMode.None,
        Standard = PointerManager.SymmetryMode.SinglePlane,
        Scripted = PointerManager.SymmetryMode.ScriptedSymmetryMode,
        TwoHanded = PointerManager.SymmetryMode.TwoHanded,
        Point,
        Wallpaper
    }

    [LuaDocsDescription("Represents the settings for the symmetry mode")]
    [MoonSharpUserData]
    public class SymmetrySettingsApiWrapper
    {
        public ApiSymmetryMode mode;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 spin;

        public PointSymmetry.Family pointType;
        public int pointOrder;

        public SymmetryGroup.R wallpaperType;
        public int wallpaperRepeatX;
        public int wallpaperRepeatY;
        public float wallpaperScale;
        public float wallpaperScaleX;
        public float wallpaperScaleY;
        public float wallpaperSkewX;
        public float wallpaperSkewY;
    }

    [LuaDocsDescription("Functions for controlling the mirror symmetry mode")]
    [MoonSharpUserData]
    public static class SymmetryApiWrapper
    {
        [LuaDocsDescription("Gets the offset betwen the current brush position and the symmetry widget")]
        public static Vector3 brushOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastBrushPos(0))).translation;
        [LuaDocsDescription("Gets the offset betwen the current wand position and the symmetry widget")]
        public static Vector3 wandOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastWandPos(0))).translation;

        [LuaDocsDescription("The current symmetry settings")]
        public static SymmetrySettingsApiWrapper settings
        {
            get
            {
                var settings = new SymmetrySettingsApiWrapper();
                var mode = ApiSymmetryMode.None;

                switch (PointerManager.m_Instance.CurrentSymmetryMode)
                {
                    case PointerManager.SymmetryMode.MultiMirror:

                        if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Point)
                        {
                            settings.mode = ApiSymmetryMode.Point;
                            settings.pointType = PointerManager.m_Instance.m_PointSymmetryFamily;
                            settings.pointOrder = PointerManager.m_Instance.m_PointSymmetryOrder;
                        }
                        else
                        {
                            settings.mode = ApiSymmetryMode.Wallpaper;
                            settings.wallpaperType = PointerManager.m_Instance.m_WallpaperSymmetryGroup;
                            settings.wallpaperScale = PointerManager.m_Instance.m_WallpaperSymmetryScale;
                            settings.wallpaperScaleX = PointerManager.m_Instance.m_WallpaperSymmetryScaleX;
                            settings.wallpaperScaleY = PointerManager.m_Instance.m_WallpaperSymmetryScaleY;
                            settings.wallpaperSkewX = PointerManager.m_Instance.m_WallpaperSymmetrySkewX;
                            settings.wallpaperSkewY = PointerManager.m_Instance.m_WallpaperSymmetrySkewY;
                        }
                        break;
                    case PointerManager.SymmetryMode.ScriptedSymmetryMode:
                        mode = ApiSymmetryMode.Scripted;
                        break;
                    case PointerManager.SymmetryMode.SinglePlane:
                        mode = ApiSymmetryMode.Standard;
                        break;
                    case PointerManager.SymmetryMode.TwoHanded:
                        mode = ApiSymmetryMode.TwoHanded;
                        break;
                    case PointerManager.SymmetryMode.None:
                        mode = ApiSymmetryMode.None;
                        break;
                }
                var widgetTr = PointerManager.m_Instance.SymmetryWidget.transform;
                settings.position = widgetTr.position;
                settings.rotation = widgetTr.rotation;
                settings.spin = PointerManager.m_Instance.SymmetryWidget.GetSpin();
                return settings;
            }
            set
            {
                switch (value.mode)
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
                        PointerManager.m_Instance.m_PointSymmetryFamily = value.pointType;
                        PointerManager.m_Instance.m_PointSymmetryOrder = value.pointOrder;
                        break;
                    case ApiSymmetryMode.Wallpaper:
                        PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
                        PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
                        PointerManager.m_Instance.m_WallpaperSymmetryGroup = value.wallpaperType;
                        PointerManager.m_Instance.m_WallpaperSymmetryScale = value.wallpaperScale;
                        PointerManager.m_Instance.m_WallpaperSymmetryScaleX = value.wallpaperScaleX;
                        PointerManager.m_Instance.m_WallpaperSymmetryScaleY = value.wallpaperScaleY;
                        PointerManager.m_Instance.m_WallpaperSymmetrySkewX = value.wallpaperSkewX;
                        PointerManager.m_Instance.m_WallpaperSymmetrySkewY = value.wallpaperSkewY;
                        break;
                }
                var widget = PointerManager.m_Instance.SymmetryWidget;
                var tr = TrTransform.TR(value.position, value.rotation);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
                );
                PointerManager.m_Instance.SymmetryWidget.Spin(value.spin.x, value.spin.y, value.spin.z);
            }
        }

        [LuaDocsDescription("")]
        public static void SummonWidget() => ApiMethods.SummonMirror();

        [LuaDocsDescription("")]
        public static float Ellipse(float angle, float minorRadius)
        {
            return minorRadius / Mathf.Sqrt(Mathf.Pow(minorRadius * Mathf.Cos(angle), 2) + Mathf.Pow(Mathf.Sin(angle), 2));
        }

        [LuaDocsDescription("")]
        public static float Square(float angle)
        {
            const float halfEdgeLength = 0.5f;
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));
            float maxComponent = Mathf.Max(x, y);
            return halfEdgeLength / maxComponent;
        }

        [LuaDocsDescription("")]
        public static float Superellipse(float angle, float n, float a = 1f, float b = 1f)
        {
            float cosTheta = Mathf.Cos(angle);
            float sinTheta = Mathf.Sin(angle);
            float cosThetaN = Mathf.Pow(Mathf.Abs(cosTheta), n);
            float sinThetaN = Mathf.Pow(Mathf.Abs(sinTheta), n);
            float radius = Mathf.Pow(Mathf.Pow(a, n) * cosThetaN + Mathf.Pow(b, n) * sinThetaN, -1 / n);
            return radius;
        }

        [LuaDocsDescription("")]
        public static float Rsquare(float angle, float halfSideLength, float cornerRadius)
        {
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));

            // Check if the point lies in the rounded corner area
            if (x > halfSideLength - cornerRadius && y > halfSideLength - cornerRadius)
            {
                // Calculate the distance to the rounded corner center
                float dx = x - (halfSideLength - cornerRadius);
                float dy = y - (halfSideLength - cornerRadius);
                float distanceToCornerCenter = Mathf.Sqrt(dx * dx + dy * dy);

                // Calculate the distance to the rounded corner edge
                return halfSideLength + cornerRadius - distanceToCornerCenter;
            }
            // Calculate the distance to the square edge as before
            float maxComponent = Mathf.Max(x, y);
            return halfSideLength / maxComponent;
        }

        [LuaDocsDescription("")]
        public static float Polygon(float angle, int numSides, float radius=1f)
        {
            // Calculate the angle of each sector in the polygon
            float sectorAngle = 2 * Mathf.PI / numSides;

            // Find the nearest vertex by rounding the angle to the nearest sector angle
            float nearestVertexAngle = Mathf.Round(angle / sectorAngle) * sectorAngle;

            // Calculate the bisector angle (half of the sector angle)
            float bisectorAngle = sectorAngle / 2;

            // Calculate the distance from the center to the midpoint of the edge
            float apothem = radius * Mathf.Cos(bisectorAngle);

            // Calculate the angle between the input angle and the nearest vertex angle
            float deltaAngle = Mathf.Abs(angle - nearestVertexAngle);

            // Calculate the distance from the midpoint of the edge to the point on the edge at the given angle
            float edgePointDistance = apothem * Mathf.Tan(deltaAngle);

            // Calculate the distance from the center to the point on the edge at the given angle
            float distanceToEdge = Mathf.Sqrt(apothem * apothem + edgePointDistance * edgePointDistance);

            return distanceToEdge;
        }

        [LuaDocsDescription("")]
        public static void ClearColors(List<Color> colors)
        {
            PointerManager.m_Instance.SymmetryPointerColors.Clear();
        }

        [LuaDocsDescription("")]
        public static void AddColor(Color color)
        {
            PointerManager.m_Instance.SymmetryPointerColors.Add(color);
        }

        [LuaDocsDescription("")]
        public static void SetColors(List<Color> colors)
        {
            PointerManager.m_Instance.SymmetryPointerColors = colors;
        }

        [LuaDocsDescription("")]
        public static List<Color> GetColors()
        {
            return PointerManager.m_Instance.SymmetryPointerColors;
        }

        [LuaDocsDescription("")]
        public static void AddBrush(string brush)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes.Add(
                ApiMethods.LookupBrushDescriptor(brush)
            );
        }

        [LuaDocsDescription("")]
        public static void ClearBrushes(List<string> brushes)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes .Clear();
        }

        [LuaDocsDescription("")]
        public static void SetBrushes(List<string> brushes)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes = brushes.Select(
                x => ApiMethods.LookupBrushDescriptor(x)
            ).Where(x => x != null).ToList();
        }

        [LuaDocsDescription("")]
        public static List<string> GetBrushNames()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.Description
            ).ToList();
        }

        [LuaDocsDescription("")]
        public static List<string> GetBrushGuids()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.m_Guid.ToString()
            ).ToList();
        }

        [LuaDocsDescription("")]
        public static PathApiWrapper PathToPolar(IPathApiWrapper path)
        {
            return new PathApiWrapper(Path2dToPolar(path.AsSingleTrList().Select(x =>
            {
                var vector3 = x.translation;
                return new Vector2(vector3.x, vector3.y);
            }).ToList()));
        }

        // Converts an array of points centered on the origin to a list of TrTransforms
        // suitable for use with symmetry scripts default space
        private static List<TrTransform> Path2dToPolar(List<Vector2> cartesianPoints)
        {
            var polarCoordinates = new List<TrTransform>(cartesianPoints.Count);

            for (var i = 0; i < cartesianPoints.Count; i++)
            {
                var point = cartesianPoints[i];
                float radius = Mathf.Sqrt(point.x * point.x + point.y * point.y);
                float angle = Mathf.Atan2(point.y, point.x);

                polarCoordinates.Add(
                    TrTransform.TR(
                        new Vector3(
                            brushOffset.x * radius,
                            brushOffset.y,
                            brushOffset.z * radius
                        ),
                        Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0)
                    )
                );
            }
            return polarCoordinates;
        }
    }
}
