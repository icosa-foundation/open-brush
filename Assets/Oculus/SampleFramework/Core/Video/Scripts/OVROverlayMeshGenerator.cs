using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(OVROverlay))]
public class OVROverlayMeshGenerator : MonoBehaviour {

    private Mesh _Mesh;
    private List<Vector3> _Verts = new List<Vector3>();
    private List<Vector2> _UV = new List<Vector2>();
    private List<int> _Tris = new List<int>();
    private OVROverlay _Overlay;
    private MeshFilter _MeshFilter;
    private MeshCollider _MeshCollider;
    private Transform _CameraRoot;
    private Transform _Transform;

    private OVROverlay.OverlayShape _LastShape;
    private Vector3 _LastPosition;
    private Quaternion _LastRotation;
    private Vector3 _LastScale;
    private Rect _LastRectLeft;
    private Rect _LastRectRight;

    private bool _Awake = false;

    protected void Awake()
    {
        _Overlay = GetComponent<OVROverlay>();
        _MeshFilter = GetComponent<MeshFilter>();
        _MeshCollider = GetComponent<MeshCollider>();

        _Transform = transform;
        if (Camera.main && Camera.main.transform.parent)
        {
            _CameraRoot = Camera.main.transform.parent;
        }

        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update += Update;
        }
        #endif

        _Awake = true;
    }

    private Rect GetBoundingRect(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.x, b.x);
        float xMax = Mathf.Max(a.x + a.width, b.x + b.width);
        float yMin = Mathf.Min(a.y, b.y);
        float yMax = Mathf.Max(a.y + a.height, b.y + b.height);

        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private void Update()
    {
        if (!_Awake)
        {
            Awake();
        }

        if (_Overlay)
        {
            OVROverlay.OverlayShape shape = _Overlay.currentOverlayShape;
            Vector3 position = _CameraRoot ? (_Transform.position - _CameraRoot.position) : _Transform.position;
            Quaternion rotation = _Transform.rotation;
            Vector3 scale = _Transform.lossyScale;
            Rect rectLeft = _Overlay.overrideTextureRectMatrix ? _Overlay.destRectLeft : new Rect(0,0,1,1);
            Rect rectRight = _Overlay.overrideTextureRectMatrix ? _Overlay.destRectRight : new Rect(0,0,1,1);

            if (_Mesh == null || _LastShape != shape || _LastPosition != position || _LastRotation != rotation || _LastScale != scale || _LastRectLeft != rectLeft || _LastRectRight != rectRight)
            {
                UpdateMesh(shape, position, rotation, scale, GetBoundingRect(rectLeft, rectRight));
                _LastShape = shape;
                _LastPosition = position;
                _LastRotation = rotation;
                _LastScale = scale;
                _LastRectLeft = rectLeft;
                _LastRectRight = rectRight;
            }
        }
    }

    private void UpdateMesh(OVROverlay.OverlayShape shape, Vector3 position, Quaternion rotation, Vector3 scale, Rect rect)
    {
        if (_MeshFilter)
        {
            if (_Mesh == null)
            {
                _Mesh = new Mesh() { name = "Overlay" };
                _Mesh.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }
            _Mesh.Clear();
            _Verts.Clear();
            _UV.Clear();
            _Tris.Clear();

            GenerateMesh(_Verts, _UV, _Tris, shape, position, rotation, scale, rect);

            _Mesh.SetVertices(_Verts);
            _Mesh.SetUVs(0, _UV);
            _Mesh.SetTriangles(_Tris, 0);
            _Mesh.UploadMeshData(false);

            _MeshFilter.sharedMesh = _Mesh;

            if (_MeshCollider)
            {
                _MeshCollider.sharedMesh = _Mesh;
            }
        }
    }

	
    public static void GenerateMesh(List<Vector3> verts, List<Vector2> uvs, List<int> tris, OVROverlay.OverlayShape shape, Vector3 position, Quaternion rotation, Vector3 scale, Rect rect)
    {
        switch (shape)
        {
            case OVROverlay.OverlayShape.Equirect:
                BuildSphere(verts, uvs, tris, position, rotation, scale, rect);
                break;
            case OVROverlay.OverlayShape.Cubemap:
            case OVROverlay.OverlayShape.OffcenterCubemap:
                BuildCube(verts, uvs, tris, position, rotation, scale);
                break;
            case OVROverlay.OverlayShape.Quad:
                BuildQuad(verts, uvs, tris, rect);
                break;
            case OVROverlay.OverlayShape.Cylinder:
                BuildHemicylinder(verts, uvs, tris, scale, rect);
                break;
        }
    }

    private static Vector2 GetSphereUV(float theta, float phi, float expand_coef)
    {
        float thetaU = ((theta / (2 * Mathf.PI) - 0.5f) / expand_coef) + 0.5f;
        float phiV = ((phi / Mathf.PI) / expand_coef) + 0.5f;
        return new Vector2(thetaU, phiV);
    }

    private static Vector3 GetSphereVert(float theta, float phi)
    {
        return new Vector3(-Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(phi), -Mathf.Cos(theta) * Mathf.Cos(phi));
    }

    public static void BuildSphere(List<Vector3> verts, List<Vector2> uv, List<int> triangles, Vector3 position, Quaternion rotation, Vector3 scale, Rect rect, float worldScale = 800, int latitudes = 128, int longitudes = 128, float expand_coef = 1.0f)
    {
        position = Quaternion.Inverse(rotation) * position;

        latitudes = Mathf.CeilToInt(latitudes * rect.height);
        longitudes = Mathf.CeilToInt(longitudes * rect.width);

        float minTheta = Mathf.PI * 2 * ( rect.x);
        float minPhi = Mathf.PI * (0.5f - rect.y - rect.height);

        float thetaScale = Mathf.PI * 2 * rect.width / longitudes;
        float phiScale = Mathf.PI * rect.height / latitudes;

        for (int j = 0; j < latitudes + 1; j += 1)
        {
            for (int k = 0; k < longitudes + 1; k++)
            {
                float theta = minTheta + k * thetaScale;
                float phi = minPhi + j * phiScale;

                Vector2 suv = GetSphereUV(theta, phi, expand_coef);
                uv.Add(new Vector2((suv.x - rect.x) / rect.width, (suv.y - rect.y) / rect.height));
                Vector3 vert = GetSphereVert(theta, phi);
                vert.x = (worldScale * vert.x - position.x) / scale.x;
                vert.y = (worldScale * vert.y - position.y) / scale.y;
                vert.z = (worldScale * vert.z - position.z) / scale.z;
                verts.Add(vert);
            }
        }

        for (int j = 0; j < latitudes; j++)
        {
            for (int k = 0; k < longitudes; k++)
            {
                triangles.Add((j * (longitudes + 1)) + k);
                triangles.Add(((j + 1) * (longitudes + 1)) + k);
                triangles.Add(((j + 1) * (longitudes + 1)) + k + 1);
                triangles.Add(((j + 1) * (longitudes + 1)) + k + 1);
                triangles.Add((j * (longitudes + 1)) + k + 1);
                triangles.Add((j * (longitudes + 1)) + k);
            }
        }
    }

    private enum CubeFace
    {
        Right,
        Left,
        Top,
        Bottom,
        Front,
        Back,
        COUNT
    }

    private static readonly Vector3[] BottomLeft = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f)
        };

    private static readonly Vector3[] RightVector = new Vector3[]
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.left,
            Vector3.left,
            Vector3.right
        };

    private static readonly Vector3[] UpVector = new Vector3[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.forward,
            Vector3.back,
            Vector3.up,
            Vector3.up
        };

    private static Vector2 GetCubeUV(CubeFace face, Vector2 sideUV, float expand_coef)
    {
        sideUV = (sideUV - 0.5f * Vector2.one) / expand_coef + 0.5f * Vector2.one;
        switch (face)
        {
            case CubeFace.Bottom:
                return new Vector2(sideUV.x / 3, sideUV.y / 2);
            case CubeFace.Front:
                return new Vector2((1 + sideUV.x) / 3, sideUV.y / 2);
            case CubeFace.Back:
                return new Vector2((2 + sideUV.x) / 3, sideUV.y / 2);
            case CubeFace.Right:
                return new Vector2(sideUV.x / 3, (1 + sideUV.y) / 2);
            case CubeFace.Left:
                return new Vector2((1 + sideUV.x) / 3, (1 + sideUV.y) / 2);
            case CubeFace.Top:
                return new Vector2((2 + sideUV.x) / 3, (1 + sideUV.y) / 2);
            default:
                return Vector2.zero;
        }
    }

    private static Vector3 GetCubeVert(CubeFace face, Vector2 sideUV, float expand_coef)
    {
        return BottomLeft[(int)face] + sideUV.x * RightVector[(int)face] + sideUV.y * UpVector[(int)face];
    }

    public static void BuildCube(List<Vector3> verts, List<Vector2> uv, List<int> triangles, Vector3 position, Quaternion rotation, Vector3 scale, float worldScale = 800, int subQuads = 1, float expand_coef = 1.01f)
    {
        position = Quaternion.Inverse(rotation) * position;

        int vertsPerSide = (subQuads + 1) * (subQuads + 1);

        for (int i = 0; i < (int)CubeFace.COUNT; i++)
        {
            for(int j = 0; j < subQuads + 1; j++)
            {
                for(int k = 0; k < subQuads + 1; k++)
                {
                    float u = j / (float)subQuads;
                    float v = k / (float)subQuads;

                    uv.Add(GetCubeUV((CubeFace)i, new Vector2(u, v), expand_coef));
                    Vector3 vert = GetCubeVert((CubeFace)i, new Vector2(u, v), expand_coef);
                    vert.x = (worldScale * vert.x - position.x) / scale.x;
                    vert.y = (worldScale * vert.y - position.y) / scale.y;
                    vert.z = (worldScale * vert.z - position.z) / scale.z;
                    verts.Add(vert);
                }
            }

            for(int j = 0; j < subQuads; j++)
            {
                for(int k = 0; k < subQuads; k++)
                {
                    triangles.Add(vertsPerSide * i + ((j + 1) * (subQuads + 1)) + k);
                    triangles.Add(vertsPerSide * i + (j * (subQuads + 1)) + k);
                    triangles.Add(vertsPerSide * i + ((j + 1) * (subQuads + 1)) + k + 1);
                    triangles.Add(vertsPerSide * i + ((j + 1) * (subQuads + 1)) + k + 1);
                    triangles.Add(vertsPerSide * i + (j * (subQuads + 1)) + k);
                    triangles.Add(vertsPerSide * i + (j * (subQuads + 1)) + k + 1);
                }
            }
        }
    }


    public static void BuildQuad(List<Vector3> verts, List<Vector2> uv, List<int> triangles, Rect rect)
    {        
        verts.Add(new Vector3(rect.x - 0.5f, (1 - rect.y - rect.height) - 0.5f, 0));
        verts.Add(new Vector3(rect.x - 0.5f, (1 - rect.y) - 0.5f, 0));
        verts.Add(new Vector3(rect.x + rect.width - 0.5f, (1 - rect.y) - 0.5f, 0));
        verts.Add(new Vector3(rect.x + rect.width - 0.5f, (1 - rect.y - rect.height) - 0.5f, 0));

        uv.Add(new Vector2(0, 0));
        uv.Add(new Vector2(0, 1));
        uv.Add(new Vector2(1, 1));
        uv.Add(new Vector2(1, 0));

        triangles.Add(0);
        triangles.Add(1);
        triangles.Add(2);
        triangles.Add(2);
        triangles.Add(3);
        triangles.Add(0);
    }

    public static void BuildHemicylinder(List<Vector3> verts, List<Vector2> uv, List<int> triangles, Vector3 scale, Rect rect, int longitudes = 128)
    {
        float height = Mathf.Abs(scale.y) * rect.height;
        float radius = scale.z;
        float arcLength = scale.x * rect.width;

        float arcAngle = arcLength / radius;
        float minAngle = scale.x * (-0.5f + rect.x) / radius;
        
        int columns = Mathf.CeilToInt(longitudes * arcAngle / (2 * Mathf.PI));

        // we don't want super tall skinny triangles because that can lead to artifacting.
        // make triangles no more than 2x taller than wide

        float triangleWidth = arcLength / columns;
        float ratio = height / triangleWidth;

        int rows = Mathf.CeilToInt(ratio / 2);

        for (int j = 0; j < rows + 1; j += 1)
        {
            for (int k = 0; k < columns + 1; k++)
            {
                uv.Add(new Vector2((k / (float)columns), 1 - (j / (float)rows)));

                Vector3 vert = Vector3.zero;
                // because the scale is used to control the parameters, we need
                // to reverse multiply by scale to appear correctly
                vert.x = (Mathf.Sin(minAngle + (k * arcAngle / columns)) * radius) / scale.x;

                vert.y = (0.5f - rect.y - rect.height + rect.height * (1 - j / (float)rows));
                vert.z = (Mathf.Cos(minAngle + (k * arcAngle / columns)) * radius) / scale.z;
                verts.Add(vert);
            }
        }

        for (int j = 0; j < rows; j++)
        {
            for (int k = 0; k < columns; k++)
            {
                triangles.Add((j * (columns + 1)) + k);
                triangles.Add(((j + 1) * (columns + 1)) + k + 1);
                triangles.Add(((j + 1) * (columns + 1)) + k);
                triangles.Add(((j + 1) * (columns + 1)) + k + 1);
                triangles.Add((j * (columns + 1)) + k);
                triangles.Add((j * (columns + 1)) + k + 1);
            }
        }
    }
}
