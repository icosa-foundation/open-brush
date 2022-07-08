using System;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush;
using TiltBrush.MeshEditing;
using UnityEngine;


[ExecuteInEditMode]
public class IconGenerator : MonoBehaviour
{
    public Vector3 cameraPosition;
    public int resWidth = 500;
    public int resHeight = 500;
    public float ZoomFactor = 1.0f;

    private string filename;
    private Camera camera;
    private PreviewPolyhedron previewPoly;
    private bool takeShot;


    void Init()
    {
        camera = Camera.main;
        camera.clearFlags = CameraClearFlags.Nothing;
        camera.backgroundColor = Color.white;
        previewPoly = GetComponentInChildren<PreviewPolyhedron>(true);
        previewPoly.Init();
        previewPoly.gameObject.SetActive(true);
    }

    [ContextMenu("Take Screenshot")]
    public void TakeScreenshotFromEditor()
    {
        filename = ScreenShotName(resWidth, resHeight);
        TakeShotNow();
    }

    [ContextMenu("Build Now")]
    public void BuildNow()
    {
        Init();
        previewPoly.ImmediateMakePolyhedron();
    }

    [ContextMenu("Take All Various Poly Screenshots")]
    void TakeAllVariousScreenshots()
    {
        Init();
        var names = Enum.GetNames(typeof(VariousSolidTypes));
        EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Various;

        for (var index = 0; index < names.Length; index++)
        {
            filename = PolyScreenShotName($"other_{names[index]}");
            previewPoly.VariousSolidsType = (VariousSolidTypes)index;
            switch (previewPoly.VariousSolidsType)
            {
                case VariousSolidTypes.Box:
                    previewPoly.Param1Int = 3;
                    previewPoly.Param1Int = 3;
                    previewPoly.Param3Int = 3;
                    break;
                case VariousSolidTypes.Stairs:
                    previewPoly.Param1Int = 3;
                    previewPoly.Param2Int = 3;
                    previewPoly.Param3Int = 3;
                    break;
                case VariousSolidTypes.UvHemisphere:
                    previewPoly.Param1Int = 8;
                    previewPoly.Param2Int = 8;
                    break;
                case VariousSolidTypes.UvSphere:
                    previewPoly.Param1Int = 8;
                    previewPoly.Param2Int = 8;
                    break;
                case VariousSolidTypes.Torus:
                    previewPoly.Param1Int = 8;
                    previewPoly.Param2Int = 8;
                    break;
            }
            CreateThumbnail();
        }
    }

    [ContextMenu("Take All Grid Screenshots")]
    void TakeAllGridScreenshots()
    {
        var oldPos = cameraPosition;
        Init();
        EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Grid;
        var gridNames = Enum.GetNames(typeof(GridEnums.GridTypes));
        previewPoly.GridShape = GridEnums.GridShapes.Plane;
        previewPoly.Param1Int = 2;
        previewPoly.Param2Int = 2;
        for (var index = 0; index < gridNames.Length; index++)
        {
            filename = PolyScreenShotName($"grid_{gridNames[index]}");
            previewPoly.GridType = (GridEnums.GridTypes)index;
            cameraPosition = new Vector3(0, 1, -.5f);
            CreateThumbnail();
        }
        cameraPosition = oldPos;
    }

    [ContextMenu("Take All Radial Screenshots")]
    void TakeAllRadialScreenshots()
    {
        Init();
        var radialNames = Enum.GetNames(typeof(RadialSolids.RadialPolyType));
        EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Radial;
        previewPoly.Param1Int = 5;
        previewPoly.Param2Int = 2;
        previewPoly.Param1Float = .75f;
        previewPoly.Param2Float = .75f;
        previewPoly.Param3Float = .75f;
        for (var index = 0; index < radialNames.Length; index++)
        {
            filename = PolyScreenShotName($"radial_{radialNames[index]}");
            previewPoly.RadialPolyType = (RadialSolids.RadialPolyType)index;
            CreateThumbnail();
        }
    }

    [ContextMenu("Take All Wythoff Screenshots")]
    void TakeAllWythoffScreenshots()
    {
        Init();
        var uniformNames = Enum.GetNames(typeof(UniformTypes));
        EditableModelManager.CurrentModel.GeneratorType = GeneratorTypes.Uniform;
        for (var index = 6; index < uniformNames.Length; index++)
        {
            filename = PolyScreenShotName($"uniform_{uniformNames[index]}");
            previewPoly.UniformPolyType = (UniformTypes)index;
            CreateThumbnail();
        }
    }

    private void CreateThumbnail()
    {
        previewPoly.ImmediateMakePolyhedron();
        previewPoly.m_PolyMesh.Recenter();
        camera.transform.position = cameraPosition;
        PolyhydraPanel.FocusCameraOnGameObject(camera, previewPoly.gameObject, ZoomFactor, false);
        TakeShotNow();
    }

    public static string PolyScreenShotName(string polyName)
    {
        return string.Format("{0}/poly_{1}.jpg",
            Application.persistentDataPath,
            polyName
        );
    }

    public static string PresetScreenShotName(string presetName)
    {
        return string.Format("{0}/preset_{1}.jpg",
            Application.persistentDataPath,
            presetName
        );
    }

    public static string ScreenShotName(int width, int height)
    {
        return string.Format("{0}/screenshot_{1}x{2}_{3}.jpg",
            Application.persistentDataPath,
            width, height,
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
    }

    private void TakeShotNow()
    {
        RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
        camera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
        camera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        camera.targetTexture = null;
        RenderTexture.active = null;
        if (Application.isPlaying)
        {
            Destroy(rt);
        }
        else
        {
            DestroyImmediate(rt);
        }
        byte[] bytes = screenShot.EncodeToJPG(90);
        System.IO.File.WriteAllBytes(filename, bytes);
        Debug.Log($"Saving shot to {filename}");
    }
}
