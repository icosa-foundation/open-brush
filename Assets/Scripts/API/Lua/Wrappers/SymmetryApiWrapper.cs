using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("The list of valid symmetry modes")]
    public enum SymmetryMode
    {
        None,
        Standard,
        Scripted,
        TwoHanded,
        Point,
        Wallpaper
    }

    [LuaDocsDescription("The list of valid point symmetry types")]
    public enum SymmetryPointType
    {
        Cn = PointSymmetry.Family.Cn,
        Cnv = PointSymmetry.Family.Cnv,
        Cnh = PointSymmetry.Family.Cnh,
        Sn = PointSymmetry.Family.Sn,
        Dn = PointSymmetry.Family.Dn,
        Dnh = PointSymmetry.Family.Dnh,
        Dnd = PointSymmetry.Family.Dnd,
        T = PointSymmetry.Family.T,
        Th = PointSymmetry.Family.Th,
        Td = PointSymmetry.Family.Td,
        O = PointSymmetry.Family.O,
        Oh = PointSymmetry.Family.Oh,
        I = PointSymmetry.Family.I,
        Ih = PointSymmetry.Family.Ih,
    }

    [LuaDocsDescription("The list of valid wallpaper symmetry types")]
    public enum SymmetryWallpaperType
    {
        p1 = SymmetryGroup.R.p1,
        pg = SymmetryGroup.R.pg,
        cm = SymmetryGroup.R.cm,
        pm = SymmetryGroup.R.pm,
        p6 = SymmetryGroup.R.p6,
        p6m = SymmetryGroup.R.p6m,
        p3 = SymmetryGroup.R.p3,
        p3m1 = SymmetryGroup.R.p3m1,
        p31m = SymmetryGroup.R.p31m,
        p4 = SymmetryGroup.R.p4,
        p4m = SymmetryGroup.R.p4m,
        p4g = SymmetryGroup.R.p4g,
        p2 = SymmetryGroup.R.p2,
        pgg = SymmetryGroup.R.pgg,
        pmg = SymmetryGroup.R.pmg,
        pmm = SymmetryGroup.R.pmm,
        cmm = SymmetryGroup.R.cmm,
    }

    [LuaDocsDescription("Functions for controlling the mirror symmetry mode")]
    [MoonSharpUserData]
    public static class SymmetryApiWrapper
    {

        [LuaDocsDescription("The current symmetry settings")]
        public static SymmetrySettingsApiWrapper current
        {
            get => new(isCurrent: true);
            set
            {
                SymmetrySettingsApiWrapper._WriteToScene(value);
            }
        }

        [LuaDocsDescription("Gets the offset between the current brush position and the symmetry widget")]
        public static Vector3 brushOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastBrushPos(0))).translation;
        [LuaDocsDescription("Gets the offset between the current wand position and the symmetry widget")]
        public static Vector3 wandOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastWandPos(0))).translation;

        [LuaDocsDescription("Moves the Symmetry Widget close to user")]
        [LuaDocsExample("Symmetry:SummonWidget()")]
        public static void SummonWidget() => ApiMethods.SummonMirror();

        [LuaDocsDescription("Returns the radius of an ellipse at a given angle")]
        [LuaDocsParameter("angle", "The angle in degrees to sample the radius at")]
        [LuaDocsParameter("minorRadius", "The minor radius of the ellipse (The major radius is always 1)")]
        [LuaDocsExample("for i = 0, 90 do\n" +
            "  radius = Symmetry:Ellipse(i * 4, 0.5)\n" +
            "  pointer = Transform:New(Symmetry.brushOffset:ScaleBy(radius, 1, radius))\n" +
            "  pointers:Insert(pointer)\n" +
            "end")]
        public static float Ellipse(float angle, float minorRadius)
        {
            return minorRadius / Mathf.Sqrt(Mathf.Pow(minorRadius * Mathf.Cos(angle), 2) + Mathf.Pow(Mathf.Sin(angle), 2));
        }

        [LuaDocsDescription("Returns the radius of an square at a given angle")]
        [LuaDocsParameter("angle", "The angle in degrees to sample the radius at")]
        [LuaDocsExample("for i = 0, 90 do\n" +
            "  radius = Symmetry:Square(i * 4)\n" +
            "  pointer = Transform:New(Symmetry.brushOffset:ScaleBy(radius, 1, radius))\n" +
            "  pointers:Insert(pointer)\n" +
            "end")]
        public static float Square(float angle)
        {
            const float halfEdgeLength = 0.5f;
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));
            float maxComponent = Mathf.Max(x, y);
            return halfEdgeLength / maxComponent;
        }

        [LuaDocsDescription("Returns the radius of a superellipse at a given angle")]
        [LuaDocsParameter("angle", "The angle in degrees to sample the radius at")]
        [LuaDocsParameter("n", "The exponent of the superellipse. " +
            "This determines the roundness vs sharpness of the corners of the superellipse. " +
            "For n = 2, you get an ellipse. As n increases, the shape becomes more rectangular with sharper corners. " +
            "As n approaches infinity, the superellipse becomes a rectangle. If n is less than 1, the shape becomes a star with pointed tips.")]
        [LuaDocsParameter("a", "The horizontal radius of the superellipse")]
        [LuaDocsParameter("b", "The vertical radius of the superellipse")]
        [LuaDocsExample("for i = 0, 90 do\n" +
            "  radius = Symmetry:Superellipse(i * 4, 2, 0.5, 0.5)\n" +
            "  pointer = Transform:New(Symmetry.brushOffset:ScaleBy(radius, 1, radius))\n" +
            "  pointers:Insert(pointer)\n" +
            "end")]
        public static float Superellipse(float angle, float n, float a = 1f, float b = 1f)
        {
            float cosTheta = Mathf.Cos(angle);
            float sinTheta = Mathf.Sin(angle);
            float cosThetaN = Mathf.Pow(Mathf.Abs(cosTheta), n);
            float sinThetaN = Mathf.Pow(Mathf.Abs(sinTheta), n);
            float radius = Mathf.Pow(Mathf.Pow(a, n) * cosThetaN + Mathf.Pow(b, n) * sinThetaN, -1 / n);
            return radius;
        }

        [LuaDocsDescription("Returns the radius of a rounded square at a given angle")]
        [LuaDocsParameter("angle", "The angle in degrees to sample the radius at")]
        [LuaDocsParameter("size", "Half the length of a side or the distance from the center to any edge midpoint")]
        [LuaDocsParameter("cornerRadius", "The radius of the rounded corners")]
        [LuaDocsExample("for i = 0, 90 do\n" +
            "  radius = Symmetry:Rsquare(i * 4, 0.5, 0.1)\n" +
            "  pointer = Transform:New(Symmetry.brushOffset:ScaleBy(radius, 1, radius))\n" +
            "  pointers:Insert(pointer)\n" +
            "end")]
        public static float Rsquare(float angle, float size, float cornerRadius)
        {
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));

            // Check if the point lies in the rounded corner area
            if (x > size - cornerRadius && y > size - cornerRadius)
            {
                // Calculate the distance to the rounded corner center
                float dx = x - (size - cornerRadius);
                float dy = y - (size - cornerRadius);
                float distanceToCornerCenter = Mathf.Sqrt(dx * dx + dy * dy);

                // Calculate the distance to the rounded corner edge
                return size + cornerRadius - distanceToCornerCenter;
            }
            // Calculate the distance to the square edge as before
            float maxComponent = Mathf.Max(x, y);
            return size / maxComponent;
        }

        [LuaDocsDescription("Returns the radius of a polygon at a given angle")]
        [LuaDocsParameter("angle", "The angle in degrees to sample the radius at")]
        [LuaDocsParameter("numSides", "The number of sides of the polygon")]
        [LuaDocsParameter("radius", "The distance from the center to any vertex")]
        [LuaDocsExample("for i = 0, 90 do\n" +
            "  radius = Symmetry:Polygon(i * 4, 5, 0.5)\n" +
            "  pointer = Transform:New(Symmetry.brushOffset:ScaleBy(radius, 1, radius))\n" +
            "  pointers:Insert(pointer)\n" +
            "end")]
        public static float Polygon(float angle, int numSides, float radius = 1f)
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

        [LuaDocsDescription("Clears the list of symmetry pointer colors")]
        [LuaDocsExample("Symmetry:ClearColors()")]
        public static void ClearColors()
        {
            PointerManager.m_Instance.SymmetryPointerColors.Clear();
        }

        [LuaDocsDescription("Adds a color to the list of symmetry pointer colors")]
        [LuaDocsParameter("color", "The color to add")]
        [LuaDocsExample("Symmetry:AddColor(Color.red)")]
        public static void AddColor(Color color)
        {
            PointerManager.m_Instance.SymmetryPointerColors.Add(color);
        }

        [LuaDocsDescription("Sets the list of symmetry pointer colors")]
        [LuaDocsParameter("colors", "The list of colors to set")]
        [LuaDocsExample("Symmetry:SetColors({Color.red, Color.green, Color.blue})")]
        public static void SetColors(List<Color> colors)
        {
            PointerManager.m_Instance.SymmetryPointerColors = colors;
        }

        [LuaDocsDescription("Gets the list of symmetry pointer colors")]
        [LuaDocsExample("myColors = Symmetry:GetColors()")]
        public static List<Color> GetColors()
        {
            return PointerManager.m_Instance.SymmetryPointerColors;
        }

        [LuaDocsDescription("Adds a brush to the list of symmetry pointer brushes")]
        [LuaDocsParameter("brush", "The brush to add. Either the name or the GUID of the brush")]
        [LuaDocsExample("Symmetry:AddBrush(\"Ink\")")]
        public static void AddBrush(string brush)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes.Add(
                ApiMethods.LookupBrushDescriptor(brush)
            );
        }

        [LuaDocsDescription("Clears the list of symmetry pointer brushes")]
        [LuaDocsExample("Symmetry:ClearBrushes()")]
        public static void ClearBrushes()
        {
            PointerManager.m_Instance.SymmetryPointerBrushes.Clear();
        }

        [LuaDocsDescription("Sets the list of symmetry pointer brushes")]
        [LuaDocsParameter("brushes", "The list of brushes to set. Either the names or the GUIDs of the brushes")]
        [LuaDocsExample("Symmetry:SetBrushes({\"Ink\", \"Marker\"})")]
        public static void SetBrushes(List<string> brushes)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes = brushes.Select(
                x => ApiMethods.LookupBrushDescriptor(x)
            ).Where(x => x != null).ToList();
        }

        [LuaDocsDescription("Gets the list of symmetry pointer brushes as brush names")]
        [LuaDocsExample("brushNames = Symmetry:GetBrushNames()")]
        public static List<string> GetBrushNames()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.Description
            ).ToList();
        }

        [LuaDocsDescription("Gets the list of symmetry pointer brushes as brush GUIDs")]
        [LuaDocsExample("brushGuids = Symmetry:GetBrushGuids()")]
        public static List<string> GetBrushGuids()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.m_Guid.ToString()
            ).ToList();
        }

        [LuaDocsDescription("Converts a path to a format suitable for using as a symmetry path")]
        [LuaDocsParameter("path", "The path to convert")]
        [LuaDocsExample("pointers = Symmetry:PathToPolar(myPath):OnY()")]
        public static PathApiWrapper PathToPolar(PathApiWrapper path)
        {
            return new PathApiWrapper(Path2dToPolar(path.AsSingleTrList().Select(x =>
            {
                var vector3 = x.translation;
                return new Vector2(vector3.x, vector3.y);
            }).ToList()));
        }

        // Converts an array of points centered on the origin to a list of TrTransforms
        // suitable for use with symmetry scripts default space
        [LuaDocsDescription("Converts a 2D path to a format suitable for using as a symmetry path")]
        [LuaDocsParameter("path", "The 2D path to convert")]
        [LuaDocsExample("pointers = Symmetry:PathToPolar(myPath)")]
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
