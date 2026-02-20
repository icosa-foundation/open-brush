using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CameraDomeGizmo_runtime : MonoBehaviour
{
    public Transform target;
    public int numRings = 4;
    public int viewsPerRing = 20;
    public float radius = 5f;
    public float height = 1.5f;

    public Vector3 volumeCenter = Vector3.zero;
    public Vector3 volumeSize = new Vector3(5, 5, 5);
    public int subdivX = 2, subdivY = 2, subdivZ = 2;

    public bool showDome = true;
    public bool showVolume = false;

    private List<LineRenderer> domeLines = new List<LineRenderer>();
    private List<LineRenderer> volumeLines = new List<LineRenderer>();
    private CameraCaptureRuntime captureRuntime;

    private int lastNumRings, lastViewsPerRing;
    private float lastRadius, lastHeight;
    private Vector3 lastVolumeCenter, lastVolumeSize;
    private int lastSubdivX, lastSubdivY, lastSubdivZ;

    private void Start()
    {
        InitializeFromRuntime();
        GenerateVisuals();
        CacheParameters();
    }

    private void Update()
    {
        InitializeFromRuntime();

        if (target != null && domeLines.Count == 0 && showDome)
        {
            GenerateVisuals();
            CacheParameters();
        }

        if (ParametersChanged())
        {
            GenerateVisuals();
            CacheParameters();
        }
        else
        {
            UpdateVisuals();
        }
    }

    private void InitializeFromRuntime()
    {
        if (captureRuntime == null)
            captureRuntime = GetComponent<CameraCaptureRuntime>();

        if (captureRuntime != null)
        {
            target = captureRuntime.target;
            numRings = captureRuntime.numRings;
            viewsPerRing = captureRuntime.viewsPerRing;
            radius = captureRuntime.radius;
            height = captureRuntime.heightOffset;
            volumeCenter = captureRuntime.volumeCenter;
            volumeSize = captureRuntime.volumeSize;
            subdivX = captureRuntime.subdivX;
            subdivY = captureRuntime.subdivY;
            subdivZ = captureRuntime.subdivZ;
        }
    }

    private void CacheParameters()
    {
        lastNumRings = numRings;
        lastViewsPerRing = viewsPerRing;
        lastRadius = radius;
        lastHeight = height;
        lastVolumeCenter = volumeCenter;
        lastVolumeSize = volumeSize;
        lastSubdivX = subdivX;
        lastSubdivY = subdivY;
        lastSubdivZ = subdivZ;
    }

    private bool ParametersChanged()
    {
        return numRings != lastNumRings ||
               viewsPerRing != lastViewsPerRing ||
               radius != lastRadius ||
               height != lastHeight ||
               volumeCenter != lastVolumeCenter ||
               volumeSize != lastVolumeSize ||
               subdivX != lastSubdivX ||
               subdivY != lastSubdivY ||
               subdivZ != lastSubdivZ;
    }

    private void GenerateVisuals()
    {
        ClearLines(domeLines);
        ClearLines(volumeLines);

        if (showDome && target != null)
        {
            for (int ring = 0; ring < numRings; ring++)
            {
                float elevation = Mathf.Lerp(-Mathf.PI / 4, Mathf.PI / 4, (float)ring / Mathf.Max(1, numRings - 1));
                for (int i = 0; i < viewsPerRing; i++)
                {
                    float azimuth = i * Mathf.PI * 2 / viewsPerRing;
                    Vector3 position = ComputeDomePosition(elevation, azimuth);
                    Vector3 direction = (target.position - position).normalized;

                    var line = CreateLineRenderer(Color.cyan);
                    line.SetPositions(new Vector3[] { position, position + direction * 0.5f });
                    domeLines.Add(line);
                }
            }
        }

        if (showVolume)
        {
            Vector3 start = volumeCenter - volumeSize / 2f;
            Vector3 step = new Vector3(volumeSize.x / subdivX, volumeSize.y / subdivY, volumeSize.z / subdivZ);

            for (int x = 0; x <= subdivX; x++)
            {
                for (int y = 0; y <= subdivY; y++)
                {
                    for (int z = 0; z <= subdivZ; z++)
                    {
                        Vector3 cellCenter = start + new Vector3(x * step.x, y * step.y, z * step.z);
                        foreach (Vector3 dir in GenerateCustomSphericalDirections())
                        {
                            var line = CreateLineRenderer(Color.blue);
                            line.SetPositions(new Vector3[] { cellCenter, cellCenter + dir.normalized * 0.2f });
                            volumeLines.Add(line);
                        }
                    }
                }
            }
        }
    }

    private void UpdateVisuals()
    {
        int index = 0;
        if (showDome && target != null)
        {
            for (int ring = 0; ring < numRings; ring++)
            {
                float elevation = Mathf.Lerp(-Mathf.PI / 4, Mathf.PI / 4, (float)ring / Mathf.Max(1, numRings - 1));
                for (int i = 0; i < viewsPerRing; i++)
                {
                    if (index >= domeLines.Count) return;

                    float azimuth = i * Mathf.PI * 2 / viewsPerRing;
                    Vector3 position = ComputeDomePosition(elevation, azimuth);
                    Vector3 direction = (target.position - position).normalized;

                    domeLines[index].SetPositions(new Vector3[] { position, position + direction * 0.5f });
                    index++;
                }
            }
        }
    }

    private Vector3 ComputeDomePosition(float elevation, float azimuth)
    {
        float x = radius * Mathf.Cos(elevation) * Mathf.Cos(azimuth);
        float y = radius * Mathf.Sin(elevation);
        float z = radius * Mathf.Cos(elevation) * Mathf.Sin(azimuth);
        return target.position + new Vector3(x, y + height, z);
    }

    private LineRenderer CreateLineRenderer(Color color)
    {
        GameObject go = new GameObject("LineRenderer");
        go.transform.parent = transform;
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.useWorldSpace = true;
        return lr;
    }

    private void ClearLines(List<LineRenderer> lines)
    {
        foreach (var line in lines)
        {
            if (line != null)
                DestroyImmediate(line.gameObject);
        }
        lines.Clear();
    }

    private List<Vector3> GenerateCustomSphericalDirections()
    {
        List<Vector3> directions = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            float azimuth = i * 45f;
            Quaternion rot = Quaternion.Euler(0f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }
        for (int i = 0; i < 4; i++)
        {
            float azimuth = i * 90f;
            Quaternion rot = Quaternion.Euler(45f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }
        for (int i = 0; i < 4; i++)
        {
            float azimuth = i * 90f;
            Quaternion rot = Quaternion.Euler(-45f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }
        directions.Add(Vector3.up);
        directions.Add(Vector3.down);
        return directions;
    }
}
