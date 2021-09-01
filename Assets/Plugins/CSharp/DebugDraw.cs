using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Debug Extension
/// 	- Static class that extends Unity's debugging functionallity.
/// 	- Attempts to mimic Unity's existing debugging behaviour for ease-of-use.
/// 	- Includes gizmo drawing methods for less memory-intensive debug visualization.
/// </summary>

public static partial class DebugDraw
{
    #region DebugDrawFunctions

    /// <summary>
    /// 	- Debugs a point.
    /// </summary>
    /// <param name='position'>
    /// 	- The point to debug.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the point.
    /// </param>
    /// <param name='scale'>
    /// 	- The size of the point.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the point.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not this point should be faded when behind other objects.
    /// </param>
    public static void Point(Vector3 position, Quaternion rotation, Color color, float scale = 1.0f, float duration = 0, bool depthTest = true)
    {
        color = (color == default(Color)) ? Color.white : color;

        Debug.DrawRay(position + rotation * (Vector3.up * (scale * 0.5f)), rotation * -Vector3.up * scale, color, duration, depthTest);
        Debug.DrawRay(position + rotation * (Vector3.right * (scale * 0.5f)), rotation * -Vector3.right * scale, color, duration, depthTest);
        Debug.DrawRay(position + rotation * (Vector3.forward * (scale * 0.5f)), rotation * -Vector3.forward * scale, color, duration, depthTest);
    }

    public static void Gnomon(Matrix4x4 trsMatrix, float duration = 0, bool depthTest = true)
    {
        Debug.DrawLine(trsMatrix.MultiplyPoint3x4(Vector3.right), trsMatrix.MultiplyPoint3x4(Vector3.left), Color.red, duration, depthTest);
        Debug.DrawLine(trsMatrix.MultiplyPoint3x4(Vector3.up), trsMatrix.MultiplyPoint3x4(Vector3.down), Color.green, duration, depthTest);
        Debug.DrawLine(trsMatrix.MultiplyPoint3x4(Vector3.forward), trsMatrix.MultiplyPoint3x4(Vector3.back), Color.blue, duration, depthTest);
    }

    public static void Gnomon(Matrix4x4 trsMatrix, float radiusA, float radiusB, float duration = 0, bool depthTest = true)
    {
        Debug.DrawLine(trsMatrix.MultiplyPoint3x4(Vector3.right * radiusA), trsMatrix.MultiplyPoint3x4(Vector3.right * radiusB), Color.red, duration, depthTest);
        Debug.DrawLine(trsMatrix.MultiplyPoint3x4(Vector3.up * radiusA), trsMatrix.MultiplyPoint3x4(Vector3.up * radiusB), Color.green, duration, depthTest);
        Debug.DrawLine(trsMatrix.MultiplyPoint3x4(Vector3.forward * radiusA), trsMatrix.MultiplyPoint3x4(Vector3.forward * radiusB), Color.blue, duration, depthTest);
    }

    public static void Gnomon(Vector3 position, Quaternion rotation, float radiusA = 0, float radiusB = 1, float duration = 0, bool depthTest = true)
    {
        Gnomon(Matrix4x4.TRS(position, rotation, Vector3.one), radiusA, radiusB, duration, depthTest);
    }

    public static void GnomonArrow(Vector3 position, Quaternion rotation, float scale = 1.0f, float duration = 0, bool depthTest = true)
    {
        DebugDraw.Arrow(position, rotation * Vector3.up * scale, Color.green, duration, depthTest);
        DebugDraw.Arrow(position, rotation * Vector3.right * scale, Color.red, duration, depthTest);
        DebugDraw.Arrow(position, rotation * Vector3.forward * scale, Color.blue, duration, depthTest);
    }


    public static void Point(Vector3 position, Color color, float scale = 1.0f, float duration = 0, bool depthTest = true)
    {
        Point(position, Quaternion.identity, color, scale, duration, depthTest);
    }


    /// <summary>
    /// 	- Debugs a point.
    /// </summary>
    /// <param name='position'>
    /// 	- The point to debug.
    /// </param>
    /// <param name='scale'>
    /// 	- The size of the point.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the point.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not this point should be faded when behind other objects.
    /// </param>
    public static void Point(Vector3 position, float scale = 1.0f, float duration = 0, bool depthTest = true)
    {
        Point(position, Color.white, scale, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs an axis-aligned bounding box.
    /// </summary>
    /// <param name='bounds'>
    /// 	- The bounds to debug.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the bounds.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the bounds.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the bounds should be faded when behind other objects.
    /// </param>
    public static void Bounds(Bounds bounds, Color color, float duration = 0, bool depthTest = true)
    {
        Vector3 center = bounds.center;

        float x = bounds.extents.x;
        float y = bounds.extents.y;
        float z = bounds.extents.z;

        Vector3 ruf = center + new Vector3(x, y, z);
        Vector3 rub = center + new Vector3(x, y, -z);
        Vector3 luf = center + new Vector3(-x, y, z);
        Vector3 lub = center + new Vector3(-x, y, -z);

        Vector3 rdf = center + new Vector3(x, -y, z);
        Vector3 rdb = center + new Vector3(x, -y, -z);
        Vector3 lfd = center + new Vector3(-x, -y, z);
        Vector3 lbd = center + new Vector3(-x, -y, -z);

        Debug.DrawLine(ruf, luf, color, duration, depthTest);
        Debug.DrawLine(ruf, rub, color, duration, depthTest);
        Debug.DrawLine(luf, lub, color, duration, depthTest);
        Debug.DrawLine(rub, lub, color, duration, depthTest);

        Debug.DrawLine(ruf, rdf, color, duration, depthTest);
        Debug.DrawLine(rub, rdb, color, duration, depthTest);
        Debug.DrawLine(luf, lfd, color, duration, depthTest);
        Debug.DrawLine(lub, lbd, color, duration, depthTest);

        Debug.DrawLine(rdf, lfd, color, duration, depthTest);
        Debug.DrawLine(rdf, rdb, color, duration, depthTest);
        Debug.DrawLine(lfd, lbd, color, duration, depthTest);
        Debug.DrawLine(lbd, rdb, color, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs an axis-aligned bounding box.
    /// </summary>
    /// <param name='bounds'>
    /// 	- The bounds to debug.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the bounds.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the bounds should be faded when behind other objects.
    /// </param>
    public static void Bounds(Bounds bounds, float duration = 0, bool depthTest = true)
    {
        Bounds(bounds, Color.white, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a local cube.
    /// </summary>
    /// <param name='transform'>
    /// 	- The transform that the cube will be local to.
    /// </param>
    /// <param name='size'>
    /// 	- The size of the cube.
    /// </param>
    /// <param name='color'>
    /// 	- Color of the cube.
    /// </param>
    /// <param name='center'>
    /// 	- The position (relative to transform) where the cube will be debugged.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cube.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cube should be faded when behind other objects.
    /// </param>
    public static void LocalCube(Transform transform, Vector3 size, Color color, Vector3 center = default(Vector3), float duration = 0, bool depthTest = true)
    {
        Vector3 lbb = transform.TransformPoint(center + ((-size) * 0.5f));
        Vector3 rbb = transform.TransformPoint(center + (new Vector3(size.x, -size.y, -size.z) * 0.5f));

        Vector3 lbf = transform.TransformPoint(center + (new Vector3(size.x, -size.y, size.z) * 0.5f));
        Vector3 rbf = transform.TransformPoint(center + (new Vector3(-size.x, -size.y, size.z) * 0.5f));

        Vector3 lub = transform.TransformPoint(center + (new Vector3(-size.x, size.y, -size.z) * 0.5f));
        Vector3 rub = transform.TransformPoint(center + (new Vector3(size.x, size.y, -size.z) * 0.5f));

        Vector3 luf = transform.TransformPoint(center + ((size) * 0.5f));
        Vector3 ruf = transform.TransformPoint(center + (new Vector3(-size.x, size.y, size.z) * 0.5f));

        Debug.DrawLine(lbb, rbb, color, duration, depthTest);
        Debug.DrawLine(rbb, lbf, color, duration, depthTest);
        Debug.DrawLine(lbf, rbf, color, duration, depthTest);
        Debug.DrawLine(rbf, lbb, color, duration, depthTest);

        Debug.DrawLine(lub, rub, color, duration, depthTest);
        Debug.DrawLine(rub, luf, color, duration, depthTest);
        Debug.DrawLine(luf, ruf, color, duration, depthTest);
        Debug.DrawLine(ruf, lub, color, duration, depthTest);

        Debug.DrawLine(lbb, lub, color, duration, depthTest);
        Debug.DrawLine(rbb, rub, color, duration, depthTest);
        Debug.DrawLine(lbf, luf, color, duration, depthTest);
        Debug.DrawLine(rbf, ruf, color, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a local cube.
    /// </summary>
    /// <param name='transform'>
    /// 	- The transform that the cube will be local to.
    /// </param>
    /// <param name='size'>
    /// 	- The size of the cube.
    /// </param>
    /// <param name='center'>
    /// 	- The position (relative to transform) where the cube will be debugged.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cube.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cube should be faded when behind other objects.
    /// </param>
    public static void LocalCube(Transform transform, Vector3 size, Vector3 center = default(Vector3), float duration = 0, bool depthTest = true)
    {
        LocalCube(transform, size, Color.white, center, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a local cube.
    /// </summary>
    /// <param name='space'>
    /// 	- The space the cube will be local to.
    /// </param>
    /// <param name='size'>
    ///		- The size of the cube.
    /// </param>
    /// <param name='color'>
    /// 	- Color of the cube.
    /// </param>
    /// <param name='center'>
    /// 	- The position (relative to transform) where the cube will be debugged.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cube.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cube should be faded when behind other objects.
    /// </param>
    public static void LocalCube(Matrix4x4 space, Vector3 size, Color color, Vector3 center = default(Vector3), float duration = 0, bool depthTest = true)
    {
        color = (color == default(Color)) ? Color.white : color;

        Vector3 lbb = space.MultiplyPoint3x4(center + ((-size) * 0.5f));
        Vector3 rbb = space.MultiplyPoint3x4(center + (new Vector3(size.x, -size.y, -size.z) * 0.5f));

        Vector3 lbf = space.MultiplyPoint3x4(center + (new Vector3(size.x, -size.y, size.z) * 0.5f));
        Vector3 rbf = space.MultiplyPoint3x4(center + (new Vector3(-size.x, -size.y, size.z) * 0.5f));

        Vector3 lub = space.MultiplyPoint3x4(center + (new Vector3(-size.x, size.y, -size.z) * 0.5f));
        Vector3 rub = space.MultiplyPoint3x4(center + (new Vector3(size.x, size.y, -size.z) * 0.5f));

        Vector3 luf = space.MultiplyPoint3x4(center + ((size) * 0.5f));
        Vector3 ruf = space.MultiplyPoint3x4(center + (new Vector3(-size.x, size.y, size.z) * 0.5f));

        Debug.DrawLine(lbb, rbb, color, duration, depthTest);
        Debug.DrawLine(rbb, lbf, color, duration, depthTest);
        Debug.DrawLine(lbf, rbf, color, duration, depthTest);
        Debug.DrawLine(rbf, lbb, color, duration, depthTest);

        Debug.DrawLine(lub, rub, color, duration, depthTest);
        Debug.DrawLine(rub, luf, color, duration, depthTest);
        Debug.DrawLine(luf, ruf, color, duration, depthTest);
        Debug.DrawLine(ruf, lub, color, duration, depthTest);

        Debug.DrawLine(lbb, lub, color, duration, depthTest);
        Debug.DrawLine(rbb, rub, color, duration, depthTest);
        Debug.DrawLine(lbf, luf, color, duration, depthTest);
        Debug.DrawLine(rbf, ruf, color, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a local cube.
    /// </summary>
    /// <param name='space'>
    /// 	- The space the cube will be local to.
    /// </param>
    /// <param name='size'>
    ///		- The size of the cube.
    /// </param>
    /// <param name='center'>
    /// 	- The position (relative to transform) where the cube will be debugged.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cube.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cube should be faded when behind other objects.
    /// </param>
    public static void LocalCube(Matrix4x4 space, Vector3 size, Vector3 center = default(Vector3), float duration = 0, bool depthTest = true)
    {
        LocalCube(space, size, Color.white, center, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void NGon(Vector3 position, int sides, Vector3 up, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Vector3 _up = up.normalized * radius;
        Vector3 _forward = Vector3.Slerp(_up, -_up, 0.5f);
        Vector3 _right = Vector3.Cross(_up, _forward).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();

        matrix[0] = _right.x;
        matrix[1] = _right.y;
        matrix[2] = _right.z;

        matrix[4] = _up.x;
        matrix[5] = _up.y;
        matrix[6] = _up.z;

        matrix[8] = _forward.x;
        matrix[9] = _forward.y;
        matrix[10] = _forward.z;

        Vector3 _lastPoint = position + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
        Vector3 _nextPoint = Vector3.zero;

        color = (color == default(Color)) ? Color.white : color;

        sides = Mathf.Clamp(sides, 3, 90);
        float increment = 360.0f / sides;

        for (var i = 0; i <= sides; i++)
        {
            _nextPoint.x = Mathf.Cos((i * increment) * Mathf.Deg2Rad);
            _nextPoint.z = Mathf.Sin((i * increment) * Mathf.Deg2Rad);
            _nextPoint.y = 0;

            _nextPoint = position + matrix.MultiplyPoint3x4(_nextPoint);

            Debug.DrawLine(_lastPoint, _nextPoint, color, duration, depthTest);
            _lastPoint = _nextPoint;
        }
    }
    public static void NGon(Vector3 position, int sides, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        NGon(position, sides, Vector3.up, color, radius, duration, depthTest);
    }
    public static void NGon(Vector3 position, int sides, Vector3 up, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        NGon(position, sides, up, Color.white, radius, duration, depthTest);
    }
    public static void NGon(Vector3 position, int sides, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        NGon(position, sides, Vector3.up, Color.white, radius, duration, depthTest);
    }

    enum CE
    {
        T = 1 << 0, // Top,
        TL = 1 << 1, // TopLeft,
        TR = 1 << 2, // TopRight,
        B = 1 << 3, // Bottom,
        BL = 1 << 4, // BottomLeft,
        BR = 1 << 5, // BottomRight,
        N = 1 << 6, // North,
        NE = 1 << 7, // NorthEast,
        E = 1 << 8, // East,
        SE = 1 << 9, // SouthEast,
        S = 1 << 10, // South,
        SW = 1 << 11, // SouthWest,
        W = 1 << 12, // West,
        NW = 1 << 13, // NorthWest
        DT = 1 << 14, // Dot
        CM = 1 << 15  // Comma
    }

    public class ElementCoords
    {
        public float x0, y0, x1, y1;
        public ElementCoords(float _x0, float _y0, float _x1, float _y1)
        {
            x0 = _x0;
            y0 = _y0;
            x1 = _x1;
            y1 = _y1;
        }
    }

    static Dictionary<CE, ElementCoords> ElementLookup = new Dictionary<CE, ElementCoords>
    {
        {CE.T                  , new ElementCoords(-1.00f,  1.00f,  1.00f,  1.00f) },
        {CE.TL                 , new ElementCoords(-1.00f,  1.00f, -1.00f,  0.00f) },
        {CE.TR                 , new ElementCoords( 1.00f,  1.00f,  1.00f,  0.00f) },
        {CE.B                  , new ElementCoords(-1.00f, -1.00f,  1.00f, -1.00f) },
        {CE.BL                 , new ElementCoords(-1.00f,  0.00f, -1.00f, -1.00f) },
        {CE.BR                 , new ElementCoords( 1.00f,  0.00f,  1.00f, -1.00f) },
        {CE.N                  , new ElementCoords( 0.00f,  1.00f,  0.00f,  0.00f) },
        {CE.NE                 , new ElementCoords( 1.00f,  1.00f,  0.00f,  0.00f) },
        {CE.E                  , new ElementCoords( 1.00f,  0.00f,  0.00f,  0.00f) },
        {CE.SE                 , new ElementCoords( 1.00f, -1.00f,  0.00f,  0.00f) },
        {CE.S                  , new ElementCoords( 0.00f, -1.00f,  0.00f,  0.00f) },
        {CE.SW                 , new ElementCoords(-1.00f, -1.00f,  0.00f,  0.00f) },
        {CE.W                  , new ElementCoords(-1.00f,  0.00f,  0.00f,  0.00f) },
        {CE.NW                 , new ElementCoords(-1.00f,  1.00f,  0.00f,  0.00f) },
        {CE.DT                 , new ElementCoords( 0.40f, -0.90f,  0.60f, -0.90f) },
        {CE.CM                 , new ElementCoords( 0.40f, -1.00f,  0.60f, -0.90f) },
    };

    static Dictionary<char, CE> CharElementLookup = new Dictionary<char, CE>
    {
        { '0' , CE.T | CE.B | CE.TL | CE.TR | CE.BL | CE.BR | CE.NE | CE.SW },
        { '1' , CE.TR | CE.BR },
        { '2' , CE.T | CE.TR | CE.E | CE.W | CE.BL | CE.B },
        { '3' , CE.T | CE.TR | CE.E | CE.W | CE.BR | CE.B },
        { '4' , CE.TL | CE.E | CE.W | CE.TR | CE.BR },
        { '5' , CE.T | CE.TL | CE.E | CE.W | CE.BR | CE.B },
        { '6' , CE.T | CE.TL | CE.BL | CE.B | CE.BR | CE.W | CE.E },
        { '7' , CE.T | CE.NE | CE.SW },
        { '8' , CE.T | CE.TL | CE.TR | CE.E | CE.W | CE.BL | CE.BR | CE.B },
        { '9' , CE.T | CE.TL | CE.TR | CE.E | CE.W | CE.BR | CE.B },

        { 'A' , CE.T | CE.TL | CE.TR | CE.E | CE.W | CE.BL | CE.BR },
        { 'B' , CE.TL | CE.E | CE.W | CE.BL | CE.BR | CE.B },
        { 'C' , CE.T | CE.TL | CE.BL | CE.B },
        { 'D' , CE.TR | CE.E | CE.W | CE.BL | CE.BR | CE.B },
        { 'E' , CE.T | CE.TL | CE.E | CE.W | CE.BL | CE.B },
        { 'F' , CE.T | CE.TL | CE.E | CE.W | CE.BL },
        { 'G' , CE.T | CE.TL | CE.E | CE.BL | CE.BR | CE.B },
        { 'H' , CE.TL | CE.TR | CE.E | CE.W | CE.BL | CE.BR },
        { 'I' , CE.T | CE.B | CE.N | CE.S},
        { 'J' , CE.TR | CE.BL | CE.BR | CE.B },
        { 'K' , CE.TL | CE.W | CE.BL | CE.NE | CE.SE},
        { 'L' , CE.TL | CE.BL | CE.B },
        { 'M' , CE.TL | CE.TR | CE.BL | CE.BR | CE.NE | CE.NW },
        { 'N' , CE.TL | CE.TR | CE.BL | CE.BR | CE.NW | CE.SE },
        { 'O' , CE.T | CE.TL | CE.TR | CE.BL | CE.BR | CE.B },
        { 'P' , CE.T | CE.TL | CE.TR | CE.E | CE.W | CE.BL },
        { 'Q' , CE.T | CE.TL | CE.TR | CE.BL | CE.BR | CE.B | CE.SE },
        { 'R' , CE.T | CE.TL | CE.TR | CE.E | CE.W | CE.BL | CE.SE },
        { 'S' , CE.T | CE.TL | CE.E | CE.W | CE.BR | CE.B },
        { 'T' , CE.T | CE.N | CE.S},
        { 'U' , CE.TL | CE.TR | CE.BL | CE.BR | CE.B },
        { 'V' , CE.TL | CE.BL | CE.NE | CE.SW },
        { 'W' , CE.TL | CE.TR | CE.BL | CE.BR | CE.SE | CE.SW },
        { 'X' , CE.NE | CE.NW | CE.SE | CE.SW },
        { 'Y' , CE.NE | CE.NW | CE.S },
        { 'Z' , CE.T | CE.B | CE.NE | CE.SW },

        { '\\' , CE.NW | CE.SE },
        { '/' , CE.NE | CE.SW },
        { '|' , CE.N | CE.S },
        { '-' , CE.E | CE.W },
        { '=' , CE.E | CE.W | CE.B},
        { '+' , CE.N | CE.S | CE.E | CE.W },
        { '<' , CE.NE | CE.SE },
        { '>' , CE.NW | CE.SW },
        { '$' , CE.T | CE.TL | CE.E | CE.W | CE.BR | CE.B | CE.N | CE.S },
        { '_' , CE.B },
        { '.' , CE.DT },
        { ',' , CE.DT | CE.CM },
    };

    static void DrawCharacter(CE ce, Vector3 position, Vector3 up, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Vector3 _up = -up.normalized * radius;
        Vector3 _forward = Vector3.Slerp(_up, -_up, 0.5f);
        Vector3 _right = Vector3.Cross(_forward, _up).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();

        matrix[0] = _right.x;
        matrix[1] = _right.y;
        matrix[2] = _right.z;

        matrix[4] = _up.x;
        matrix[5] = _up.y;
        matrix[6] = _up.z;

        matrix[8] = _forward.x;
        matrix[9] = _forward.y;
        matrix[10] = _forward.z;

        foreach (CE c in System.Enum.GetValues(typeof(CE)))
        {
            if ((ce & c) != 0)
            {
                ElementCoords ec = ElementLookup[c];
                Vector3 _p0 = position + matrix.MultiplyPoint3x4(-new Vector3(ec.x0 * radius * 0.5f, 0, ec.y0 * radius));
                Vector3 _p1 = position + matrix.MultiplyPoint3x4(-new Vector3(ec.x1 * radius * 0.5f, 0, ec.y1 * radius));
                Debug.DrawLine(_p0, _p1, color, duration, depthTest);
            }
        }
    }

    public static void String(string message, Vector3 position, Vector3 up, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Vector3 _up = -up.normalized * radius;
        Vector3 _forward = Vector3.Slerp(_up, -_up, 0.5f);
        Vector3 _right = Vector3.Cross(_forward, _up).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();

        matrix[0] = _right.x;
        matrix[1] = _right.y;
        matrix[2] = _right.z;

        matrix[4] = _up.x;
        matrix[5] = _up.y;
        matrix[6] = _up.z;

        matrix[8] = _forward.x;
        matrix[9] = _forward.y;
        matrix[10] = _forward.z;

        Vector3 offsetIncrement_X = matrix.MultiplyPoint3x4(Vector3.left * radius * 1.25f);
        Vector3 offsetIncrement_Y = matrix.MultiplyPoint3x4(Vector3.forward * radius * 2.25f);
        Vector3 offset = Vector3.zero;
        int column = 0;
        int row = 0;
        foreach (char c in message)
        {
            offset = offsetIncrement_X * column + offsetIncrement_Y * row;
            if (c == '\n')
            {
                row++;
                column = 0;
            }
            else if (c == '\t')
                column += 4;
            else
            {
                if (CharElementLookup.ContainsKey(c))
                    DrawCharacter(CharElementLookup[c], position + offset, up, color, radius, duration, depthTest);
                else if (CharElementLookup.ContainsKey(char.ToUpper(c)))
                    DrawCharacter(CharElementLookup[char.ToUpper(c)], position + offset, up, color, radius, duration, depthTest);
                column++;
            }
        }
    }

    public static void String(string message, Vector3 position, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        String(message, position, Vector3.up, color, radius, duration, depthTest);
    }

    public static void String(string message, Vector3 position, Vector3 up, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        String(message, position, up, Color.white, radius, duration, depthTest);
    }

    public static void String(string message, Vector3 position, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        String(message, position, Vector3.up, Color.white, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Circle(Vector3 position, Vector3 up, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Vector3 _up = up.normalized * radius;
        Vector3 _forward = Vector3.Slerp(_up, -_up, 0.5f);
        Vector3 _right = Vector3.Cross(_up, _forward).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();

        matrix[0] = _right.x;
        matrix[1] = _right.y;
        matrix[2] = _right.z;

        matrix[4] = _up.x;
        matrix[5] = _up.y;
        matrix[6] = _up.z;

        matrix[8] = _forward.x;
        matrix[9] = _forward.y;
        matrix[10] = _forward.z;

        Vector3 _lastPoint = position + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
        Vector3 _nextPoint = Vector3.zero;

        color = (color == default(Color)) ? Color.white : color;

        for (var i = 0; i < 91; i++)
        {
            _nextPoint.x = Mathf.Cos((i * 4) * Mathf.Deg2Rad);
            _nextPoint.z = Mathf.Sin((i * 4) * Mathf.Deg2Rad);
            _nextPoint.y = 0;

            _nextPoint = position + matrix.MultiplyPoint3x4(_nextPoint);

            Debug.DrawLine(_lastPoint, _nextPoint, color, duration, depthTest);
            _lastPoint = _nextPoint;
        }
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Circle(Vector3 position, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Circle(position, Vector3.up, color, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Circle(Vector3 position, Vector3 up, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Circle(position, up, Color.white, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Circle(Vector3 position, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Circle(position, Vector3.up, Color.white, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Arc(Vector3 position, Vector3 up, Vector3 forward, float angle, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Vector3 _up = up.normalized * radius;
        Vector3 _forward = forward.normalized * radius;

        Vector3 _lastPoint = position + _forward;
        Vector3 _nextPoint = Vector3.zero;

        color = (color == default(Color)) ? Color.white : color;

        float sweep = Mathf.Lerp(0, 90, Mathf.InverseLerp(0, 360, Mathf.Abs(angle)));
        float incr = angle / sweep;

        for (var i = 0; i < sweep; i++)
        {
            _nextPoint = position + Quaternion.AngleAxis(i * incr, _up) * _forward;

            Debug.DrawLine(_lastPoint, _nextPoint, color.FadeAlpha(Mathf.Lerp(0.25f, 1, Mathf.InverseLerp(0, sweep - 1, i))), duration, depthTest);

            _lastPoint = _nextPoint;
        }

        Vector3 radial = Quaternion.AngleAxis(angle, _up) * _forward;
        Debug.DrawLine(position + radial * 0.5f, position + radial * 1.5f, color, duration, depthTest);

        _nextPoint = position + radial;
        Debug.DrawLine(_lastPoint, _nextPoint, color, duration, depthTest);

        Vector3 direction = Vector3.Cross(up, Quaternion.AngleAxis(angle, up) * forward.normalized) * (angle >= 0 ? -1 : 1);

        Cone(_nextPoint, direction.normalized * radius * 0.1f, color, 30, duration, depthTest);
        String(string.Format("{1}{0:0.000}", angle, angle < 0 ? " " : "  "), position + radial * 1.5f, color, radius * 0.5f);

    }

    public static void Arc(Vector3 position, Vector3 forward, Quaternion rotation, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        float angle;
        Vector3 up;

        rotation.ToAngleAxis(out angle, out up);

        Arc(position, up, forward, angle, color, radius, duration, depthTest);
    }


    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Arc(Vector3 position, Vector3 forward, float angle, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Arc(position, Vector3.up, forward, angle, color, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Arc(Vector3 position, Vector3 up, Vector3 forward, float angle, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Arc(position, up, forward, angle, Color.white, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the circle.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the circle should be faded when behind other objects.
    /// </param>
    public static void Arc(Vector3 position, float angle, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        Arc(position, Vector3.up, Vector3.forward, angle, Color.white, radius, duration, depthTest);
    }



    /// <summary>
    /// 	- Debugs a wire sphere.
    /// </summary>
    /// <param name='position'>
    /// 	- The position of the center of the sphere.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the sphere.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the sphere.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the sphere.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the sphere should be faded when behind other objects.
    /// </param>
    public static void WireSphere(Vector3 position, Color color, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        float angle = 10.0f;

        Vector3 x = new Vector3(position.x, position.y + radius * Mathf.Sin(0), position.z + radius * Mathf.Cos(0));
        Vector3 y = new Vector3(position.x + radius * Mathf.Cos(0), position.y, position.z + radius * Mathf.Sin(0));
        Vector3 z = new Vector3(position.x + radius * Mathf.Cos(0), position.y + radius * Mathf.Sin(0), position.z);

        Vector3 new_x;
        Vector3 new_y;
        Vector3 new_z;

        for (int i = 1; i < 37; i++)
        {

            new_x = new Vector3(position.x, position.y + radius * Mathf.Sin(angle * i * Mathf.Deg2Rad), position.z + radius * Mathf.Cos(angle * i * Mathf.Deg2Rad));
            new_y = new Vector3(position.x + radius * Mathf.Cos(angle * i * Mathf.Deg2Rad), position.y, position.z + radius * Mathf.Sin(angle * i * Mathf.Deg2Rad));
            new_z = new Vector3(position.x + radius * Mathf.Cos(angle * i * Mathf.Deg2Rad), position.y + radius * Mathf.Sin(angle * i * Mathf.Deg2Rad), position.z);

            Debug.DrawLine(x, new_x, color, duration, depthTest);
            Debug.DrawLine(y, new_y, color, duration, depthTest);
            Debug.DrawLine(z, new_z, color, duration, depthTest);

            x = new_x;
            y = new_y;
            z = new_z;
        }
    }

    /// <summary>
    /// 	- Debugs a wire sphere.
    /// </summary>
    /// <param name='position'>
    /// 	- The position of the center of the sphere.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the sphere.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the sphere.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the sphere should be faded when behind other objects.
    /// </param>
    public static void WireSphere(Vector3 position, float radius = 1.0f, float duration = 0, bool depthTest = true)
    {
        WireSphere(position, Color.white, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a cylinder.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the cylinder.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the cylinder.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cylinder.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the cylinder.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cylinder.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cylinder should be faded when behind other objects.
    /// </param>
    public static void Cylinder(Vector3 start, Vector3 end, Color color, float radius = 1, float duration = 0, bool depthTest = true)
    {
        Vector3 up = (end - start).normalized * radius;
        Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        //Radial circles
        DebugDraw.Circle(start, up, color, radius, duration, depthTest);
        DebugDraw.Circle(end, -up, color, radius, duration, depthTest);
        DebugDraw.Circle((start + end) * 0.5f, up, color, radius, duration, depthTest);

        //Side lines
        Debug.DrawLine(start + right, end + right, color, duration, depthTest);
        Debug.DrawLine(start - right, end - right, color, duration, depthTest);

        Debug.DrawLine(start + forward, end + forward, color, duration, depthTest);
        Debug.DrawLine(start - forward, end - forward, color, duration, depthTest);

        //Start endcap
        Debug.DrawLine(start - right, start + right, color, duration, depthTest);
        Debug.DrawLine(start - forward, start + forward, color, duration, depthTest);

        //End endcap
        Debug.DrawLine(end - right, end + right, color, duration, depthTest);
        Debug.DrawLine(end - forward, end + forward, color, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a cylinder.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the cylinder.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the cylinder.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the cylinder.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cylinder.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cylinder should be faded when behind other objects.
    /// </param>
    public static void Cylinder(Vector3 start, Vector3 end, float radius = 1, float duration = 0, bool depthTest = true)
    {
        Cylinder(start, end, Color.white, radius, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction for the cone gets wider in.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cone.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cone.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cone should be faded when behind other objects.
    /// </param>
    public static void Cone(Vector3 position, Vector3 direction, Color color, float angle = 45, float duration = 0, bool depthTest = true)
    {
        float length = direction.magnitude;

        Vector3 _forward = direction;
        Vector3 _up = Vector3.Slerp(_forward, -_forward, 0.5f);
        Vector3 _right = Vector3.Cross(_forward, _up).normalized * length;

        direction = direction.normalized;

        Vector3 slerpedVector = Vector3.Slerp(_forward, _up, angle / 90.0f);

        float dist;
        var farPlane = new Plane(-direction, position + _forward);
        var distRay = new Ray(position, slerpedVector);

        farPlane.Raycast(distRay, out dist);

        Debug.DrawRay(position, slerpedVector.normalized * dist, color);
        Debug.DrawRay(position, Vector3.Slerp(_forward, -_up, angle / 90.0f).normalized * dist, color, duration, depthTest);
        Debug.DrawRay(position, Vector3.Slerp(_forward, _right, angle / 90.0f).normalized * dist, color, duration, depthTest);
        Debug.DrawRay(position, Vector3.Slerp(_forward, -_right, angle / 90.0f).normalized * dist, color, duration, depthTest);

        Circle(position + _forward, direction, color, (_forward - (slerpedVector.normalized * dist)).magnitude, duration, depthTest);
        Circle(position + (_forward * 0.5f), direction, color, ((_forward * 0.5f) - (slerpedVector.normalized * (dist * 0.5f))).magnitude, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction for the cone gets wider in.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cone.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cone should be faded when behind other objects.
    /// </param>
    public static void Cone(Vector3 position, Vector3 direction, float angle = 45, float duration = 0, bool depthTest = true)
    {
        Cone(position, direction, Color.white, angle, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cone.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cone.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cone should be faded when behind other objects.
    /// </param>
    public static void Cone(Vector3 position, Color color, float angle = 45, float duration = 0, bool depthTest = true)
    {
        Cone(position, Vector3.up, color, angle, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the cone.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the cone should be faded when behind other objects.
    /// </param>
    public static void Cone(Vector3 position, float angle = 45, float duration = 0, bool depthTest = true)
    {
        Cone(position, Vector3.up, Color.white, angle, duration, depthTest);
    }

    public static void Sweep(Vector3 position, Vector3 startDir, Vector3 endDir, int subdivisions, Color color)
    {
        for (int i = 0; i <= subdivisions; i++)
        {
            float t = Mathf.InverseLerp(0, subdivisions, i);
            Debug.DrawRay(position, Vector3.Slerp(startDir, endDir, t), color);
        }
    }

    /// <summary>
    /// 	- Debugs an arrow.
    /// </summary>
    /// <param name='position'>
    /// 	- The start position of the arrow.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction the arrow will point in.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the arrow.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the arrow.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the arrow should be faded when behind other objects.
    /// </param>
    public static void Arrow(Vector3 position, Vector3 direction, Color color, float duration = 0, bool depthTest = true)
    {
        Debug.DrawRay(position, direction, color, duration, depthTest);
        Cone(position + direction, -direction * 0.333f, color, 15, duration, depthTest);
    }

    public static void Arrow(Ray ray, Color color, float duration = 0, bool depthTest = true)
    {
        Arrow(ray.origin, ray.direction, color, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs an arrow.
    /// </summary>
    /// <param name='position'>
    /// 	- The start position of the arrow.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction the arrow will point in.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the arrow.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the arrow should be faded when behind other objects.
    /// </param>
    public static void Arrow(Vector3 position, Vector3 direction, float duration = 0, bool depthTest = true)
    {
        Arrow(position, direction, Color.white, duration, depthTest);
    }

    public static void Arrow(Ray ray, float duration = 0, bool depthTest = true)
    {
        Arrow(ray.origin, ray.direction, duration, depthTest);
    }

    /// <summary>
    /// 	- Debugs a capsule.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the capsule.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the capsule.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the capsule.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the capsule.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the capsule.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the capsule should be faded when behind other objects.
    /// </param>
    public static void Capsule(Vector3 start, Vector3 end, Color color, float radius = 1, float duration = 0, bool depthTest = true)
    {
        Vector3 up = (end - start).normalized * radius;
        Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        float height = (start - end).magnitude;
        float sideLength = Mathf.Max(0, (height * 0.5f) - radius);
        Vector3 middle = (end + start) * 0.5f;

        start = middle + ((start - middle).normalized * sideLength);
        end = middle + ((end - middle).normalized * sideLength);

        //Radial circles
        DebugDraw.Circle(start, up, color, radius, duration, depthTest);
        DebugDraw.Circle(end, -up, color, radius, duration, depthTest);

        //Side lines
        Debug.DrawLine(start + right, end + right, color, duration, depthTest);
        Debug.DrawLine(start - right, end - right, color, duration, depthTest);

        Debug.DrawLine(start + forward, end + forward, color, duration, depthTest);
        Debug.DrawLine(start - forward, end - forward, color, duration, depthTest);

        for (int i = 1; i < 26; i++)
        {

            //Start endcap
            Debug.DrawLine(Vector3.Slerp(right, -up, i / 25.0f) + start, Vector3.Slerp(right, -up, (i - 1) / 25.0f) + start, color, duration, depthTest);
            Debug.DrawLine(Vector3.Slerp(-right, -up, i / 25.0f) + start, Vector3.Slerp(-right, -up, (i - 1) / 25.0f) + start, color, duration, depthTest);
            Debug.DrawLine(Vector3.Slerp(forward, -up, i / 25.0f) + start, Vector3.Slerp(forward, -up, (i - 1) / 25.0f) + start, color, duration, depthTest);
            Debug.DrawLine(Vector3.Slerp(-forward, -up, i / 25.0f) + start, Vector3.Slerp(-forward, -up, (i - 1) / 25.0f) + start, color, duration, depthTest);

            //End endcap
            Debug.DrawLine(Vector3.Slerp(right, up, i / 25.0f) + end, Vector3.Slerp(right, up, (i - 1) / 25.0f) + end, color, duration, depthTest);
            Debug.DrawLine(Vector3.Slerp(-right, up, i / 25.0f) + end, Vector3.Slerp(-right, up, (i - 1) / 25.0f) + end, color, duration, depthTest);
            Debug.DrawLine(Vector3.Slerp(forward, up, i / 25.0f) + end, Vector3.Slerp(forward, up, (i - 1) / 25.0f) + end, color, duration, depthTest);
            Debug.DrawLine(Vector3.Slerp(-forward, up, i / 25.0f) + end, Vector3.Slerp(-forward, up, (i - 1) / 25.0f) + end, color, duration, depthTest);
        }
    }

    /// <summary>
    /// 	- Debugs a capsule.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the capsule.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the capsule.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the capsule.
    /// </param>
    /// <param name='duration'>
    /// 	- How long to draw the capsule.
    /// </param>
    /// <param name='depthTest'>
    /// 	- Whether or not the capsule should be faded when behind other objects.
    /// </param>
    public static void Capsule(Vector3 start, Vector3 end, float radius = 1, float duration = 0, bool depthTest = true)
    {
        Capsule(start, end, Color.white, radius, duration, depthTest);
    }

    #endregion

    #region GizmoDrawFunctions

    /// <summary>
    /// 	- Draws a point.
    /// </summary>
    /// <param name='position'>
    /// 	- The point to draw.
    /// </param>
    ///  <param name='color'>
    /// 	- The color of the drawn point.
    /// </param>
    /// <param name='scale'>
    /// 	- The size of the drawn point.
    /// </param>
    public static void GPoint(Vector3 position, Color color, float scale = 1.0f)
    {
        Color oldColor = Gizmos.color;

        Gizmos.color = color;
        Gizmos.DrawRay(position + (Vector3.up * (scale * 0.5f)), -Vector3.up * scale);
        Gizmos.DrawRay(position + (Vector3.right * (scale * 0.5f)), -Vector3.right * scale);
        Gizmos.DrawRay(position + (Vector3.forward * (scale * 0.5f)), -Vector3.forward * scale);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a point.
    /// </summary>
    /// <param name='position'>
    /// 	- The point to draw.
    /// </param>
    /// <param name='scale'>
    /// 	- The size of the drawn point.
    /// </param>
    public static void GPoint(Vector3 position, float scale = 1.0f)
    {
        GPoint(position, Color.white, scale);
    }

    /// <summary>
    /// 	- Draws an axis-aligned bounding box.
    /// </summary>
    /// <param name='bounds'>
    /// 	- The bounds to draw.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the bounds.
    /// </param>
    public static void GBounds(Bounds bounds, Color color)
    {
        Vector3 center = bounds.center;

        float x = bounds.extents.x;
        float y = bounds.extents.y;
        float z = bounds.extents.z;

        Vector3 ruf = center + new Vector3(x, y, z);
        Vector3 rub = center + new Vector3(x, y, -z);
        Vector3 luf = center + new Vector3(-x, y, z);
        Vector3 lub = center + new Vector3(-x, y, -z);

        Vector3 rdf = center + new Vector3(x, -y, z);
        Vector3 rdb = center + new Vector3(x, -y, -z);
        Vector3 lfd = center + new Vector3(-x, -y, z);
        Vector3 lbd = center + new Vector3(-x, -y, -z);

        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawLine(ruf, luf);
        Gizmos.DrawLine(ruf, rub);
        Gizmos.DrawLine(luf, lub);
        Gizmos.DrawLine(rub, lub);

        Gizmos.DrawLine(ruf, rdf);
        Gizmos.DrawLine(rub, rdb);
        Gizmos.DrawLine(luf, lfd);
        Gizmos.DrawLine(lub, lbd);

        Gizmos.DrawLine(rdf, lfd);
        Gizmos.DrawLine(rdf, rdb);
        Gizmos.DrawLine(lfd, lbd);
        Gizmos.DrawLine(lbd, rdb);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws an axis-aligned bounding box.
    /// </summary>
    /// <param name='bounds'>
    /// 	- The bounds to draw.
    /// </param>
    public static void GBounds(Bounds bounds)
    {
        GBounds(bounds, Color.white);
    }

    /// <summary>
    /// 	- Draws a local cube.
    /// </summary>
    /// <param name='transform'>
    /// 	- The transform the cube will be local to.
    /// </param>
    /// <param name='size'>
    /// 	- The local size of the cube.
    /// </param>
    /// <param name='center'>
    ///		- The local position of the cube.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cube.
    /// </param>
    public static void GLocalCube(Transform transform, Vector3 size, Color color, Vector3 center = default(Vector3))
    {
        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        Vector3 lbb = transform.TransformPoint(center + ((-size) * 0.5f));
        Vector3 rbb = transform.TransformPoint(center + (new Vector3(size.x, -size.y, -size.z) * 0.5f));

        Vector3 lbf = transform.TransformPoint(center + (new Vector3(size.x, -size.y, size.z) * 0.5f));
        Vector3 rbf = transform.TransformPoint(center + (new Vector3(-size.x, -size.y, size.z) * 0.5f));

        Vector3 lub = transform.TransformPoint(center + (new Vector3(-size.x, size.y, -size.z) * 0.5f));
        Vector3 rub = transform.TransformPoint(center + (new Vector3(size.x, size.y, -size.z) * 0.5f));

        Vector3 luf = transform.TransformPoint(center + ((size) * 0.5f));
        Vector3 ruf = transform.TransformPoint(center + (new Vector3(-size.x, size.y, size.z) * 0.5f));

        Gizmos.DrawLine(lbb, rbb);
        Gizmos.DrawLine(rbb, lbf);
        Gizmos.DrawLine(lbf, rbf);
        Gizmos.DrawLine(rbf, lbb);

        Gizmos.DrawLine(lub, rub);
        Gizmos.DrawLine(rub, luf);
        Gizmos.DrawLine(luf, ruf);
        Gizmos.DrawLine(ruf, lub);

        Gizmos.DrawLine(lbb, lub);
        Gizmos.DrawLine(rbb, rub);
        Gizmos.DrawLine(lbf, luf);
        Gizmos.DrawLine(rbf, ruf);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a local cube.
    /// </summary>
    /// <param name='transform'>
    /// 	- The transform the cube will be local to.
    /// </param>
    /// <param name='size'>
    /// 	- The local size of the cube.
    /// </param>
    /// <param name='center'>
    ///		- The local position of the cube.
    /// </param>
    public static void GLocalCube(Transform transform, Vector3 size, Vector3 center = default(Vector3))
    {
        GLocalCube(transform, size, Color.white, center);
    }

    /// <summary>
    /// 	- Draws a local cube.
    /// </summary>
    /// <param name='space'>
    /// 	- The space the cube will be local to.
    /// </param>
    /// <param name='size'>
    /// 	- The local size of the cube.
    /// </param>
    /// <param name='center'>
    /// 	- The local position of the cube.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cube.
    /// </param>
    public static void GLocalCube(Matrix4x4 space, Vector3 size, Color color, Vector3 center = default(Vector3))
    {
        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        Vector3 lbb = space.MultiplyPoint3x4(center + ((-size) * 0.5f));
        Vector3 rbb = space.MultiplyPoint3x4(center + (new Vector3(size.x, -size.y, -size.z) * 0.5f));

        Vector3 lbf = space.MultiplyPoint3x4(center + (new Vector3(size.x, -size.y, size.z) * 0.5f));
        Vector3 rbf = space.MultiplyPoint3x4(center + (new Vector3(-size.x, -size.y, size.z) * 0.5f));

        Vector3 lub = space.MultiplyPoint3x4(center + (new Vector3(-size.x, size.y, -size.z) * 0.5f));
        Vector3 rub = space.MultiplyPoint3x4(center + (new Vector3(size.x, size.y, -size.z) * 0.5f));

        Vector3 luf = space.MultiplyPoint3x4(center + ((size) * 0.5f));
        Vector3 ruf = space.MultiplyPoint3x4(center + (new Vector3(-size.x, size.y, size.z) * 0.5f));

        Gizmos.DrawLine(lbb, rbb);
        Gizmos.DrawLine(rbb, lbf);
        Gizmos.DrawLine(lbf, rbf);
        Gizmos.DrawLine(rbf, lbb);

        Gizmos.DrawLine(lub, rub);
        Gizmos.DrawLine(rub, luf);
        Gizmos.DrawLine(luf, ruf);
        Gizmos.DrawLine(ruf, lub);

        Gizmos.DrawLine(lbb, lub);
        Gizmos.DrawLine(rbb, rub);
        Gizmos.DrawLine(lbf, luf);
        Gizmos.DrawLine(rbf, ruf);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a local cube.
    /// </summary>
    /// <param name='space'>
    /// 	- The space the cube will be local to.
    /// </param>
    /// <param name='size'>
    /// 	- The local size of the cube.
    /// </param>
    /// <param name='center'>
    /// 	- The local position of the cube.
    /// </param>
    public static void GLocalCube(Matrix4x4 space, Vector3 size, Vector3 center = default(Vector3))
    {
        GLocalCube(space, size, Color.white, center);
    }

    /// <summary>
    /// 	- Draws a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    public static void GCircle(Vector3 position, Vector3 up, Color color, float radius = 1.0f)
    {
        up = ((up == Vector3.zero) ? Vector3.up : up).normalized * radius;
        Vector3 _forward = Vector3.Slerp(up, -up, 0.5f);
        Vector3 _right = Vector3.Cross(up, _forward).normalized * radius;

        Matrix4x4 matrix = new Matrix4x4();

        matrix[0] = _right.x;
        matrix[1] = _right.y;
        matrix[2] = _right.z;

        matrix[4] = up.x;
        matrix[5] = up.y;
        matrix[6] = up.z;

        matrix[8] = _forward.x;
        matrix[9] = _forward.y;
        matrix[10] = _forward.z;

        Vector3 _lastPoint = position + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
        Vector3 _nextPoint = Vector3.zero;

        Color oldColor = Gizmos.color;
        Gizmos.color = (color == default(Color)) ? Color.white : color;

        for (var i = 0; i < 91; i++)
        {
            _nextPoint.x = Mathf.Cos((i * 4) * Mathf.Deg2Rad);
            _nextPoint.z = Mathf.Sin((i * 4) * Mathf.Deg2Rad);
            _nextPoint.y = 0;

            _nextPoint = position + matrix.MultiplyPoint3x4(_nextPoint);

            Gizmos.DrawLine(_lastPoint, _nextPoint);
            _lastPoint = _nextPoint;
        }

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    public static void GCircle(Vector3 position, Color color, float radius = 1.0f)
    {
        GCircle(position, Vector3.up, color, radius);
    }

    /// <summary>
    /// 	- Draws a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='up'>
    /// 	- The direction perpendicular to the surface of the circle.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    public static void GCircle(Vector3 position, Vector3 up, float radius = 1.0f)
    {
        GCircle(position, position, Color.white, radius);
    }

    /// <summary>
    /// 	- Draws a circle.
    /// </summary>
    /// <param name='position'>
    /// 	- Where the center of the circle will be positioned.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the circle.
    /// </param>
    public static void GCircle(Vector3 position, float radius = 1.0f)
    {
        GCircle(position, Vector3.up, Color.white, radius);
    }

    //Wiresphere already exists

    /// <summary>
    /// 	- Draws a cylinder.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the cylinder.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the cylinder.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cylinder.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the cylinder.
    /// </param>
    public static void GCylinder(Vector3 start, Vector3 end, Color color, float radius = 1.0f)
    {
        Vector3 up = (end - start).normalized * radius;
        Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        //Radial circles
        GCircle(start, up, color, radius);
        GCircle(end, -up, color, radius);
        GCircle((start + end) * 0.5f, up, color, radius);

        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        //Side lines
        Gizmos.DrawLine(start + right, end + right);
        Gizmos.DrawLine(start - right, end - right);

        Gizmos.DrawLine(start + forward, end + forward);
        Gizmos.DrawLine(start - forward, end - forward);

        //Start endcap
        Gizmos.DrawLine(start - right, start + right);
        Gizmos.DrawLine(start - forward, start + forward);

        //End endcap
        Gizmos.DrawLine(end - right, end + right);
        Gizmos.DrawLine(end - forward, end + forward);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a cylinder.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the cylinder.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the cylinder.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the cylinder.
    /// </param>
    public static void GCylinder(Vector3 start, Vector3 end, float radius = 1.0f)
    {
        GCylinder(start, end, Color.white, radius);
    }

    /// <summary>
    /// 	- Draws a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction for the cone to get wider in.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cone.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    public static void GCone(Vector3 position, Vector3 direction, Color color, float angle = 45)
    {
        float length = direction.magnitude;

        Vector3 _forward = direction;
        Vector3 _up = Vector3.Slerp(_forward, -_forward, 0.5f);
        Vector3 _right = Vector3.Cross(_forward, _up).normalized * length;

        direction = direction.normalized;

        Vector3 slerpedVector = Vector3.Slerp(_forward, _up, angle / 90.0f);

        float dist;
        var farPlane = new Plane(-direction, position + _forward);
        var distRay = new Ray(position, slerpedVector);

        farPlane.Raycast(distRay, out dist);

        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawRay(position, slerpedVector.normalized * dist);
        Gizmos.DrawRay(position, Vector3.Slerp(_forward, -_up, angle / 90.0f).normalized * dist);
        Gizmos.DrawRay(position, Vector3.Slerp(_forward, _right, angle / 90.0f).normalized * dist);
        Gizmos.DrawRay(position, Vector3.Slerp(_forward, -_right, angle / 90.0f).normalized * dist);

        GCircle(position + _forward, direction, color, (_forward - (slerpedVector.normalized * dist)).magnitude);
        GCircle(position + (_forward * 0.5f), direction, color, ((_forward * 0.5f) - (slerpedVector.normalized * (dist * 0.5f))).magnitude);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction for the cone to get wider in.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    public static void GCone(Vector3 position, Vector3 direction, float angle = 45)
    {
        GCone(position, direction, Color.white, angle);
    }

    /// <summary>
    /// 	- Draws a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the cone.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    public static void GCone(Vector3 position, Color color, float angle = 45)
    {
        GCone(position, Vector3.up, color, angle);
    }

    /// <summary>
    /// 	- Draws a cone.
    /// </summary>
    /// <param name='position'>
    /// 	- The position for the tip of the cone.
    /// </param>
    /// <param name='angle'>
    /// 	- The angle of the cone.
    /// </param>
    public static void GCone(Vector3 position, float angle = 45)
    {
        GCone(position, Vector3.up, Color.white, angle);
    }

    /// <summary>
    /// 	- Draws an arrow.
    /// </summary>
    /// <param name='position'>
    /// 	- The start position of the arrow.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction the arrow will point in.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the arrow.
    /// </param>
    public static void GArrow(Vector3 position, Vector3 direction, Color color)
    {
        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawRay(position, direction);
        GCone(position + direction, -direction * 0.333f, color, 15);

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws an arrow.
    /// </summary>
    /// <param name='position'>
    /// 	- The start position of the arrow.
    /// </param>
    /// <param name='direction'>
    /// 	- The direction the arrow will point in.
    /// </param>
    public static void GArrow(Vector3 position, Vector3 direction)
    {
        GArrow(position, direction, Color.white);
    }

    /// <summary>
    /// 	- Draws a capsule.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the capsule.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the capsule.
    /// </param>
    /// <param name='color'>
    /// 	- The color of the capsule.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the capsule.
    /// </param>
    public static void GCapsule(Vector3 start, Vector3 end, Color color, float radius = 1)
    {
        Vector3 up = (end - start).normalized * radius;
        Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        Color oldColor = Gizmos.color;
        Gizmos.color = color;

        float height = (start - end).magnitude;
        float sideLength = Mathf.Max(0, (height * 0.5f) - radius);
        Vector3 middle = (end + start) * 0.5f;

        start = middle + ((start - middle).normalized * sideLength);
        end = middle + ((end - middle).normalized * sideLength);

        //Radial circles
        GCircle(start, up, color, radius);
        GCircle(end, -up, color, radius);

        //Side lines
        Gizmos.DrawLine(start + right, end + right);
        Gizmos.DrawLine(start - right, end - right);

        Gizmos.DrawLine(start + forward, end + forward);
        Gizmos.DrawLine(start - forward, end - forward);

        for (int i = 1; i < 26; i++)
        {

            //Start endcap
            Gizmos.DrawLine(Vector3.Slerp(right, -up, i / 25.0f) + start, Vector3.Slerp(right, -up, (i - 1) / 25.0f) + start);
            Gizmos.DrawLine(Vector3.Slerp(-right, -up, i / 25.0f) + start, Vector3.Slerp(-right, -up, (i - 1) / 25.0f) + start);
            Gizmos.DrawLine(Vector3.Slerp(forward, -up, i / 25.0f) + start, Vector3.Slerp(forward, -up, (i - 1) / 25.0f) + start);
            Gizmos.DrawLine(Vector3.Slerp(-forward, -up, i / 25.0f) + start, Vector3.Slerp(-forward, -up, (i - 1) / 25.0f) + start);

            //End endcap
            Gizmos.DrawLine(Vector3.Slerp(right, up, i / 25.0f) + end, Vector3.Slerp(right, up, (i - 1) / 25.0f) + end);
            Gizmos.DrawLine(Vector3.Slerp(-right, up, i / 25.0f) + end, Vector3.Slerp(-right, up, (i - 1) / 25.0f) + end);
            Gizmos.DrawLine(Vector3.Slerp(forward, up, i / 25.0f) + end, Vector3.Slerp(forward, up, (i - 1) / 25.0f) + end);
            Gizmos.DrawLine(Vector3.Slerp(-forward, up, i / 25.0f) + end, Vector3.Slerp(-forward, up, (i - 1) / 25.0f) + end);
        }

        Gizmos.color = oldColor;
    }

    /// <summary>
    /// 	- Draws a capsule.
    /// </summary>
    /// <param name='start'>
    /// 	- The position of one end of the capsule.
    /// </param>
    /// <param name='end'>
    /// 	- The position of the other end of the capsule.
    /// </param>
    /// <param name='radius'>
    /// 	- The radius of the capsule.
    /// </param>
    public static void GCapsule(Vector3 start, Vector3 end, float radius = 1)
    {
        GCapsule(start, end, Color.white, radius);
    }

    #endregion

    #region DebugFunctions

    /// <summary>
    /// 	- Gets the methods of an object.
    /// </summary>
    /// <returns>
    /// 	- A list of methods accessible from this object.
    /// </returns>
    /// <param name='obj'>
    /// 	- The object to get the methods of.
    /// </param>
    /// <param name='includeInfo'>
    /// 	- Whether or not to include each method's method info in the list.
    /// </param>
    public static string MethodsOfObject(System.Object obj, bool includeInfo = false)
    {
        string methods = "";
        MethodInfo[] methodInfos = obj.GetType().GetMethods();
        for (int i = 0; i < methodInfos.Length; i++)
        {
            if (includeInfo)
            {
                methods += methodInfos[i] + "\n";
            }

            else
            {
                methods += methodInfos[i].Name + "\n";
            }
        }

        return (methods);
    }

    /// <summary>
    /// 	- Gets the methods of a type.
    /// </summary>
    /// <returns>
    /// 	- A list of methods accessible from this type.
    /// </returns>
    /// <param name='type'>
    /// 	- The type to get the methods of.
    /// </param>
    /// <param name='includeInfo'>
    /// 	- Whether or not to include each method's method info in the list.
    /// </param>
    public static string MethodsOfType(System.Type type, bool includeInfo = false)
    {
        string methods = "";
        MethodInfo[] methodInfos = type.GetMethods();
        for (var i = 0; i < methodInfos.Length; i++)
        {
            if (includeInfo)
            {
                methods += methodInfos[i] + "\n";
            }

            else
            {
                methods += methodInfos[i].Name + "\n";
            }
        }

        return (methods);
    }

    #endregion
}
