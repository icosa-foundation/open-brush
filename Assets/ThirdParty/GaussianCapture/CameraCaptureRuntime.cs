
using UnityEngine;
using System;
using System.IO;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TiltBrush;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public class CameraCaptureRuntime : MonoBehaviour
{
    [Header("Common")]
    public Camera cameraToUse;
    public int width = 1920;
    public int height = 1080;
    [Tooltip("Samples per image for point cloud (~sqrt grid)")]
    public int raysPerView = 500;
    [Tooltip("Root output folder. If empty, defaults to Application.persistentDataPath + /Output")]
    public string outputFolder = "";

    [Header("Runtime Sequence (optional)")]
    public bool runtimeSequence = false;
    [Tooltip("Frames per second for runtime sequence")]
    public int fbs = 25;
    [Tooltip("Duration in seconds for runtime sequence")]
    public float duration = 1f;

    [Header("PostShot (optional)")]
    public bool trainPostShot = false;
    [Tooltip("Full path to postshot-cli executable (Standalone only)")]
    public string postShotCliPath = @"C:\Program Files\Jawset Postshot\bin\postshot-cli.exe";
    [Range(1, 50)] public int trainSteps = 5;
    public OutputFormat outputFormat = OutputFormat.PSHT;
    public TrainingProfile profile = TrainingProfile.Splat3;

    [Header("Dome Capture")]
    public Transform target;
    public Shader eyeDepthShader;
    public int numRings = 4;
    public int viewsPerRing = 20;
    public float radius = 5f;
    public float heightOffset = 1.5f;
    private SphereStencil m_SphereWidget;

    [Header("Volume Capture")]
    public Vector3 volumeCenter = Vector3.zero;
    public Vector3 volumeSize = new Vector3(5, 5, 5);
    public int subdivX = 2, subdivY = 2, subdivZ = 2;
    private CubeStencil m_CubeWidget;

    [Header("Transparents/Particles depth")]
    public bool includeTransparentsAndParticles = true;
    [Range(0f, 1f)] public float alphaThreshold = 0.05f;
    [Tooltip("Replacement shader qui �crit la profondeur en R, avec alpha-clip.")]
    public Shader depthReplacementShader;

    public Action<float, string> OnProgress;

    private bool isRunning = false;
    private bool cancel = false;
    private Material _eyeDepthMat;

    public enum OutputFormat { PSHT, PLY }
    public enum TrainingProfile { Splat3, MCMC, ADC }

    [ContextMenu("Start Dome Capture")]
    public void StartDomeCapture()
    {
        if (!ValidateCommon(domeMode: true)) return;
        this.radius = m_SphereWidget.Extents.x;
        this.heightOffset = 0;
        if (runtimeSequence)
            StartCoroutine(RuntimeSequenceCoroutine(isDome: true));
        else
            StartCoroutine(CaptureViewsAndExportColmap(outAdd: ""));
    }

    [ContextMenu("Start Volume Capture")]
    public void StartVolumeCapture()
    {
        if (!ValidateCommon(domeMode: false)) return;
        this.volumeCenter = m_CubeWidget.transform.position;
        this.volumeSize = m_CubeWidget.Extents;

        if (runtimeSequence)
            StartCoroutine(RuntimeSequenceCoroutine(isDome: false));
        else
            StartCoroutine(CaptureVolumeViewsAndExportColmap(outAdd: ""));
    }

    [ContextMenu("Cancel")]
    public void Cancel()
    {
        if (isRunning)
        {
            cancel = true;
            Debug.LogWarning("[Capture] Cancel requested.");
        }
    }

    private bool ValidateCommon(bool domeMode)
    {
        if (cameraToUse == null)
        {
            Debug.LogError("Assign a Camera to use.");
            return false;
        }
        if (string.IsNullOrEmpty(outputFolder))
        {
            outputFolder = Path.Combine(Application.persistentDataPath, "Output");
        }
        if (domeMode && target == null)
        {
            Debug.LogError("Assign a Target transform for Dome capture.");
            return false;
        }
        if (width <= 0 || height <= 0)
        {
            Debug.LogError("Invalid Width/Height.");
            return false;
        }
        if (eyeDepthShader == null && !(includeTransparentsAndParticles && depthReplacementShader != null))
        {
            Debug.LogWarning("Eye-Depth Shader is not set. Depth-based point cloud export will fail for opaque path.");
        }
        return true;
    }

    private IEnumerator RuntimeSequenceCoroutine(bool isDome)
    {
        int totalFrames = Mathf.Max(1, Mathf.RoundToInt(duration * Mathf.Max(1, fbs)));
        isRunning = true;
        float frameDt = 1f / Mathf.Max(1, fbs);

        for (int i = 0; i < totalFrames; i++)
        {
            if (cancel) break;
            string outAdd = "/" + i + "/";
            if (isDome)
                yield return StartCoroutine(CaptureViewsAndExportColmap(outAdd));
            else
                yield return StartCoroutine(CaptureVolumeViewsAndExportColmap(outAdd));
            yield return new WaitForSeconds(frameDt);
        }

        if (trainPostShot && !cancel)
        {
            TryRunPostshotBatch();
        }
        isRunning = false;
        cancel = false;
        ReportProgress(1f, "Runtime sequence finished");
    }

    public IEnumerator CaptureViewsAndExportColmap(string outAdd)
    {
        isRunning = true;
        string folderPath = PathCombineSafe(outputFolder, outAdd);
        Directory.CreateDirectory(folderPath);

        string camerasTxt = Path.Combine(folderPath, "cameras.txt");
        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * height / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;
        float cx = width / 2f;
        float cy = height / 2f;
        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("# CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {width} {height} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        string imagesTxt = Path.Combine(folderPath, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            RenderTexture rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGBFloat);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            int imageId = 1;
            int batchSize = 40;
            int batchCounter = 0;
            int totalImages = viewsPerRing * Mathf.Max(1, numRings);
            int currentImage = 0;

            using (StreamWriter writer3D = new StreamWriter(Path.Combine(folderPath, "points3D.txt")))
            {
                writer3D.WriteLine("# 3D point list with one line of data per point:");
                writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
                int pointId = 1;

                BakeSkinnedMeshColliders();

                for (int ring = 0; ring < numRings; ring++)
                {
                    float elevation = Mathf.Lerp(-Mathf.PI / 4f, Mathf.PI / 4f, (numRings == 1) ? 0.5f : (float)ring / (numRings - 1));
                    for (int i = 0; i < viewsPerRing; i++)
                    {
                        if (cancel) { CleanupRT(ref cameraToUse, ref rt, ref tex); isRunning = false; yield break; }

                        float progress = (float)currentImage / Mathf.Max(1, totalImages);
                        ReportProgress(progress, $"Dome Capture {currentImage + 1}/{totalImages}");

                        float azimuth = i * Mathf.PI * 2f / viewsPerRing;
                        float x = radius * Mathf.Cos(elevation) * Mathf.Cos(azimuth);
                        float y = radius * Mathf.Sin(elevation);
                        float z = radius * Mathf.Cos(elevation) * Mathf.Sin(azimuth);
                        Vector3 position = target.position + new Vector3(x, y + heightOffset, z);
                        cameraToUse.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target.position - position, Vector3.up));

                        Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                        Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                        Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;
                        Matrix4x4 R = colmapMatrix;
                        R.SetColumn(3, new Vector4(0, 0, 0, 1));
                        Quaternion q = QuaternionFromMatrix(R);
                        Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                        string imageName = $"view_{imageId:D3}.png";
                        string imagePath = Path.Combine(folderPath, imageName);
                        cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                        cameraToUse.backgroundColor = new Color(0, 0, 0, 0);
                        cameraToUse.targetTexture = rt;
                        cameraToUse.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();

                        CapturePointCloudFromCamera(cameraToUse, tex, raysPerView, writer3D, imageId, ref pointId);

                        File.WriteAllBytes(imagePath, tex.EncodeToPNG());
                        imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                        imgWriter.WriteLine();

                        imageId++;
                        batchCounter++;
                        currentImage++;

                        if (batchCounter >= batchSize)
                        {
                            BatchMemoryStep(ref rt, ref tex);
                            yield return null;
                        }
                    }
                }
            }

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            SafeDestroy(rt);
            SafeDestroy(tex);
        }

        Debug.Log("Dome capture + COLMAP files finished!");
        isRunning = false;

        if (!runtimeSequence && trainPostShot && !cancel)
            TryRunPostshotBatch();

        yield return new WaitForEndOfFrame();
    }

    public IEnumerator CaptureVolumeViewsAndExportColmap(string outAdd)
    {
        isRunning = true;
        string folderPath = PathCombineSafe(outputFolder, outAdd);
        Directory.CreateDirectory(folderPath);

        string camerasTxt = Path.Combine(folderPath, "cameras.txt");
        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * height / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;
        float cx = width / 2f;
        float cy = height / 2f;
        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("# CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {width} {height} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        string imagesTxt = Path.Combine(folderPath, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            RenderTexture rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGBFloat);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            int imageId = 1;
            Vector3 step = new Vector3(
                (subdivX > 0 ? volumeSize.x / subdivX : volumeSize.x),
                (subdivY > 0 ? volumeSize.y / subdivY : volumeSize.y),
                (subdivZ > 0 ? volumeSize.z / subdivZ : volumeSize.z));
            List<Vector3> directions = GenerateCustomSphericalDirections();
            int totalImages = Mathf.Max(1, subdivX * subdivY * subdivZ * directions.Count);
            int currentImage = 0;
            int batchSize = 40;
            int batchCounter = 0;
            int imagesSkipped = 0;

            using (StreamWriter writer3D = new StreamWriter(Path.Combine(folderPath, "points3D.txt")))
            {
                writer3D.WriteLine("# 3D point list with one line of data per point:");
                writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
                int pointId = 1;

                BakeSkinnedMeshColliders();

                for (int ix = 0; ix < Mathf.Max(1, subdivX); ix++)
                {
                    for (int iy = 0; iy < Mathf.Max(1, subdivY); iy++)
                    {
                        for (int iz = 0; iz < Mathf.Max(1, subdivZ); iz++)
                        {
                            Vector3 cellCenter = volumeCenter - volumeSize / 2f + step * 0.5f
                                + new Vector3(ix * step.x, iy * step.y, iz * step.z);

                            foreach (Vector3 dir in directions)
                            {
                                if (cancel) { CleanupRT(ref cameraToUse, ref rt, ref tex); isRunning = false; yield break; }

                                float progress = (float)currentImage / totalImages;
                                ReportProgress(progress, $"Volume Capture {currentImage + 1}/{totalImages} Skipped:{imagesSkipped}");

                                cameraToUse.transform.SetPositionAndRotation(cellCenter, Quaternion.LookRotation(dir, Vector3.up));

                                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cameraToUse);
                                bool objectVisible = false;
                                foreach (Renderer renderer in GameObject.FindObjectsOfType<Renderer>())
                                {
                                    if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                                    {
                                        objectVisible = true;
                                        break;
                                    }
                                }
                                if (!objectVisible)
                                {
                                    imagesSkipped++;
                                    currentImage++;
                                    continue;
                                }

                                Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                                Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                                Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;
                                Matrix4x4 R = colmapMatrix;
                                R.SetColumn(3, new Vector4(0, 0, 0, 1));
                                Quaternion q = QuaternionFromMatrix(R);
                                Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                                string imageName = $"vol_{imageId:D4}.png";
                                string imagePath = Path.Combine(folderPath, imageName);

                                cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                                cameraToUse.backgroundColor = new Color(0, 0, 0, 0);
                                cameraToUse.targetTexture = rt;
                                cameraToUse.Render();
                                RenderTexture.active = rt;
                                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                                tex.Apply();

                                CapturePointCloudFromCamera(cameraToUse, tex, raysPerView, writer3D, imageId, ref pointId);

                                byte[] pngData = tex.EncodeToPNG();
                                File.WriteAllBytes(imagePath, pngData);
                                pngData = null;

                                imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                                imgWriter.WriteLine();

                                imageId++;
                                currentImage++;
                                batchCounter++;

                                if (batchCounter >= batchSize)
                                {
                                    BatchMemoryStep(ref rt, ref tex);
                                    yield return null;
                                }
                            }
                        }
                    }
                }
            }

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            SafeDestroy(rt);
            SafeDestroy(tex);
        }

        Debug.Log("Volume capture + COLMAP files finished!");
        isRunning = false;

        if (!runtimeSequence && trainPostShot && !cancel)
            TryRunPostshotBatch();

        yield return new WaitForEndOfFrame();
    }

    private void BakeSkinnedMeshColliders()
    {
        foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
        {
            var col = r.GetComponent<MeshCollider>();
            if (col == null) col = r.gameObject.AddComponent<MeshCollider>();
            Mesh baked = new Mesh();
            r.BakeMesh(baked);
            col.sharedMesh = null;
            col.sharedMesh = baked;
        }
    }

    private void BatchMemoryStep(ref RenderTexture rt, ref Texture2D tex)
    {
        cameraToUse.targetTexture = null;
        RenderTexture.active = null;
        GL.Clear(true, true, Color.clear);
        SafeDestroy(rt);
        SafeDestroy(tex);
        Resources.UnloadUnusedAssets();
        GC.Collect();
        rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGBFloat);
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
    }

    private void CleanupRT(ref Camera cam, ref RenderTexture rt, ref Texture2D tex)
    {
        cam.targetTexture = null;
        RenderTexture.active = null;
        SafeDestroy(rt);
        SafeDestroy(tex);
        ReportProgress(0f, "Capture canceled.");
        cancel = false;
    }

    private void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private string PathCombineSafe(string root, string add)
    {
        if (string.IsNullOrEmpty(add)) return root;
        add = add.Replace("\\", "/");
        if (add.StartsWith("/")) add = add.Substring(1);
        return Path.Combine(root, add);
    }

    private void ReportProgress(float pct, string msg)
    {
        OnProgress?.Invoke(Mathf.Clamp01(pct), msg);
    }

    private static Quaternion QuaternionFromMatrix(Matrix4x4 m)
    {
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
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

    private Texture2D RenderDepthIncludingTransparents(Camera srcCam, int w, int h)
    {
        if (depthReplacementShader == null)
        {
            Debug.LogError("[Capture] depthReplacementShader manquant.");
            return null;
        }

        var go = new GameObject("TempDepthCam");
        go.hideFlags = HideFlags.HideAndDontSave;
        var depthCam = go.AddComponent<Camera>();
        depthCam.CopyFrom(srcCam);
        depthCam.allowHDR = false;
        depthCam.allowMSAA = false;
        depthCam.clearFlags = CameraClearFlags.SolidColor;
        depthCam.backgroundColor = new Color(srcCam.farClipPlane, 0, 0, 1);
        depthCam.cullingMask = srcCam.cullingMask;

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        depthCam.targetTexture = rt;

        Shader.SetGlobalFloat("_CaptureFar", srcCam.farClipPlane);
        Shader.SetGlobalFloat("_AlphaThreshold", alphaThreshold);

        depthCam.RenderWithShader(depthReplacementShader, null);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var depthTex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
        depthTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        depthTex.Apply(false, false);
        RenderTexture.active = prev;

        RenderTexture.ReleaseTemporary(rt);
        Destroy(go);

        return depthTex;
    }

    private void AppendVisibleParticlesToPointCloud(StreamWriter writer, Camera cam, int imageId, ref int pointId, float minAlpha = 0.05f)
    {
        var systems = GameObject.FindObjectsOfType<ParticleSystem>();
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);

        foreach (var ps in systems)
        {
            if (!ps.IsAlive()) continue;

            var main = ps.main;
            int max = main.maxParticles > 0 ? main.maxParticles : 10000;
            var buffer = new ParticleSystem.Particle[max];
            int count = ps.GetParticles(buffer);

            for (int i = 0; i < count; i++)
            {
                Vector3 wp = ps.transform.TransformPoint(buffer[i].position);

                if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(wp, Vector3.one * 0.05f)))
                    continue;

                Color32 c = buffer[i].GetCurrentColor(ps);
                if (c.a / 255f < minAlpha) continue;

                float x = -wp.x;
                float y = wp.y;
                float z = wp.z;

                writer.WriteLine(
                    $"{pointId} " +
                    $"{x.ToString(CultureInfo.InvariantCulture)} " +
                    $"{y.ToString(CultureInfo.InvariantCulture)} " +
                    $"{z.ToString(CultureInfo.InvariantCulture)} " +
                    $"{c.r} {c.g} {c.b} 1.0"
                );
                pointId++;
            }
        }
    }

    void CapturePointCloudFromCamera(
        Camera cam,
        Texture2D colorTex,
        int rayCount,
        StreamWriter writer,
        int imageId,
        ref int pointId,
        bool invertX = true,
        bool useScreenToWorldPoint = true,
        bool skipMaxDepthPlane = true,
        float maxDepthEpsilon = 0.01f,
        float clampMinMeters = 0f,
        float clampMaxMeters = 0f,
        bool useFloat32 = false
    )
    {
        if (cam == null || colorTex == null || writer == null) return;

        cam.depthTextureMode = DepthTextureMode.Depth;

        if (_eyeDepthMat == null)
        {
            var sh = eyeDepthShader;
            if (sh == null && !(includeTransparentsAndParticles && depthReplacementShader != null))
            {
                Debug.LogError("[PointCloudExporter] Missing eyeDepth Shader and no replacement depth set. Aborting point sampling.");
                return;
            }
            if (sh != null)
                _eyeDepthMat = new Material(sh) { hideFlags = HideFlags.DontSave };
        }

        int w = colorTex.width;
        int h = colorTex.height;

        Texture2D depthTex = null;
        RenderTexture rtDepth = null;

        int oldMask = cam.cullingMask;
        int noCloudLayer = LayerMask.NameToLayer("NoCloud");
        if (noCloudLayer >= 0) cam.cullingMask = oldMask & ~(1 << noCloudLayer);

        try
        {
            if (includeTransparentsAndParticles && depthReplacementShader != null)
            {
                depthTex = RenderDepthIncludingTransparents(cam, w, h);
                if (depthTex == null) return;
            }
            else
            {
                var rtFormat = useFloat32 ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf;
                rtDepth = RenderTexture.GetTemporary(w, h, 0, rtFormat, RenderTextureReadWrite.Linear);
                rtDepth.filterMode = FilterMode.Point;

                _eyeDepthMat.SetFloat("_MinMeters", (clampMaxMeters > clampMinMeters) ? clampMinMeters : 0f);
                _eyeDepthMat.SetFloat("_MaxMeters", (clampMaxMeters > clampMinMeters) ? clampMaxMeters : 0f);

                Graphics.Blit(Texture2D.blackTexture, rtDepth, _eyeDepthMat);

                var prev = RenderTexture.active;
                RenderTexture.active = rtDepth;
                var texFormat = useFloat32 ? TextureFormat.RGBAFloat : TextureFormat.RGBAHalf;
                depthTex = new Texture2D(w, h, texFormat, false, true);
                depthTex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                depthTex.Apply(false, false);
                RenderTexture.active = prev;
            }

            int sqrtRayCount = Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, rayCount)));
            float stepX = w / (float)sqrtRayCount;
            float stepY = h / (float)sqrtRayCount;

            float skipThreshold = -1f;
            if (skipMaxDepthPlane)
            {
                Color[] dpx = depthTex.GetPixels();
                float measuredMaxDepth = 0f;
                for (int i = 0; i < dpx.Length; i++)
                {
                    float v = dpx[i].r;
                    if (v <= 0f || float.IsNaN(v) || float.IsInfinity(v)) continue;
                    if (v > measuredMaxDepth) measuredMaxDepth = v;
                }
                float refMax = (clampMaxMeters > clampMinMeters && clampMaxMeters > 0f)
                    ? clampMaxMeters
                    : cam.farClipPlane;
                if (measuredMaxDepth > 0f) measuredMaxDepth = Mathf.Min(measuredMaxDepth, refMax);
                else measuredMaxDepth = refMax;
                skipThreshold = Mathf.Max(0f, measuredMaxDepth - Mathf.Max(1e-6f, maxDepthEpsilon));
            }

            for (int i = 0; i < sqrtRayCount; i++)
            {
                for (int j = 0; j < sqrtRayCount; j++)
                {
                    float px = i * stepX + stepX * 0.5f;
                    float py = j * stepY + stepY * 0.5f;

                    float d = depthTex.GetPixel(
                        Mathf.Clamp((int)px, 0, w - 1),
                        Mathf.Clamp((int)py, 0, h - 1)).r;

                    if (d <= 0f || float.IsNaN(d) || float.IsInfinity(d)) continue;
                    if (skipMaxDepthPlane && d >= skipThreshold) continue;

                    Vector3 worldPos;
                    if (useScreenToWorldPoint)
                    {
                        worldPos = cam.ScreenToWorldPoint(new Vector3(px + 0.5f, py + 0.5f, d));
                    }
                    else
                    {
                        var ray = cam.ScreenPointToRay(new Vector3(px + 0.5f, py + 0.5f, 0f));
                        float cos = Vector3.Dot(ray.direction, cam.transform.forward);
                        float t = d / Mathf.Max(1e-6f, cos);
                        worldPos = ray.origin + ray.direction * t;
                    }

                    if (invertX) worldPos.x = -worldPos.x;

                    Color color = colorTex.GetPixel(
                        Mathf.Clamp((int)px, 0, w - 1),
                        Mathf.Clamp((int)py, 0, h - 1));
                    int r = Mathf.Clamp((int)(color.r * 255f), 0, 255);
                    int g = Mathf.Clamp((int)(color.g * 255f), 0, 255);
                    int b = Mathf.Clamp((int)(color.b * 255f), 0, 255);

                    writer.WriteLine(
                        $"{pointId} " +
                        $"{worldPos.x.ToString(CultureInfo.InvariantCulture)} " +
                        $"{worldPos.y.ToString(CultureInfo.InvariantCulture)} " +
                        $"{worldPos.z.ToString(CultureInfo.InvariantCulture)} " +
                        $"{r} {g} {b} 1.0"
                    );
                    pointId++;
                }
            }

            if (includeTransparentsAndParticles)
            {
                AppendVisibleParticlesToPointCloud(writer, cam, imageId, ref pointId);
            }

            SafeDestroy(depthTex);
        }
        finally
        {
            cam.cullingMask = oldMask;
            if (rtDepth != null) RenderTexture.ReleaseTemporary(rtDepth);
        }
    }

    private void TryRunPostshotBatch()
    {
#if UNITY_EDITOR
        
#endif
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
        {
            Debug.LogError("Output folder does not exist for PostShot training.");
            return;
        }

        var subDirs = Directory.GetDirectories(outputFolder);
        List<string> foldersToProcess = subDirs.Length > 0 ? new List<string>(subDirs) : new List<string> { outputFolder };

        List<string> commands = new List<string>();
        foreach (string folder in foldersToProcess)
        {
            string folderName = new DirectoryInfo(folder).Name;

            string profileCLI = profile switch
            {
                TrainingProfile.Splat3 => "Splat3",
                TrainingProfile.MCMC => "Splat MCMC",
                TrainingProfile.ADC => "Splat ADC",
                _ => "Splat3"
            };

            string ext = (outputFormat == OutputFormat.PLY) ? "ply" : "psht";
            string outFile = Path.Combine(outputFolder, $"{folderName}.{ext}");

#if UNITY_STANDALONE_WIN
            string cmd = $"\"{postShotCliPath}\" train -i \"{folder}\" -s {trainSteps} --profile \"{profileCLI}\"";
            if (outputFormat == OutputFormat.PLY) cmd += $" --export-splat-ply \"{outFile}\"";
            else cmd += $" --output \"{outFile}\"";
            commands.Add(cmd);
#else
            string cmd = $"\"{postShotCliPath}\" train -i \"{folder}\" -s {trainSteps} --profile \"{profileCLI}\"";
            if (outputFormat == OutputFormat.PLY) cmd += $" --export-splat-ply \"{outFile}\"";
            else cmd += $" --output \"{outFile}\"";
            commands.Add(cmd);
#endif
        }

#if UNITY_STANDALONE_WIN
        string tempBat = Path.Combine(Path.GetTempPath(), "postshot_batch.cmd");
        File.WriteAllLines(tempBat, commands);
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K \"{tempBat}\"",
            UseShellExecute = true
        };
        Process.Start(psi);
#else
        string tempSh = Path.Combine(Path.GetTempPath(), "postshot_batch.sh");
        File.WriteAllLines(tempSh, new[] { "#!/usr/bin/env bash" }.Concat(commands));
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"+x \"{tempSh}\"",
                UseShellExecute = false
            });
        }
        catch { /* ignore */ }

        Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/env",
            Arguments = $"bash \"{tempSh}\"",
            UseShellExecute = true
        });
#endif
#else
        Debug.LogWarning("PostShot training not supported on this platform at runtime.");
#endif
    }
}
