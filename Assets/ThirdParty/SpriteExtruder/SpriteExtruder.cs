using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///
/// </summary>
/// <remarks>Source: https://forum.unity3d.com/threads/trying-extrude-a-2d-polygon-to-create-a-mesh.102629/ </remarks>
public class SpriteExtruder : MonoBehaviour
{
    public Color extrudeColor = Color.white;
    public float frontDistance = -0.249f;
    public float backDistance = 0.249f;

    private Texture tex;

    public void AssignSprite(Sprite sprite)
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        sr.sprite = sprite;
        sr.enabled = true;
        int width = Mathf.RoundToInt(sprite.bounds.size.x * sprite.pixelsPerUnit);
        int height = Mathf.RoundToInt(sprite.bounds.size.y * sprite.pixelsPerUnit);
        tex = new Texture2D(width, height);
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        float ppu = 100;
        Vector2 scale = new(ppu / tex.width, ppu / tex.height);

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr.sprite == null)
        {
            var sprite = Sprite.Create(
                (Texture2D)tex,
                new Rect(0.0f, 0.0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.Tight,
                Vector4.zero,
                true
            );
            sr.enabled = true;
            sr.sprite = sprite;
        }
        var pol = sr.GetComponent<PolygonCollider2D>();
        if (pol != null)
        {
            Destroy(pol);
        }
        pol = sr.gameObject.AddComponent<PolygonCollider2D>();

        var paths = new Vector2[pol.pathCount][];
        for (int i = 0; i < pol.pathCount; i++)
        {
            paths[i] = pol.GetPath(i);
        }
        Mesh m = CreateMesh(paths.ToArray(), scale, frontDistance, backDistance);

        GetComponent<MeshFilter>().sharedMesh = m;
        GetComponent<MeshRenderer>().material.color = extrudeColor;

        pol.isTrigger = true;
        pol.enabled = false;
        sr.enabled = false;
    }

    private static Mesh CreateMesh(Vector2[][] paths, Vector2 scale, float frontDistance = -10, float backDistance = 10)
    {
        frontDistance = Mathf.Min(frontDistance, 0);
        backDistance = Mathf.Max(backDistance, 0);

        var allVertices = new List<Vector3>();
        var allTriangles = new List<int>();

        for (var pathIndex = 0; pathIndex < paths.Length; pathIndex++)
        {
            Vector2[] path = paths[pathIndex];

            // convert polygon to triangles
            Triangulator triangulator = new Triangulator(path);
            int[] tris = triangulator.Triangulate();

            Vector3[] vertices = new Vector3[path.Length * 2];
            var triangles = new List<int>(tris.Length * 2);

            for (int i = 0; i < path.Length; i++)
            {
                path[i].Scale(scale);
                vertices[i].x = path[i].x;
                vertices[i].y = path[i].y;
                vertices[i].z = frontDistance; // front vertex
                vertices[i + path.Length].x = path[i].x;
                vertices[i + path.Length].y = path[i].y;
                vertices[i + path.Length].z = backDistance; // back vertex
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                triangles.Add(tris[i]);
                triangles.Add(tris[i + 1]);
                triangles.Add(tris[i + 2]);
            } // front vertices

            for (int i = 0; i < tris.Length; i += 3)
            {
                triangles.Add(tris[i + 2] + path.Length);
                triangles.Add(tris[i + 1] + path.Length);
                triangles.Add(tris[i] + path.Length);
            } // back vertices

            int count = allVertices.Count;
            allTriangles.AddRange(triangles.Select(i => i + count).ToList());
            allVertices.AddRange(vertices);

            var sideVertices = new Vector3[path.Length * 4];
            triangles = new List<int>(path.Length * 6);

            int j = 0;
            // triangles around the perimeter of the object
            for (int i = 0; i < path.Length * 4; i += 4)
            {
                // Copy the front and back vertices
                sideVertices[i] = vertices[j];
                sideVertices[i + 1] = vertices[(j + 1) % path.Length];
                sideVertices[i + 2] = vertices[j + path.Length];
                sideVertices[i + 3] = vertices[(j + 1) % path.Length + path.Length];
                j++;

                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 2);

                triangles.Add(i + 3);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
            }

            count = allVertices.Count;
            allTriangles.AddRange(triangles.Select(i => i + count).ToList());
            allVertices.AddRange(sideVertices);
        }

        Mesh mesh = new Mesh
        {
            vertices = allVertices.ToArray(),
            triangles = allTriangles.ToArray()
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();
        return mesh;
    }

    public void Clear()
    {
        GetComponentInChildren<SpriteRenderer>().sprite = null;
        GetComponent<MeshFilter>().sharedMesh = null;
    }
}

/// <summary>
///
/// </summary>
/// <remarks>Source: http://wiki.unity3d.com/index.php?title=Triangulator </remarks>
public class Triangulator
{
    private readonly List<Vector2> m_points;

    public Triangulator(Vector2[] points)
    {
        m_points = new List<Vector2>(points);
    }

    public int[] Triangulate()
    {
        List<int> indices = new List<int>();

        int n = m_points.Count;
        if (n < 3)
            return indices.ToArray();

        int[] V = new int[n];
        if (Area() > 0)
        {
            for (int v = 0; v < n; v++)
                V[v] = v;
        }
        else
        {
            for (int v = 0; v < n; v++)
                V[v] = (n - 1) - v;
        }

        int nv = n;
        int count = 2 * nv;
        for (int v = nv - 1; nv > 2;)
        {
            if (count-- <= 0)
                return indices.ToArray();

            int u = v;
            if (nv <= u)
                u = 0;
            v = u + 1;
            if (nv <= v)
                v = 0;
            int w = v + 1;
            if (nv <= w)
                w = 0;

            if (Snip(u, v, w, nv, V))
            {
                int a = V[u];
                int b = V[v];
                int c = V[w];
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                int s, t;
                for (s = v, t = v + 1; t < nv; s++, t++)
                    V[s] = V[t];
                nv--;
                count = 2 * nv;
            }
        }

        indices.Reverse();
        return indices.ToArray();
    }

    private float Area()
    {
        int n = m_points.Count;
        float A = 0.0f;
        for (int p = n - 1, q = 0; q < n; p = q++)
        {
            Vector2 pval = m_points[p];
            Vector2 qval = m_points[q];
            A += pval.x * qval.y - qval.x * pval.y;
        }
        return (A * 0.5f);
    }

    private bool Snip(int u, int v, int w, int n, int[] V)
    {
        int p;
        Vector2 A = m_points[V[u]];
        Vector2 B = m_points[V[v]];
        Vector2 C = m_points[V[w]];
        if (Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))))
            return false;
        for (p = 0; p < n; p++)
        {
            if ((p == u) || (p == v) || (p == w))
                continue;
            Vector2 P = m_points[V[p]];
            if (InsideTriangle(A, B, C, P))
                return false;
        }
        return true;
    }

    private bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax = C.x - B.x;
        float ay = C.y - B.y;
        float bx = A.x - C.x;
        float by = A.y - C.y;
        float cx = B.x - A.x;
        float cy = B.y - A.y;
        float apx = P.x - A.x;
        float apy = P.y - A.y;
        float bpx = P.x - B.x;
        float bpy = P.y - B.y;
        float cpx = P.x - C.x;
        float cpy = P.y - C.y;

        float aCROSSbp = ax * bpy - ay * bpx;
        float cCROSSap = cx * apy - cy * apx;
        float bCROSScp = bx * cpy - by * cpx;

        return aCROSSbp >= 0.0f && bCROSScp >= 0.0f && cCROSSap >= 0.0f;
    }
}
