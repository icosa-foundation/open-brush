using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using System.Globalization;
using System;
using System.Text;
using System.Linq;
using System.Reflection;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class Utils
{
    private readonly static Vector3[] m_trianglePoints;
    private readonly static Vector3[] m_circlePoints;

    private readonly static RaycastHit[] m_nonAllocClampRaycastResults = new RaycastHit[5];

    private const int CIRCLE_POINT_COUNT = 40;

    private static int m_defaultLayer = -1;

    static Utils()
    {
        m_rainbowGradient = CreateGradient(
            Color.red,
            ToColor(0xFF8200),
            Color.yellow,
            Color.green,
            ToColor(0x005DFF),
            ToColor(0xAE00FF),
            ToColor(0xFF00CB)
            );

        m_horizontalPlane = new Plane(Vector3.up, Vector3.zero);
        m_verticalPlane = new Plane(Vector3.back, Vector3.zero);

        m_trianglePoints = new Vector3[]
        {
                new Vector3(-0.333f, 0f, -0.333f),
                new Vector3(0f, 0f, 0.333f),
                new Vector3(0.333f, 0f, -0.333f)
        };

        m_circlePoints = new Vector3[CIRCLE_POINT_COUNT];

        for (int i = 0; i < CIRCLE_POINT_COUNT; i++)
        {
            float t = (i / (m_circlePoints.Length - 1f)) * 2f * Mathf.PI;
            m_circlePoints[i] = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t));
        }
    }

    private static LayerMask DefaultLayerMask
    {
        get
        {
            if (m_defaultLayer == -1)
                m_defaultLayer = LayerMask.NameToLayer("Default");

            return 1 << m_defaultLayer;
        }
    }

    #region Editor

#if UNITY_EDITOR
    /// <summary>
    /// It's strangely difficult to get a ray from the mouse position in a scene view during edit mode. This stupid method works, I guess.
    /// 
    /// (Note: HandleUtility.GUIPointToWorldRay is supposed to do this but only works in OnSceneGUI)
    /// </summary>
    public static Ray MouseToSceneViewRay(SceneView view, float offset = 0.05f)
    {
        Vector3 mousePosition = Event.current.mousePosition;

        mousePosition.x /= view.position.width; //scale to size
        mousePosition.y /= view.position.height;
        mousePosition.z = 1; //set Z to a sensible non-zero value so the raycast goes in the right direction
        mousePosition.y = 1 - mousePosition.y; //invert Y because UIs are top-down and cameras are bottom-up

        // and for some reason, the result is off by a tiny fraction every time.
        mousePosition.y += offset;

        return view.camera.ViewportPointToRay(mousePosition);
    }
#endif

    #endregion

    #region Unity Extensions
    
    /// <summary>
    /// Finds the highest value on the curve between 0 and 1 inputs. This is simply done by checking manually, so it's not 100% accurate.
    /// </summary>
    public static float FindHighestValue01(this AnimationCurve curve)
    {
        const int iterations = 100;
        return FindHighestValue(0f, 1f, curve.Evaluate, iterations);
    }

#if UNITY_EDITOR
    /// <summary>
    //	This makes it easy to create, name and place unique new ScriptableObject asset files.
    /// </summary>
    public static T CreateAsset<T>(string name = null) where T : ScriptableObject
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (path == "")
        {
            path = "Assets";
        }
        else if (Path.GetExtension(path) != "")
        {
            path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
        }

        name = "/" + (name ?? "New_" + typeof(T).ToString());

        return CreateAsset<T>(path, name);
    }

    /// <summary>
    //	This makes it easy to create, name and place unique new ScriptableObject asset files.
    /// </summary>
    public static T CreateAsset<T>(string path, string name = null) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + name + ".asset");

        Debug.Log(path + name + ".asset");
        
        AssetDatabase.CreateAsset(asset, assetPathAndName);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        return asset;
    }
    
    public static void SaveTexture<T>(string path, T texture, string name = null) where T : Texture
    {
        name = name ?? texture.name ?? ("New_" + typeof(T).ToString());

        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + name + ".asset");

        AssetDatabase.CreateAsset(texture, assetPathAndName);

        EditorUtility.SetDirty(texture);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
#endif

    public static T GetOrAddComponent<T>(this GameObject gO) where T : Component
    {
        T component = gO.GetComponent<T>();

        if (component == null)
            component = gO.AddComponent<T>() as T;

        return component;

    }

    /// <summary>
    /// Set this transforms local coordinates to zeros and identities.
    /// </summary>
    public static void Reset(this Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Create a Texture2D from the given rendertexture.
    /// </summary>
    public static Texture2D ToTexture2D(this RenderTexture renderTexture, UnityEngine.Experimental.Rendering.GraphicsFormat? format = null)
    {
        return renderTexture.ToTexture2D(renderTexture.width, renderTexture.height, format);
    }

    /// <summary>
    /// Create a Texture2D from the given rendertexture with the given width and height.
    /// </summary>
    public static Texture2D ToTexture2D(this RenderTexture renderTexture, int width, int height, UnityEngine.Experimental.Rendering.GraphicsFormat? format = null)
    {
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = renderTexture;

        Texture2D tex = new Texture2D(width, height, format ?? renderTexture.graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        tex.Apply();

        RenderTexture.active = active;

        return tex;
    }
    
    public static RenderTexture CreateTempRenderTexture(int width, int height, Color? col = null, UnityEngine.Experimental.Rendering.GraphicsFormat? format = null, int antiAliasing = 1)
    {
        RenderTexture texture = RenderTexture.GetTemporary(width, height, 24);
        texture.enableRandomWrite = true;
        texture.antiAliasing = antiAliasing;

        if (format != null)
            texture.graphicsFormat = format.Value;

        texture.Create();

        if (col != null)
            texture.FillWithColour(col.Value);

        return texture;
    }

    public static Texture2DArray CreateTextureArray(IList<Texture2D> textures, int width, int height, UnityEngine.Experimental.Rendering.GraphicsFormat? format = null)
    {
        Texture2DArray textureArray =
            new Texture2DArray(width, height, textures.Count, format ?? textures[0].graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };


        for (int i = 0; i < textures.Count; i++)
        {
            Graphics.CopyTexture(textures[i], 0, textureArray, i);
        }

        textureArray.Apply();

        return textureArray;
    }

    public static void FillWithColour(this RenderTexture renderTexture, Color colour)
    {
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, colour);
        RenderTexture.active = active;
    }

    #endregion

    #region Vectors

    public static bool IsInRange(this int val, int from, int to, bool inclusive = true)
    {
        if (inclusive)
            return val >= from && val <= to;

        return val > from && val < to;
    }

    public static bool IsInRange(this float val, float from, float to, bool inclusive = true)
    {
        if (inclusive)
            return val >= from && val <= to;

        return val > from && val < to;
    }

    public static bool IsInRange(this Vector3 input, float from, float to) => input.All(f => IsInRange(f, from, to));

    public static bool All(this Vector3 input, Func<float, bool> function) => function(input.x) && function(input.y) && function(input.z);
    public static bool Any(this Vector3 input, Func<float, bool> function) => function(input.x) || function(input.y) || function(input.z);

    public static (int x, int y, int z, int w) PiecewiseOp(this Vector4 input, Func<float, int> function) => (function(input.x), function(input.y), function(input.z), function(input.w));
    public static (int x, int y, int z) PiecewiseOp(this Vector3 input, Func<float, int> function) => (function(input.x), function(input.y), function(input.z));
    public static (int x, int y, int z) PiecewiseOp(this (int inX, int inY, int inZ) input, Func<int, int> function) => (function(input.inX), function(input.inY), function(input.inZ));
    public static Vector2 PiecewiseOp(this Vector2 input, Func<float, float> function) => new Vector2(function(input.x), function(input.y));
    public static Vector3 PiecewiseOp(this Vector3 input, Func<float, float> function) => new Vector3(function(input.x), function(input.y), function(input.z));
    public static Vector4 PiecewiseOp(this Vector4 input, Func<float, float> function) => new Vector4(function(input.x), function(input.y), function(input.z), function(input.w));

    public static Vector3 Abs(this Vector3 input) => PiecewiseOp(input, Mathf.Abs);
    public static Vector3 Saturate(this Vector3 input) => PiecewiseOp(input, Mathf.Clamp01);
    public static Vector3 Max0(this Vector3 input) => PiecewiseOp(input, (float f) => Mathf.Max(f, 0f));
    public static Vector4 Abs(this Vector4 input) => PiecewiseOp(input, Mathf.Abs);
    public static Vector4 Saturate(this Vector4 input) => PiecewiseOp(input, Mathf.Clamp01);
    public static Vector4 Max0(this Vector4 input) => input.Max(0f);

    public static Vector2 Max(this Vector2 input, float max) => PiecewiseOp(input, (float f) => Mathf.Max(f, max));
    public static Vector3 Max(this Vector3 input, float max) => PiecewiseOp(input, (float f) => Mathf.Max(f, max));
    public static Vector4 Max(this Vector4 input, float max) => PiecewiseOp(input, (float f) => Mathf.Max(f, max));
    public static Vector2 Min(this Vector2 input, float max) => PiecewiseOp(input, (float f) => Mathf.Min(f, max));
    public static Vector3 Min(this Vector3 input, float max) => PiecewiseOp(input, (float f) => Mathf.Min(f, max));
    public static Vector4 Min(this Vector4 input, float max) => PiecewiseOp(input, (float f) => Mathf.Min(f, max));
    public static Vector2 Clamp(this Vector2 input, float min, float max) => PiecewiseOp(input, (float f) => Mathf.Clamp(f, min, max));
    public static Vector3 Clamp(this Vector3 input, float min, float max) => PiecewiseOp(input, (float f) => Mathf.Clamp(f, min, max));
    public static Vector4 Clamp(this Vector4 input, float min, float max) => PiecewiseOp(input, (float f) => Mathf.Clamp(f, min, max));

    /// <summary>
    /// This method calculates a new direction from the given info, which avoids a position.
    /// </summary>
    /// <param name="currentPosition">The position of the thing doing the avoiding.</param>
    /// <param name="obstaclePosition">The position of the object to be avoided.</param>
    /// <param name="targetPosition">The goal position.</param>
    /// <param name="up">The up vector, used for cross product.</param>
    /// <param name="minimumDistance">The closest you can get to the obstacle. If the target is within this distance, the vector returned will simply be the direction to the target.</param>
    /// <param name="avoidance">How wide a berth to give the obstacle. Must be > 0.</param>
    public static Vector3 CalculateAvoidanceDirection(Vector3 currentPosition, Vector3 obstaclePosition, Vector3 targetPosition, Vector3 up, float minimumDistance, float avoidance)
    {
        float distanceFromTargetToPlayer = Vector3.Distance(targetPosition, obstaclePosition);
        Vector3 dirToTarget = (targetPosition - currentPosition).normalized;

        // in this case, there's no valid way to avoid, so just go straight for it
        if (distanceFromTargetToPlayer < minimumDistance)
            return dirToTarget;

        avoidance = Mathf.Max(0.05f, avoidance);

        Vector3 dirToPlayer = (obstaclePosition - currentPosition).normalized;
        float distanceToPlayer = Vector3.Distance(currentPosition, obstaclePosition);

        // this fixes the edge case where the target is in the avoidance zone
        avoidance = Mathf.Min(avoidance, distanceFromTargetToPlayer - minimumDistance);

        Vector3 cross = Vector3.Cross(up, dirToPlayer).normalized;

        if (Vector3.Dot(cross, dirToTarget) < 0f)
            cross = Vector3.Cross(dirToPlayer, up).normalized;

        float dot = Vector3.Dot(dirToTarget, dirToPlayer);

        Vector3 direction;
        if (dot > 0f)
            direction = Vector3.Lerp(cross, dirToTarget, Mathf.InverseLerp(minimumDistance, minimumDistance + avoidance, distanceToPlayer)).normalized;
        else
            direction = dirToTarget;

        return direction;
    }

    /// <summary>
    /// Given a point and a centre of rotation, rotate the point around the centre by the given
    /// euler angle.
    /// </summary>
    public static Vector3 RotateAround(Vector3 point, Vector3 centre, Vector3 eulerAngle)
    {
        return (Quaternion.Euler(eulerAngle) * (point - centre)) + centre;
    }

    /// <summary>
    /// Given a point and a centre of rotation, rotate the point around the centre by the given
    /// euler angle.
    /// </summary>
    public static Vector2 RotateAround(Vector2 point, Vector2 centre, float angle)
    {
        angle *= Mathf.Deg2Rad;

        Vector2 atOrigin = point - centre;
        return centre + (new Vector2(atOrigin.x * Mathf.Cos(angle) - atOrigin.y * Mathf.Sin(angle), atOrigin.y * Mathf.Cos(angle) + atOrigin.x * Mathf.Sin(angle)));
    }

    /// <summary>
    /// Given a point and a centre of rotation, rotate the (0, 1, 0) around the origin by the given
    /// euler angle.
    /// </summary>
    public static Vector3 RotateAround(Vector3 eulerAngle)
    {
        return Quaternion.Euler(eulerAngle) * Vector3.up;
    }

    /// <summary>
    /// Sets the y ordinate of this vector to 0.
    /// </summary>
    public static Vector3 Flatten(this Vector3 vec)
    {
        return new Vector3(vec.x, 0f, vec.z);
    }

    /// <summary>
    /// Returns true if this vector is zero.
    /// </summary>
    public static bool IsZero(this Vector3 vec)
    {
        return vec == Vector3.zero;
    }

    /// <summary>
    /// Returns true if this vector is zero.
    /// </summary>
    public static bool IsZero(this Vector2 vec)
    {
        return vec == Vector2.zero;
    }

    /// <summary>
    /// Returns whether this vector contains NaNs.
    /// </summary>
    public static bool IsNaN(this Vector2 vec)
    {
        return float.IsNaN(vec.x) || float.IsNaN(vec.y);
    }

    /// <summary>
    /// Returns whether this vector contains NaNs.
    /// </summary>
    public static bool IsNaN(this Vector3 vec)
    {
        return float.IsNaN(vec.x) || float.IsNaN(vec.y) || float.IsNaN(vec.z);
    }

    /// <summary>
    /// Returns whether this vector contains NaNs.
    /// </summary>
    public static bool IsNaN(this Vector4 vec)
    {
        return float.IsNaN(vec.x) || float.IsNaN(vec.y) || float.IsNaN(vec.z) || float.IsNaN(vec.w);
    }

    /// <summary>
    /// Set the x ordinate of this vector.
    /// </summary>
    public static Vector4 SetX(this Vector4 vec, float x)
    {
        return new Vector4(x, vec.y, vec.z, vec.w);
    }

    /// <summary>
    /// Set the y ordinate of this vector.
    /// </summary>
    public static Vector4 SetY(this Vector4 vec, float y)
    {
        return new Vector4(vec.x, y, vec.z, vec.w);
    }

    /// <summary>
    /// Set the z ordinate of this vector.
    /// </summary>
    public static Vector4 SetZ(this Vector4 vec, float z)
    {
        return new Vector4(vec.x, vec.y, z, vec.w);
    }

    /// <summary>
    /// Set the w ordinate of this vector.
    /// </summary>
    public static Vector4 SetW(this Vector4 vec, float w)
    {
        return new Vector4(vec.x, vec.y, vec.z, w);
    }

    /// <summary>
    /// Set the x ordinate of this vector.
    /// </summary>
    public static Vector3 SetX(this Vector3 vec, float x)
    {
        return new Vector3(x, vec.y, vec.z);
    }

    /// <summary>
    /// Set the y ordinate of this vector.
    /// </summary>
    public static Vector3 SetY(this Vector3 vec, float y)
    {
        return new Vector3(vec.x, y, vec.z);
    }

    /// <summary>
    /// Set the z ordinate of this vector.
    /// </summary>
    public static Vector3 SetZ(this Vector3 vec, float z)
    {
        return new Vector3(vec.x, vec.y, z);
    }

    /// <summary>
    /// Adds a value to the x ordinate of this vector.
    /// </summary>
    public static Vector3 AddToX(this Vector3 vec, float x)
    {
        return new Vector3(vec.x + x, vec.y, vec.z);
    }

    /// <summary>
    /// Adds a value to the y ordinate of this vector.
    /// </summary>
    public static Vector3 AddToY(this Vector3 vec, float y)
    {
        return new Vector3(vec.x, vec.y + y, vec.z);
    }

    /// <summary>
    /// Adds a value to the z ordinate of this vector.
    /// </summary>
    public static Vector3 AddToZ(this Vector3 vec, float z)
    {
        return new Vector3(vec.x, vec.y, vec.z + z);
    }
    /// <summary>
    /// Set the x ordinate of this vector.
    /// </summary>
    public static Vector2 SetX(this Vector2 vec, float x)
    {
        return new Vector2(x, vec.y);
    }

    /// <summary>
    /// Set the y ordinate of this vector.
    /// </summary>
    public static Vector2 SetY(this Vector2 vec, float y)
    {
        return new Vector2(vec.x, y);
    }

    /// <summary>
    /// Inverse lerp function for Vector3. Given a vector A and a vector B and a colinear vector 
    /// between them, return the value which would be required to lerp from A to B.
    /// </summary>
    public static float InverseLerp(Vector3 a, Vector3 b, Vector3 t)
    {
        Vector3 AB = b - a;
        Vector3 AV = t - a;
        return Vector3.Dot(AV, AB) / Vector3.Dot(AB, AB);
    }

    /// <summary>
    /// Given a normal, return a random new direction in a range of angles around that normal.
    /// </summary>
    public static Vector3 GetRandomDirectionInRange(Vector3 normal, float maxAngle)
    {
        float GetRandomAngle() => UnityEngine.Random.Range(-maxAngle * 0.5f, maxAngle * 0.5f);

        float xDegrees = GetRandomAngle();
        float zDegrees = GetRandomAngle();

        Quaternion globalRot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(xDegrees, 0f, zDegrees);

        return globalRot * Vector3.up;
    }

    /// <summary>
    /// EqualityComparer for Vector3s which smooths over floating point imprecisions to a given precision.
    /// </summary>
    public struct Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        public int Precision { get; private set; }

        public bool Equals(Vector3 x, Vector3 y)
        {
            return GetHashCode(x) == GetHashCode(y);
        }

        public int GetHashCode(Vector3 obj)
        {
            return (int)(obj.x * Precision) ^ ((int)(obj.y * Precision) << 2) ^ ((int)(obj.z * Precision) >> 2);
        }

        public Vector3EqualityComparer(int precision) => Precision = precision;
    }

    public static Vector3 XYZ(this Vector4 vec) =>
        new Vector3(vec.x, vec.y, vec.z);

    #endregion

    #region Data Structures

    /// <summary>
    /// Returns a string of the given array in the form [x, y, z...]
    /// </summary>
    public static string ToFormattedString<T>(this IList<T> array) => ToFormattedString(array, 0, array.Count);

    /// <summary>
    /// Returns a string of the given array in the form [x, y, z...]
    /// </summary>
    public static string ToFormattedString<T>(this IList<T> array, int startIndex) => ToFormattedString(array, startIndex, array.Count);

    /// <summary>
    /// Returns a string of the given array in the form [x, y, z...]
    /// </summary>
    public static string ToFormattedString<T>(this IList<T> array, int startIndex, int endIndex)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");

        endIndex = Mathf.Min(endIndex, array.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            sb.Append(array[i].ToString());

            if (i < endIndex - 1)
                sb.Append(", ");
        }

        sb.Append("]");

        return sb.ToString();
    }

    /// <summary>
    /// Returns a random value from a dictionary.
    /// 
    /// Todo: Find a way to do this without making allocations.
    /// </summary>
    public static V GetRandomValue<K, V>(this Dictionary<K, V> dict)
    {
        List<KeyValuePair<K, V>> list = dict.ToList();

        return list[UnityEngine.Random.Range(0, list.Count)].Value;
    }

    /// <summary>
    /// Remove and return the first element of the list.
    /// </summary>
    public static T PopFirst<T>(this LinkedList<T> list)
    {
        T first = list.First();
        list.RemoveFirst();
        return first;
    }

    /// <summary>
    /// Remove and return the last element of the list.
    /// </summary>
    public static T PopLast<T>(this LinkedList<T> list)
    {
        T first = list.Last();
        list.RemoveLast();
        return first;
    }

    /// <summary>
    /// An ICollection has a count property available that is O(1), an enumerable that is also a collection will 
    /// prefer to use this extension without the need to use the Any() method
    /// </summary>
    public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
    {
        return collection == null || collection.Count == 0;
    }

    /// <summary>
    /// Returns whether the given index is outside of the range of this collection.
    /// </summary>
    public static bool IsOutOfBounds<T>(this ICollection<T> collection, int index)
    {
        return index < 0 || index >= collection.Count;
    }

    public static void PushFront<T>(this List<T> list, T toAdd)
    {
        list.Insert(0, toAdd);
    }

    public static void PushBack<T>(this List<T> list, T toAdd)
    {
        //list.Insert(list.Count - 1, toAdd);
        list.Add(toAdd);
    }

    public static T PopFront<T>(this List<T> list)
    {
        T tempElement = list[0];
        list.RemoveAt(0);
        return tempElement;
    }

    #endregion

    #region Colours

    private static readonly Gradient m_rainbowGradient;

    /// <summary>
    /// Creates a gradient of the given colours, distributed evenly.
    /// </summary>
    public static Gradient CreateGradient(params Color[] colours)
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colourKeys = new GradientColorKey[colours.Length];

        for (int i = 0; i < colours.Length; i++)
        {
            colourKeys[i].color = colours[i];
            colourKeys[i].time = i / (colours.Length - 1f);
        }

        GradientAlphaKey[] alphaKey = new GradientAlphaKey[1];

        for (int i = 0; i < alphaKey.Length; i++)
            alphaKey[i].alpha = 1f;

        gradient.SetKeys(colourKeys, alphaKey);

        return gradient;
    }

    /// <summary>
    /// Given a value between 0 and 1, return a rainbow colour.
    /// </summary>
    public static Color RainbowLerp(float t)
    {
        return m_rainbowGradient.Evaluate(t);
    }

    /// <summary>
    /// Convert an RGB hex value to a Color32.
    /// </summary>
    public static Color32 ToColor32(int hexVal)
    {
        byte R = (byte)((hexVal >> 16) & 0xFF);
        byte G = (byte)((hexVal >> 8) & 0xFF);
        byte B = (byte)((hexVal) & 0xFF);
        return new Color32(R, G, B, 255);
    }

    /// <summary>
    /// Convert an RGB hex value to a Color.
    /// </summary>
    public static Color ToColor(int hexVal)
    {
        float R = ((byte)((hexVal >> 16) & 0xFF)) / 255f;
        float G = ((byte)((hexVal >> 8) & 0xFF)) / 255f;
        float B = ((byte)((hexVal) & 0xFF)) / 255f;
        return new Color(R, G, B, 1f);
    }

    public static Color SetAlpha(this Color color, float a)
    {
        return new Color(color.r, color.g, color.b, a);
    }

    public static Color Brighten(this Color color, float val = 0.5f)
    {
        return Color.Lerp(color, Color.white, Mathf.Clamp01(val));
    }

    public static Color Darken(this Color color, float val = 0.5f)
    {
        return Color.Lerp(color, Color.black, Mathf.Clamp01(val));
    }

    #endregion

    #region Gizmos

    public static void Label(this Component mono, object text, int line = 0, int size = 20, Color col = default)
    {
#if UNITY_EDITOR
        Vector3 pos = mono.transform.position + Vector3.one * 0.5f;
        Label(pos, text, line, size, col);
#endif
    }


    public static void Label(Vector3 position, object text, int line = 0, int size = 20, Color col = default)
    {
#if UNITY_EDITOR
        if (line != 0f)
        {
            Camera camera = Camera.current;
            position = camera.ScreenToWorldPoint(camera.WorldToScreenPoint(position) - Vector3.up * line * 22f);
        }

        GUIStyle style = new GUIStyle();
        style.normal.textColor = col == default ? Color.black : col;
        style.fontSize = size;

        Handles.Label(position, text.ToString(), style);
#endif
    }


    public static void DrawArrow(Vector3 position, Vector3 direction, Vector3 normal, float arrowLength = 1f, float arrowheadScale = 0.4f)
    {
#if UNITY_EDITOR
        const float LINE_THICKNESS = 4f;

        Matrix4x4 handleMatrix = Handles.matrix;

        Handles.DrawAAPolyLine(LINE_THICKNESS, position, position + direction * arrowLength);

        Handles.matrix = Matrix4x4.TRS(position + direction * arrowLength, Quaternion.LookRotation(direction, normal), arrowheadScale * Vector3.one);
        Handles.DrawAAConvexPolygon(m_trianglePoints);

        Handles.matrix = handleMatrix;
#endif
    }

    public static void DrawAAWireDisc(Vector3 centre, Vector3 normal, float radius)
    {
#if UNITY_EDITOR
        const float LINE_THICKNESS = 4f;

        Matrix4x4 handleMatrix = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(centre, Quaternion.FromToRotation(Vector3.up, normal), radius * Vector3.one);

        Handles.DrawAAPolyLine(LINE_THICKNESS, m_circlePoints);

        Handles.matrix = handleMatrix;
#endif
    }

    #endregion

    #region Maths
    
    /// <summary>
    /// Returns the distance from p to the triangle described by the vertices a, b, c.
    /// 
    /// From: https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
    /// </summary>
    public static float DistanceToTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float Dot2(Vector3 v) => Vector3.Dot(v, v);

        Vector3 ba = b - a; Vector3 pa = p - a;
        Vector3 cb = c - b; Vector3 pb = p - b;
        Vector3 ac = a - c; Vector3 pc = p - c;

        Vector3 normal = Vector3.Cross(ba, ac);

        return Mathf.Sqrt(
          (Mathf.Sign(Vector3.Dot(Vector3.Cross(ba, normal), pa)) +
           Mathf.Sign(Vector3.Dot(Vector3.Cross(cb, normal), pb)) +
           Mathf.Sign(Vector3.Dot(Vector3.Cross(ac, normal), pc)) < 2f)
           ?
           Mathf.Min(Mathf.Min(
           Dot2(ba * Mathf.Clamp01(Vector3.Dot(ba, pa) / Dot2(ba)) - pa),
           Dot2(cb * Mathf.Clamp01(Vector3.Dot(cb, pb) / Dot2(cb)) - pb)),
           Dot2(ac * Mathf.Clamp01(Vector3.Dot(ac, pc) / Dot2(ac)) - pc))
           :
           Vector3.Dot(normal, pa) * Vector3.Dot(normal, pa) / Dot2(normal));
    }

    //public static float DistanceToTriangle2(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    //{
    //    Vector3 ba = b - a;
    //    Vector3 pa = p - a;
    //    Vector3 cb = c - b;
    //    Vector3 pb = p - b;
    //    Vector3 ac = a - c;
    //    Vector3 pc = p - c;

    //    Vector3 normal = Vector3.Cross(ba, ac);

    //    return Mathf.Sqrt(
    //    (sign(dot(cross(ba, nor), pa)) +
    //     sign(dot(cross(cb, nor), pb)) +
    //     sign(dot(cross(ac, nor), pc)) < 2.0)
    //     ?
    //     min(min(
    //     dot2(ba * clamp(dot(ba, pa) / dot2(ba), 0.0, 1.0) - pa),
    //     dot2(cb * clamp(dot(cb, pb) / dot2(cb), 0.0, 1.0) - pb)),
    //     dot2(ac * clamp(dot(ac, pc) / dot2(ac), 0.0, 1.0) - pc))
    //     :
    //     dot(nor, pa) * dot(nor, pa) / dot2(nor));
    //}

    /// <summary>
    /// Given a point p and a triangle described by the vertices a, b, c, return the barycentric coordinates 
    /// of p on that triangle.    
    /// </summary>
    public static void GetBarycentricCoordinates(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float u, out float v, out float w)
    {
        Vector3 v0 = b - a;
        Vector3 v1 = c - a;
        Vector3 v2 = p - a;

        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;

        v = (d11 * d20 - d01 * d21) / denom;
        w = (d00 * d21 - d01 * d20) / denom;
        u = 1f - v - w;
    }

    /// <summary>
    /// Given a point p, a triangle described by the vertices a, b, c, and Vector4 values at each of these vertices,
    /// return the Vector4 value for p which is smoothly interpolated between the vertices.
    /// </summary>
    public static Vector4 BarycentricInterpolation(Vector3 p, Vector3 v1, Vector3 v2, Vector3 v3, Vector4 val1, Vector4 val2, Vector4 val3)
    {
        GetBarycentricCoordinates(p, v1, v2, v3, out float w1, out float w2, out float w3);

        return w1 * val1 + w2 * val2 + w3 * val3;
    }

    /// <summary>
    /// Given a point p, a triangle described by the vertices a, b, c, and Color values at each of these vertices,
    /// return the Color value for p which is smoothly interpolated between the vertices.
    /// </summary>
    public static Color BarycentricInterpolation(Vector3 p, Vector3 v1, Vector3 v2, Vector3 v3, Color val1, Color val2, Color val3)
    {
        GetBarycentricCoordinates(p, v1, v2, v3, out float w1, out float w2, out float w3);

        return w1 * val1 + w2 * val2 + w3 * val3;
    }

    /// <summary>
    /// Given a point p, a triangle described by the vertices a, b, c, and Vector3 values at each of these vertices,
    /// return the Vector3 value for p which is smoothly interpolated between the vertices.
    /// </summary>
    public static Vector3 BarycentricInterpolation(Vector3 p, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 val1, Vector3 val2, Vector3 val3)
    {
        GetBarycentricCoordinates(p, v1, v2, v3, out float w1, out float w2, out float w3);

        return w1 * val1 + w2 * val2 + w3 * val3;
    }

    /// <summary>
    /// Given a point p, a triangle described by the vertices a, b, c, and float values at each of these vertices,
    /// return the float value for p which is smoothly interpolated between the vertices.
    /// </summary>
    public static float BarycentricInterpolation(Vector3 p, Vector3 v1, Vector3 v2, Vector3 v3, float val1, float val2, float val3)
    {
        GetBarycentricCoordinates(p, v1, v2, v3, out float w1, out float w2, out float w3);

        return w1 * val1 + w2 * val2 + w3 * val3;
    }


    /// <summary>
    /// Interpolate values between a cube of points.
    /// </summary>
    public static float TrilinearInterpolate(Vector3 frac, float a, float b, float c, float d, float e, float f, float g, float h)
    {
        /*      g-------h
         *     /|      /|
         *    / |     / |
         *   c--|----d  |
         *   |  e----|--f
         *   | /     | /
         *   a-------b
         */

        // x axis
        float aToB = Mathf.Lerp(a, b, frac.x);
        float cToD = Mathf.Lerp(c, d, frac.x);
        float eToF = Mathf.Lerp(e, f, frac.x);
        float gToH = Mathf.Lerp(g, h, frac.x);

        // y axis
        float y1 = Mathf.Lerp(aToB, cToD, frac.y);
        float y2 = Mathf.Lerp(eToF, gToH, frac.y);

        // finally, x axis
        return Mathf.Lerp(y1, y2, frac.z);
    }

    /// <summary>
    /// Interpolate values between a cube of points.
    /// </summary>
    public static Vector3 TrilinearInterpolate(Vector3 frac, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f, Vector3 g, Vector3 h)
    {
        /*      g-------h
         *     /|      /|
         *    / |     / |
         *   c--|----d  |
         *   |  e----|--f
         *   | /     | /
         *   a-------b
         */

        // x axis
        Vector3 aToB = Vector3.Lerp(a, b, frac.x);
        Vector3 cToD = Vector3.Lerp(c, d, frac.x);
        Vector3 eToF = Vector3.Lerp(e, f, frac.x);
        Vector3 gToH = Vector3.Lerp(g, h, frac.x);

        // y axis
        Vector3 y1 = Vector3.Lerp(aToB, cToD, frac.y);
        Vector3 y2 = Vector3.Lerp(eToF, gToH, frac.y);

        // finally, x axis
        return Vector3.Lerp(y1, y2, frac.z);
    }


    /// <summary>
    /// Given a set of vertices, normals at those vertices, and a point between the three vertices, calculate an 'interpolated surface position'
    /// which is implied by the normals. Explained better here: http://cs.engr.uky.edu/~cheng/PUBL/Paper_Nagata.pdf
    /// </summary>
    private static Vector3 InterpolateSurfacePosition(Vector3 p, Vector3 v_0, Vector3 v_1, Vector3 v_2, Vector3 n_0, Vector3 n_1, Vector3 n_2)
    {
        Vector3 C(Vector3 D, Vector3 _n0, Vector3 _n1)
        {
            Vector3 _v = (_n0 + _n1) * 0.5f;
            Vector3 dv = (_n0 - _n1) * 0.5f;
            float d = Vector3.Dot(D, _v);
            float dd = Vector3.Dot(D, dv);
            float c = Vector3.Dot(_n0, _n0 - 2f * dv);
            float dc = Vector3.Dot(_n0, dv);

            if (Mathf.Approximately(c, -1f) || Mathf.Approximately(c, 1f))
                return Vector3.zero;

            return dd / (1f - dc) * _v + d / dc * dv;
        }

        GetBarycentricCoordinates(p, v_0, v_1, v_2, out float u, out float v, out float w);

        Vector3 d1 = v_1 - v_0;
        Vector3 d2 = v_2 - v_1;
        Vector3 d3 = v_2 - v_0;

        Vector3 c1 = C(d1, n_0, n_1);
        Vector3 c2 = C(d2, n_1, n_2);
        Vector3 c3 = C(d3, n_0, n_2);

        // u = 1−η
        // v = η−ζ
        // w = ζ

        return
            (v_0 * u) +
            (v_1 * v) +
            (v_2 * w)
            -
            (c1 * u * v) -
            (c2 * v * w) -
            (c3 * u * w);
    }

    /// <summary>
    /// Given a function and a range, manually find the earliest x value for which it outputs the given y.
    /// </summary>
    public static bool TryFindFirstY(float from, float to, float y, Func<float, float> function, out float result, int iterations = 100)
    {
        for (int i = 0; i < iterations; i++)
        {
            float x = Mathf.Lerp(from, to, i / (iterations - 1f));
            float value = function(x);

            if (value >= y)
            {
                result = x;
                return true;
            }
        }

        result = -1f;
        return false;
    }

    /// <summary>
    /// This function finds the highest output value manually, in the given range.
    /// </summary>
    public static float FindHighestValue(float from, float to, Func<float, float> function, int iterations = 100)
    {
        float highestValue = float.NegativeInfinity;

        for (int i = 0; i < iterations; i++)
        {
            float value = function(Mathf.Lerp(from, to, i / (iterations - 1f)));

            if (value > highestValue)
                highestValue = value;
        }

        return highestValue;
    }

    public static void SortAnticlockwise(Vector3 normal, Vector3 a, Vector3 b, Vector3 c, int a_index, int b_index, int c_index, out Vector3 a_sorted, out Vector3 b_sorted, out Vector3 c_sorted, out int a_index_sorted, out int b_index_sorted, out int c_index_sorted)
    {
        Quaternion rot = Quaternion.FromToRotation(normal, Vector3.up);

        Vector3 a_rot = rot * a;
        Vector3 b_rot = rot * b;
        Vector3 c_rot = rot * c;

        float a_angle = Get2DClockwiseAngle(a_rot);
        float b_angle = Get2DClockwiseAngle(b_rot);
        float c_angle = Get2DClockwiseAngle(c_rot);

        if (a_angle < b_angle)
        {
            if (b_angle < c_angle)
            {
                // a, b, c
                a_sorted = a;
                b_sorted = b;
                c_sorted = c;

                a_index_sorted = a_index;
                b_index_sorted = b_index;
                c_index_sorted = c_index;
            }
            else if (a_angle < c_angle)
            {
                // a, c, b
                a_sorted = a;
                b_sorted = c;
                c_sorted = b;

                a_index_sorted = a_index;
                b_index_sorted = c_index;
                c_index_sorted = b_index;
            }
            else
            {
                // c, a, b
                a_sorted = c;
                b_sorted = a;
                c_sorted = b;

                a_index_sorted = c_index;
                b_index_sorted = a_index;
                c_index_sorted = b_index;
            }
        }
        else
        {
            if (a_angle < c_angle)
            {
                // b, a, c
                a_sorted = b;
                b_sorted = a;
                c_sorted = c;

                a_index_sorted = b_index;
                b_index_sorted = a_index;
                c_index_sorted = c_index;
            }
            else if (b_angle < c_angle)
            {
                // b, c, a
                a_sorted = b;
                b_sorted = c;
                c_sorted = a;

                a_index_sorted = b_index;
                b_index_sorted = c_index;
                c_index_sorted = a_index;
            }
            else
            {
                // c, b, a
                a_sorted = c;
                b_sorted = b;
                c_sorted = a;

                a_index_sorted = c_index;
                b_index_sorted = b_index;
                c_index_sorted = a_index;
            }
        }
    }

    /// <summary>
    /// Just use Acos to get the clockwise angle of the given 2d direction in degrees.
    /// </summary>
    public static float Get2DClockwiseAngle(Vector2 direction)
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle;
    }

    public static float Remap(float oldMin, float oldMax, float newMin, float newMax, float t)
    {
        return Mathf.Lerp(newMin, newMax, Mathf.InverseLerp(oldMin, oldMax, t));
    }

    public static Vector3 Remap(Vector3 oldMin, Vector3 oldMax, Vector3 newMin, Vector3 newMax, Vector3 t)
    {
        return new Vector3
        (
            Remap(oldMin.x, oldMax.x, newMin.x, newMax.x, t.x),
            Remap(oldMin.y, oldMax.y, newMin.y, newMax.y, t.y),
            Remap(oldMin.z, oldMax.z, newMin.z, newMax.z, t.z)
        );
    }

    /// <summary>
    /// This function finds out on which side of a line segment the point is located.
    /// The point is assumed to be on a line created by linePoint1 and linePoint2. If the point is not on
    /// the line segment, project it on the line using ProjectPointOnLine() first.
    /// Returns 0 if point is on the line segment.
    /// Returns 1 if point is outside of the line segment and located on the side of linePoint1.
    /// Returns 2 if point is outside of the line segment and located on the side of linePoint2.
    /// </summary>
    public static int PointOnWhichSideOfLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point)
    {
        Vector3 lineVec = linePoint2 - linePoint1;
        Vector3 pointVec = point - linePoint1;

        float dot = Vector3.Dot(pointVec, lineVec);

        if (dot > 0)
        {
            // point is on side of linePoint2, compared to linePoint1

            //point is on the line segment
            if (pointVec.magnitude <= lineVec.magnitude)
                return 0;

            //point is not on the line segment and it is on the side of linePoint2
            else
                return 2;
        }
        else
        {
            // Point is not on side of linePoint2, compared to linePoint1.
            // Point is not on the line segment and it is on the side of linePoint1.
            return 1;
        }
    }

    /// <summary>
    /// This function returns a point which is a projection from a point to a line.
    /// The line is considered infinite. If the line is finite, use ProjectPointOnLineSegment() instead.
    /// </summary>
    public static Vector3 ProjectPointOnLine(Vector3 linePoint, Vector3 lineDir, Vector3 point)
    {
        //get vector from point on line to point in space
        Vector3 linePointToPoint = point - linePoint;

        float t = Vector3.Dot(linePointToPoint, lineDir);

        return linePoint + lineDir * t;
    }

    /// <summary>
    /// This function returns a point which is a projection from a point to a line segment.
    /// If the projected point lies outside of the line segment, the projected point will 
    /// be clamped to the appropriate line edge.
    /// If the line is infinite instead of a segment, use ProjectPointOnLine() instead.
    /// </summary>
    public static Vector3 ProjectPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        // if this method looks weird, it was because it was written to be shader code, and
        // so has no conditionals

        Vector3 diff = b - a;
        float diffMagnitude = diff.magnitude;
        Vector3 lineDirection = diff / diffMagnitude;

        //get vector from point on line to point in space
        Vector3 linePointToLineDirection = point - a;

        // how far along the point is from a to b
        float t = Vector3.Dot(linePointToLineDirection, lineDirection);

        Vector3 projectedPoint = a + lineDirection * t;

        Vector3 pointVec = projectedPoint - a;

        float dot = Vector3.Dot(pointVec, diff);

        float dotGreaterThanZero = Mathf.Ceil(Mathf.Max(dot, 0f));
        float pointVecMagnitudeLessThanOrEqualToDiffMagnitude = Mathf.Max(Mathf.Sign(diffMagnitude - pointVec.magnitude), 0f);

        return Vector3.Lerp(a, Vector3.Lerp(b, projectedPoint, pointVecMagnitudeLessThanOrEqualToDiffMagnitude), dotGreaterThanZero);
    }

    private static Plane m_horizontalPlane;
    private static Plane m_verticalPlane;

    /// <summary>
    /// Provides the position where the ray from the input cursor hits y = 0.
    /// </summary>
    public static Vector3 GetMousePosAtYZero(Camera camera)
    {
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);

        if (m_horizontalPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return Vector3.zero;
    }

    /// <summary>
    /// Provides the position where the ray from the input cursor hits z = 0.
    /// </summary>
    public static Vector3 GetMousePos2D(Camera camera)
    {
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);

        if (m_verticalPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return Vector3.zero;
    }

    public static float ClampAngle(float angle, float from, float to)
    {
        while (angle > 180)
            angle = 360 - angle;

        angle = Mathf.Clamp(angle, from, to);

        while (angle < 0)
            angle += 360f;

        return angle;
    }

    public static float ClampToBearing(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }

    /// <summary>
    /// Round the given number to the nearest given number.
    /// </summary>
    public static float RoundToNearest(float val, float rounding)
    {
        val /= rounding;
        val = Mathf.Round(val);
        return val * rounding;
    }

    /// <summary>
    /// Round the given number to the nearest given number.
    /// </summary>
    public static int RoundToNearest(int val, int rounding)
    {
        val /= rounding;
        //val = Mathf.Round(val);
        return val * rounding;
    }

    /// <summary>
    /// Floor the given number to the nearest given number.
    /// </summary>
    public static float FloorToNearest(float val, float rounding)
    {
        val /= rounding;
        val = Mathf.Floor(val);
        return val * rounding;
    }

    /// <summary>
    /// Apply the SmoothStep function to a Vector3.
    /// </summary>
    public static Vector3 SmoothStep(Vector3 from, Vector3 to, float t)
    {
        return new Vector3(
            Mathf.SmoothStep(from.x, to.x, t),
            Mathf.SmoothStep(from.y, to.y, t),
            Mathf.SmoothStep(from.z, to.z, t));
    }

    /// <summary>
    /// Acts like lerp at the start and smoothstep at the end.
    /// </summary>
    public static float Sinerp(float from, float to, float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(from, to, Mathf.Sin(t * Mathf.PI * 0.5f));
    }

    /// <summary>
    /// Acts like smoothstep at the start and lerp at the end.
    /// </summary>
    public static float Coserp(float from, float to, float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(from, to, 1f - Mathf.Cos(t * Mathf.PI * 0.5f));
    }

    public const float BERP_OVERSHOOT = 1.0842f;

    /// <summary>
    /// Short for 'boing-like interpolation', this method will first overshoot, then waver back and forth around the end value before coming to a rest.
    /// 
    /// (This overshoots to 1.0842.)
    /// </summary>
    public static float Berp(float from, float to, float t, float springiness = 2.5f)
    {
        t = Mathf.Clamp01(t);
        t = (Mathf.Sin(t * Mathf.PI * (0.2f + springiness * t * t * t)) * Mathf.Pow(1f - t, 2.2f) + t) * (1f + (1.2f * (1f - t)));

        return from + (to - from) * t;
    }

    /// <summary>
    /// Same as Unity's inverse lerp function, but values are extrapolated outside the range [0, 1].
    /// </summary>
    public static float InverseLerpUnclamped(float a, float b, float value)
    {
        return (value - a) / (b - a);
    }

    /// <summary>
    /// A 'true mod' function, unlike C#'s % operator which is just a remainder,
    /// this works well with negative numbers.
    /// </summary>
    public static float Mod(this float x, float m)
    {
        return (x % m + m) % m;
    }

    /// <summary>
    /// A 'true mod' function, unlike C#'s % operator which is just a remainder,
    /// this works well with negative numbers.
    /// </summary>
    public static int Mod(this int x, int m)
    {
        return (x % m + m) % m;
    }

    /// <summary>
    /// Safely increase an index in a list while looping around instead of overflowing.
    /// </summary>
    public static int Increment(this int index, int count)
    {
        return (index + 1).Mod(count);
    }

    /// <summary>
    /// Safely decrease an index in a list while looping around instead of overflowing.
    /// </summary>
    public static int Decrement(this int index, int count)
    {
        return (index - 1).Mod(count);
    }


    /// <summary>
    /// Safely increase an index in a list while looping around instead of overflowing.
    /// </summary>
    public static int Increment<T>(this int index, ICollection<T> col)
    {
        return (index + 1).Mod(col.Count);
    }

    /// <summary>
    /// Safely decrease an index in a list while looping around instead of overflowing.
    /// </summary>
    public static int Decrement<T>(this int index, ICollection<T> col)
    {
        return (index - 1).Mod(col.Count);
    }

    /// <summary>
    /// Check if one angle is between two other angles.
    /// </summary>
    public static bool IsInSpan(float value, float from, float to)
    {
        value = value.Mod(360f);
        to = to.Mod(360f);
        from = from.Mod(360f);

        if (from <= to)
            return value >= from && value <= to;
        else
            return value >= from || value <= to;
    }

    /// <summary>
    /// Returns whether the given lines intersect. Both lines are defined by a direction and a point along the line.
    /// If an intersection is found, it will be returned as an out param.
    /// </summary>
    public static bool LineIntersection(out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {
        Vector3 lineVec3 = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parallel
        if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.00000001f)
        //if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > float.Epsilon)
        {
            float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }

    public static bool LineSegmentsIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 intersection)
    {
        bool result = LineSegmentsIntersection(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z), new Vector2(p3.x, p3.z), new Vector2(p4.x, p4.z), out Vector2 flatIntersection);

        intersection = new Vector3(flatIntersection.x, 0f, flatIntersection.y);

        return result;
    }

    public static bool LineSegmentsIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;

        float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);

        if (d == 0f)
            return false;

        float u = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
        float v = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;

        if (u < 0.0f || u > 1.0f || v < 0.0f || v > 1.0f)
            return false;

        intersection.x = p1.x + u * (p2.x - p1.x);
        intersection.y = p1.y + u * (p2.y - p1.y);

        return true;
    }

    /// <summary>
    /// Return the rotation component of a TRS matrix.
    /// </summary>
    public static Quaternion ExtractRotation(this Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    /// <summary>
    /// Return the translation component of a TRS matrix.
    /// </summary>
    public static Vector3 ExtractTranslation(this Matrix4x4 matrix)
    {
        Vector3 position;
        position.x = matrix.m03;
        position.y = matrix.m13;
        position.z = matrix.m23;
        return position;
    }

    /// <summary>
    /// Return the scale component of a TRS matrix.
    /// </summary>
    public static Vector3 ExtractScale(this Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }

    #endregion

    #region Physics

    /// <summary>
    /// Given a vector, a layer mask, and optionally, a direction (defaults to down), raycast in that direction on
    /// that layer mask, and if the raycast hits anything, return the result.
    /// </summary>
    public static Vector3 ClampToLayer(this Vector3 vec, LayerMask layerMask, GameObject ignore = null, Vector3 dir = default)
    {
        if (dir == default)
            dir = -Vector3.up;

        const float CLAMP_SIZE = 4f;

        int results = Physics.RaycastNonAlloc(vec - dir * CLAMP_SIZE * 0.5f, dir, m_nonAllocClampRaycastResults, CLAMP_SIZE, layerMask);

        if (results == 1 && m_nonAllocClampRaycastResults[0].collider.gameObject != ignore)
        {
            return m_nonAllocClampRaycastResults[0].point;
        }
        else if (results > 1)
        {
            Vector3 closest = vec;
            float leastSqrMag = Mathf.Infinity;

            for (int i = results - 1; i >= 0; --i)
            {
                if (ignore == null || m_nonAllocClampRaycastResults[i].collider.gameObject != ignore)
                {
                    Vector3 point = m_nonAllocClampRaycastResults[i].point;

                    float sqrMag = (vec - point).sqrMagnitude;

                    if (sqrMag < leastSqrMag)
                    {
                        closest = point;
                        leastSqrMag = sqrMag;
                    }
                }
            }

            return closest;
        }

        return vec;
    }

    /// <summary>
    /// Given a vector, and optionally, a direction (defaults to down), raycast in that direction on
    /// the default layer, and if the raycast hits anything, return the result.
    /// </summary>
    public static Vector3 ClampToDefaultLayer(this Vector3 vec, GameObject ignore = null, Vector3 dir = default) => ClampToLayer(vec, DefaultLayerMask, ignore, dir);

    #endregion

    #region UI

    /// <summary>
    /// Force the same behaviour as when the mouse pointer enters a selectable.
    /// </summary>
    public static void SimulatePointerEnter(this Selectable selectable)
    {
        ExecuteEvents.Execute(selectable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
    }

    /// <summary>
    /// Force the same behaviour as when the mouse pointer exits a selectable.
    /// </summary>
    public static void SimulatePointerExit(this Selectable selectable)
    {
        ExecuteEvents.Execute(selectable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
    }

    /// <summary>
    /// Force the same behaviour as when the mouse pointer clicks (up and down) a selectable.
    /// </summary>
    public static void SimulatePointerClick(this Selectable selectable)
    {
        ExecuteEvents.Execute(selectable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }

    /// <summary>
    /// Force the same behaviour as when the mouse pointer clicks a selectable.
    /// </summary>
    public static void SimulatePointerDown(this Selectable selectable)
    {
        ExecuteEvents.Execute(selectable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
    }

    /// <summary>
    /// Force the same behaviour as when the mouse pointer releases a click on a selectable.
    /// </summary>
    public static void SimulatePointerUp(this Selectable selectable)
    {
        ExecuteEvents.Execute(selectable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
    }

    /// <summary>
    /// Tell a scroll rect to go to the top.
    /// </summary>
    public static void ScrollToTop(this ScrollRect scrollRect)
    {
        scrollRect.normalizedPosition = new Vector2(0, 1);
    }

    /// <summary>
    /// Tell a scroll rect to go to the bottom.
    /// </summary>
    public static void ScrollToBottom(this ScrollRect scrollRect)
    {
        scrollRect.normalizedPosition = Vector2.zero;
    }

    /// <summary>
    /// Set the 'Left' field of the RectTransform.
    /// </summary>
    public static void SetLeft(this RectTransform rt, float left)
    {
        rt.offsetMin = rt.offsetMin.SetX(left);
    }

    /// <summary>
    /// Set the 'Right' field of the RectTransform.
    /// </summary>
    public static void SetRight(this RectTransform rt, float right)
    {
        rt.offsetMax = rt.offsetMax.SetX(-right);
    }

    /// <summary>
    /// Set the 'Top' field of the RectTransform.
    /// </summary>
    public static void SetTop(this RectTransform rt, float top)
    {
        rt.offsetMax = rt.offsetMax.SetY(-top);
    }

    /// <summary>
    /// Get the 'Top' field of the RectTransform.
    /// </summary>
    public static float GetTop(this RectTransform rt)
    {
        return rt.offsetMax.y;
    }

    /// <summary>
    /// Set the 'Bottom' field of the RectTransform.
    /// </summary>
    public static void SetBottom(this RectTransform rt, float bottom)
    {
        rt.offsetMin = rt.offsetMin.SetY(bottom);
    }

    /// <summary>
    /// Get the 'Bottom' field of the RectTransform.
    /// </summary>
    public static float GetBottom(this RectTransform rt)
    {
        return rt.offsetMin.y;
    }

    /// <summary>
    /// Set the x ordinate of the 'AnchorMin' field of the RectTransform.
    /// </summary>
    public static void SetAnchorLeft(this RectTransform rt, float left)
    {
        rt.anchorMin = rt.anchorMin.SetX(left);
    }

    /// <summary>
    /// Set the x ordinate of the 'AnchorMin' field of the RectTransform.
    /// </summary>
    public static void SetAnchorRight(this RectTransform rt, float right)
    {
        rt.anchorMax = rt.anchorMax.SetX(right);
    }


    /// <summary>
    /// Set the y ordinate of the 'AnchorMin' field of the RectTransform.
    /// </summary>
    public static void SetAnchorBottom(this RectTransform rt, float bottom)
    {
        rt.anchorMin = rt.anchorMin.SetY(bottom);
    }

    /// <summary>
    /// Set the y ordinate of the 'AnchorMax' field of the RectTransform.
    /// </summary>
    public static void SetAnchorTop(this RectTransform rt, float top)
    {
        rt.anchorMax = rt.anchorMax.SetY(top);
    }


    #endregion

    #region Strings, Text, and Parsing

    /// <summary>
    /// Gives this string rainbow colours!
    /// </summary>
    public static string ToRainbow(this string str)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < str.Length; i++)
            sb.Append(str[i].AddColour(RainbowLerp((float)i / (str.Length - 1))));

        return sb.ToString();
    }

    /// <summary>
    /// Alias for string.IsNullOrEmpty
    /// </summary>
    public static bool IsNullOrEmpty(this string str)
    {
        return string.IsNullOrEmpty(str);
    }

    /// <summary>
    /// Split "StringLikeThis" into "String Like This"
    /// </summary>
    public static string SplitCamelCase(this string input)
    {
        return Regex.Replace(input, "([a-z])([A-Z])", "$1 $2").Trim();
    }

    /// <summary>
    /// Title case means every word's first letter is capitalized.
    /// </summary>
    public static string ToTitleCase(this string s)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace('_', ' ').ToLowerInvariant());
    }

    /// <summary>
    /// Provides the number of words in this string.
    /// </summary>
    public static int GetWordCount(this string str)
    {
        return str.Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Removes everything in this string before the given string.
    /// 
    /// E.g. "The quick brown fox" + "quick" = "quick brown fox"
    /// </summary>
    public static string TrimBefore(this string str, string before)
    {
        return str.Substring(Mathf.Max(0, str.IndexOf(before)));
    }

    /// <summary>
    /// Returns everything in this string after the given string.
    /// 
    /// E.g. "The quick brown fox" + "quick" = " brown fox"
    /// </summary>
    public static string TrimToAfter(this string str, string after)
    {
        return str.Substring(Mathf.Clamp(str.IndexOf(after) + after.Length + 1, 0, str.Length));
    }

    /// <summary>
    /// If this string ends with the given string, remove the given string from the end.
    /// </summary>
    public static string TrimEnd(this string str, string end)
    {
        if (!str.EndsWith(end))
            return str;

        return str.Remove(str.Length - end.Length);
    }

    /// <summary>
    /// Remove any markup tags from this string.
    /// </summary>
    public static string RemoveMarkup(this string content)
    {
        return Regex.Replace(content, "<[^>]*>", string.Empty);
    }

    /// <summary>
    /// Remove any color tags from this string.
    /// </summary>=
    public static string RemoveColouredText(this string content)
    {
        return Regex.Replace(content, "<\\/*color.*>", string.Empty);
    }

    /// <summary>
    /// Ensure the string is a given length. Will pad with a custom character
    /// (default space) if under, or truncate if over.
    /// </summary>
    public static string FixLength(this string str, int length, char padder = ' ', bool addEllipsis = true)
    {
        length = Mathf.Max(0, length);

        if (str.Length > length)
        {
            return str.Truncate(length, addEllipsis);
        }
        else if (str.Length < length)
        {
            str += new string(padder, length - str.Length);
        }

        return str;
    }

    /// <summary>
    /// Truncate the given string a certain length, optionally can add ellipsis ("...") whose length
    /// will also come out of the total length.
    /// </summary>
    public static string Truncate(this string str, int length, bool addEllipsis = true)
    {
        length = Mathf.Max(0, length);

        if (str.Length > length)
        {
            str = str.Substring(0, length - (addEllipsis ? 3 : 0));

            if (addEllipsis)
                str += "...";
        }

        return str;
    }

    /// <summary>
    /// Remove all leading and trailing whitespace.
    /// </summary>
    public static string TrimWhitespace(this string s)
    {
        return new StringBuilder(s).TrimWhitespace().ToString();
    }

    /// <summary>
    /// Remove all leading and trailing whitespace.
    /// </summary>
    public static StringBuilder TrimWhitespace(this StringBuilder sb)
    {
        if (sb.Length == 0)
            return sb;

        // set [start] to first not-whitespace char or to sb.Length

        int start = 0;

        while (start < sb.Length)
        {
            if (char.IsWhiteSpace(sb[start]))
                start++;
            else
                break;
        }

        // if [sb] has only whitespaces, then return empty string

        if (start == sb.Length)
        {
            sb.Length = 0;
            return sb;
        }

        // set [end] to last not-whitespace char

        int end = sb.Length - 1;

        while (end >= 0)
        {
            if (char.IsWhiteSpace(sb[end]))
                end--;
            else
                break;
        }

        // compact string

        int dest = 0;
        bool previousIsWhitespace = false;

        for (int i = start; i <= end; i++)
        {
            if (char.IsWhiteSpace(sb[i]))
            {
                if (!previousIsWhitespace)
                {
                    previousIsWhitespace = true;
                    sb[dest] = ' ';
                    dest++;
                }
            }
            else
            {
                previousIsWhitespace = false;
                sb[dest] = sb[i];
                dest++;
            }
        }

        sb.Length = dest;

        return sb;
    }

    /// <summary>
    /// Add an RGB colour markcup code to the given string.
    /// </summary>
    public static string AddColour(this string content, Color colour)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">" + content + "</color>";
    }

    /// <summary>
    /// Add an RGB colour markcup code to the given string.
    /// </summary>
    public static string AddColour(this char content, Color colour)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">" + content + "</color>";
    }

    /// <summary>
    /// Keep this string below some character count, but preserve it along 
    /// some boundary. For example, if the delimiter is line breaks, preserve individual lines.
    /// 
    /// Prioritizes newer entries.
    /// </summary>
    public static string TrimByLines(this string str, int characterCount, string delimiter)
    {
        string[] lines = str.Split(delimiter.ToCharArray()).Reverse().ToArray();
        List<string> newLines = new List<string>(lines.Length);

        int runningTotal = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == delimiter)
                continue;

            if (runningTotal + lines[i].Length > characterCount)
                break;

            runningTotal += lines[i].Length;

            newLines.Add(lines[i]);
        }

        StringBuilder sb = new StringBuilder();

        for (int i = newLines.Count - 1; i >= 0; --i)
        {
            sb.Append(newLines[i]);

            if (i != 0)
                sb.Append(delimiter);
        }

        return sb.ToString();
    }


    #endregion

    #region Reflection

    /// <summary>
    /// Return all the methods in this type marked with the given attribute.
    /// </summary>
    public static IEnumerable<MethodInfo> GetMethodsWithAttribute(this Type classType, Type attributeType)
    {
        return classType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(methodInfo => methodInfo.GetCustomAttributes(attributeType, true).Length > 0);
    }

    #endregion

    #region Enums

    /// <summary>
    /// Will try to get the enum value matching the given string. (Case insensitive)
    /// </summary>
    public static bool TryGetFromString<T>(string str, out T result) where T : Enum
    {
        str = str.ToLowerInvariant();

        foreach (T t in Enum.GetValues(typeof(T)))
        {
            if (t.ToString().ToLowerInvariant().Equals(str))
            {
                result = t;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Gets all items for an enum type.
    /// </summary>
    public static IEnumerable<T> GetAll<T>() where T : Enum
    {
        foreach (T item in Enum.GetValues(typeof(T)))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Returns a random value of an enum.
    /// </summary>
    public static T GetRandomEnum<T>() where T : Enum
    {
        Array array = Enum.GetValues(typeof(T));
        return (T)array.GetValue(UnityEngine.Random.Range(0, array.Length));
    }

    /// <summary>
    /// Generic EqualityComparer for enums. Use this for faster lookups
    /// in enum-indexed dictionaries.
    /// </summary>
    public struct EnumEqualityComparer<T> : IEqualityComparer<T> where T : Enum
    {
        public bool Equals(T x, T y)
        {
            return Convert.ToInt32(x) == Convert.ToInt32(y);
        }

        public int GetHashCode(T obj)
        {
            return Convert.ToInt32(obj);
        }
    }


    #endregion

    #region Assets

#if UNITY_EDITOR
    /// <summary>
    /// Find all assets in the project of the given type.
    /// </summary>
    public static List<T> FindAssetsByType<T>() where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }
        return assets;
    }

    /// <summary>
    /// Automatically set this model importer's mask type to 'CreateFromThisModel' and set the mask
    /// to the entire hierarchy.
    /// </summary>
    public static bool CreateMaskFromThisModel(this ModelImporter modelImporter, GameObject model)
    {
        bool changed = false;

        ModelImporterClipAnimation[] clips = modelImporter.clipAnimations;

        if (clips == null || clips.Length == 0)
            clips = modelImporter.defaultClipAnimations;

        ModelImporterClipAnimation[] newAnims = new ModelImporterClipAnimation[clips.Length];

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].maskType != ClipAnimationMaskType.CreateFromThisModel)
            {
                newAnims[i] = clips[i];
                newAnims[i].maskType = ClipAnimationMaskType.CreateFromThisModel;

                changed = true;
            }
        }

        AvatarMask mask = new AvatarMask();
        mask.AddTransformPath(model.transform, true);

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].maskSource == null || clips[i].maskSource.transformCount != mask.transformCount)
            {
                clips[i].ConfigureClipFromMask(mask);
                changed = true;
            }
        }

        modelImporter.clipAnimations = clips;

        return changed;
    }
#endif

    #endregion
}