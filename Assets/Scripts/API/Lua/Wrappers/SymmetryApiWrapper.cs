using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class SymmetryApiWrapper
    {
        public static TrTransform transform
        {
            get => TrTransform.FromTransform(PointerManager.m_Instance.SymmetryWidget.transform);
            set => ApiMethods._SymmetrySetTransform(value.translation, value.rotation);
        }
        public static Vector3 position
        {
            get => PointerManager.m_Instance.SymmetryWidget.transform.position;
            set => ApiMethods.SymmetrySetPosition(value);
        }
        public static Quaternion rotation
        {
            get => PointerManager.m_Instance.SymmetryWidget.transform.rotation;
            set => ApiMethods._SymmetrySetRotation(value);
        }

        public static Vector3 brushOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastBrushPos(0))).translation;
        public static Vector3 wandOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastWandPos(0))).translation;
        public static Vector3 direction => PointerManager.m_Instance.SymmetryWidget.transform.rotation * Vector3.forward;
        public static void Mirror() => ApiMethods.SymmetryPlane();
        public static void DoubleMirror() => ApiMethods.SymmetryFour();
        public static void TwoHandeded() => ApiMethods.SymmetryTwoHanded();
        public static void SummonWidget() => ApiMethods.SummonMirror();
        public static void Spin(float xSpeed, float ySpeed, float zSpeed) => PointerManager.m_Instance.SymmetryWidget.Spin(xSpeed, ySpeed, zSpeed);
        public static float Ellipse(float angle, float minorRadius)
        {
            return minorRadius / Mathf.Sqrt(Mathf.Pow(minorRadius * Mathf.Cos(angle), 2) + Mathf.Pow(Mathf.Sin(angle), 2));
        }
        public static float Square(float angle)
        {
            const float halfEdgeLength = 0.5f;
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));
            float maxComponent = Mathf.Max(x, y);
            return halfEdgeLength / maxComponent;
        }
        public static float Superellipse(float angle, float n, float a = 1f, float b = 1f)
        {
            float cosTheta = Mathf.Cos(angle);
            float sinTheta = Mathf.Sin(angle);
            float cosThetaN = Mathf.Pow(Mathf.Abs(cosTheta), n);
            float sinThetaN = Mathf.Pow(Mathf.Abs(sinTheta), n);
            float radius = Mathf.Pow(Mathf.Pow(a, n) * cosThetaN + Mathf.Pow(b, n) * sinThetaN, -1 / n);
            return radius;
        }
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

        public static void ClearColors(List<Color> colors)
        {
            PointerManager.m_Instance.SymmetryPointerColors.Clear();
        }

        public static void AddColor(Color color)
        {
            PointerManager.m_Instance.SymmetryPointerColors.Add(color);
        }

        public static void SetColors(List<Color> colors)
        {
            PointerManager.m_Instance.SymmetryPointerColors = colors;
        }

        public static List<Color> GetColors()
        {
            return PointerManager.m_Instance.SymmetryPointerColors;
        }

        public static void AddBrush(string brush)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes.Add(
                ApiMethods.LookupBrushDescriptor(brush)
            );
        }

        public static void ClearBrushes(List<string> brushes)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes .Clear();
        }

        public static void SetBrushes(List<string> brushes)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes = brushes.Select(
                x => ApiMethods.LookupBrushDescriptor(x)
            ).Where(x => x != null).ToList();
        }

        public static List<string> GetBrushNames()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.Description
            ).ToList();
        }

        public static List<string> GetBrushGuids()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.m_Guid.ToString()
            ).ToList();
        }

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
