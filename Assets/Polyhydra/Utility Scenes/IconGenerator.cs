using System;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush;
using TiltBrush.MeshEditing;
using UnityEngine;
using Random = UnityEngine.Random;


[ExecuteInEditMode]
public class IconGenerator : MonoBehaviour
{
    public Color[] DefaultColors;
    public Vector3 cameraPosition;
    public int resWidth = 500;
    public int resHeight = 500;
    public float ZoomFactor = 1.0f;

    private string filename;
    private Camera camera;
    private bool takeShot;
    public bool RandomRotation;


    void Init()
    {
        camera = Camera.main;
        camera.clearFlags = CameraClearFlags.Nothing;
        camera.backgroundColor = Color.white;
        PreviewPolyhedron.m_Instance.Init();
        PreviewPolyhedron.m_Instance.gameObject.SetActive(true);
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
        PreviewPolyhedron.m_Instance.ImmediateMakePolyhedron();
    }

    [ContextMenu("Take All Screenshots")]
    void TakeAllScreenshots()
    {
        TakeAllGridScreenshots();
        TakeAllRadialScreenshots();
        TakeAllUniformScreenshots();
        TakeAllVariousScreenshots();
        TakeAllShapeScreenshots();
        TakeCategoryScreenshot();
    }

    [ContextMenu("Take Waterman Screenshot")]
    void TakeCategoryScreenshot()
    {
        Init();
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Waterman;
        filename = PolyScreenShotName($"Waterman");
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 35;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 5;
        CreateThumbnail();
    }

    [ContextMenu("Take All Shape Screenshots")]
    void TakeAllShapeScreenshots()
    {
        Init();
        var names = Enum.GetNames(typeof(ShapeTypes));
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Shapes;

        for (var index = 0; index < names.Length; index++)
        {
            var oldRot = PreviewPolyhedron.m_Instance.transform.rotation;
            filename = PolyScreenShotName($"other_{names[index]}");
            PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType = (ShapeTypes)index;
            switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.ShapeType)
            {
                case ShapeTypes.Polygon:
                case ShapeTypes.Star:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 6;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Float = .5f;
                    break;
                case ShapeTypes.Arc:
                case ShapeTypes.Arch:
                    PreviewPolyhedron.m_Instance.transform.Rotate(new Vector3(-90, 0, 0));
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 8;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Float = .333f;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Float = .75f;
                    break;
                default:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Float = 1f;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Float = 1f;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Float = 1f;
                    break;
            }
            CreateThumbnail();
            PreviewPolyhedron.m_Instance.transform.rotation = oldRot;
        }
    }

    [ContextMenu("Take All Various Poly Screenshots")]
    void TakeAllVariousScreenshots()
    {
        Init();
        var names = Enum.GetNames(typeof(VariousSolidTypes));
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Various;

        for (var index = 0; index < names.Length; index++)
        {
            filename = PolyScreenShotName($"other_{names[index]}");
            PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType = (VariousSolidTypes)index;
            switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType)
            {
                case VariousSolidTypes.Box:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 3;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 4;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Int = 3;
                    break;
                case VariousSolidTypes.Stairs:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 3;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Float = 3;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Float = 3;
                    break;
                case VariousSolidTypes.UvHemisphere:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 8;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 8;
                    break;
                case VariousSolidTypes.UvSphere:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 8;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 8;
                    break;
                case VariousSolidTypes.Torus:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 8;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 8;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Float = 16;
                    break;
            }
            CreateThumbnail();
        }
    }

    [ContextMenu("Take All Grid Shape Screenshots")]
    void TakeAllGridShapeScreenshots()
    {
        Init();
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.RegularGrids;
        var gridNames = Enum.GetNames(typeof(GridEnums.GridShapes));
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GridType = GridEnums.GridTypes.Triangular;
        for (var index = 0; index < gridNames.Length; index++)
        {
            switch ((GridEnums.GridShapes)index)
            {
                case GridEnums.GridShapes.Sphere:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 12;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 12;
                    break;
                case GridEnums.GridShapes.Plane:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 4;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 4;
                    break;
                case GridEnums.GridShapes.Cone:
                case GridEnums.GridShapes.Cylinder:
                case GridEnums.GridShapes.Polar:
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 8;
                    PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 8;
                    break;
            }
            filename = PolyScreenShotName($"gridshape_{gridNames[index]}");
            PreviewPolyhedron.m_Instance.m_PolyRecipe.GridShape = (GridEnums.GridShapes)index;
            CreateThumbnail();
        }
    }


    [ContextMenu("Take All Grid Screenshots")]
    void TakeAllGridScreenshots()
    {
        var oldPos = cameraPosition;
        Init();
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.RegularGrids;
        var gridNames = Enum.GetNames(typeof(GridEnums.GridTypes));
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GridShape = GridEnums.GridShapes.Plane;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 2;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 2;
        for (var index = 0; index < gridNames.Length; index++)
        {
            filename = PolyScreenShotName($"grid_{gridNames[index]}");
            PreviewPolyhedron.m_Instance.m_PolyRecipe.GridType = (GridEnums.GridTypes)index;
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
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Radial;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Int = 5;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Int = 2;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param1Float = .75f;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param2Float = .75f;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Param3Float = .75f;
        for (var index = 0; index < radialNames.Length; index++)
        {
            filename = PolyScreenShotName($"radial_{radialNames[index]}");
            PreviewPolyhedron.m_Instance.m_PolyRecipe.RadialPolyType = (RadialSolids.RadialPolyType)index;
            CreateThumbnail();
        }
    }

    [ContextMenu("Take All Uniform Screenshots")]
    void TakeAllUniformScreenshots()
    {
        Init();
        var uniformNames = Enum.GetNames(typeof(UniformTypes));
        PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType = GeneratorTypes.Uniform;
        for (var index = 5; index < uniformNames.Length; index++)
        {
            filename = PolyScreenShotName($"uniform_{uniformNames[index]}");
            PreviewPolyhedron.m_Instance.m_PolyRecipe.UniformPolyType = (UniformTypes)index;
            CreateThumbnail();
        }
    }

    private void CreateThumbnail()
    {
        PreviewPolyhedron.m_Instance.m_PolyRecipe.Colors = DefaultColors;
        PreviewPolyhedron.m_Instance.m_PolyRecipe.ColorMethod = ColorMethods.ByRole;
        PreviewPolyhedron.m_Instance.ImmediateMakePolyhedron();
        PreviewPolyhedron.m_Instance.m_PolyMesh.Recenter();
        switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.GeneratorType)
        {
            case GeneratorTypes.Various:
                PreviewPolyhedron.m_Instance.transform.localScale = Vector3.one;
                switch (PreviewPolyhedron.m_Instance.m_PolyRecipe.VariousSolidsType)
                {
                    case VariousSolidTypes.Box:
                        PreviewPolyhedron.m_Instance.transform.localRotation = Quaternion.Euler(-30, 30, 0);
                        break;
                    case VariousSolidTypes.Stairs:
                        PreviewPolyhedron.m_Instance.transform.localRotation = Quaternion.Euler(0, 90, 0);
                        break;
                    default:
                        PreviewPolyhedron.m_Instance.transform.localRotation = Quaternion.identity;
                        break;
                }
                break;
            case GeneratorTypes.Uniform:
                PreviewPolyhedron.m_Instance.m_PolyRecipe.ColorMethod = ColorMethods.BySides;
                if (PreviewPolyhedron.m_Instance.m_PolyRecipe.UniformPolyType == UniformTypes.Cube)
                {
                    PreviewPolyhedron.m_Instance.transform.localRotation = Quaternion.Euler(-30, 30, 0);
                    PreviewPolyhedron.m_Instance.transform.localScale = Vector3.one * .3333f;
                }
                else
                {
                    PreviewPolyhedron.m_Instance.transform.localRotation = Quaternion.identity;
                    PreviewPolyhedron.m_Instance.transform.localScale = Vector3.one * .5f;
                }
                break;
            default:
                PreviewPolyhedron.m_Instance.transform.localScale = Vector3.one;
                break;
        }
        camera.transform.position = cameraPosition;
        PolyhydraPanel.FocusCameraOnGameObject(camera, PreviewPolyhedron.m_Instance.gameObject, ZoomFactor, false);
        TakeShotNow();
    }

    public static string PolyScreenShotName(string polyName)
    {
        return string.Format("{0}/{1}.jpg",
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
        if (RandomRotation)
        {
            PreviewPolyhedron.m_Instance.transform.localRotation = Random.rotation;
        }
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
