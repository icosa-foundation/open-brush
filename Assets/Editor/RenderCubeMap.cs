using UnityEngine;
using UnityEditor;
using System.IO;

public class RenderCubeMap : EditorWindow
{
    [SerializeField]
    static int faceSize = 1024;

    [MenuItem("Open Brush/RenderCubeMap", false, 11)]
    static void Init()
    {
        var cam = Camera.main;
        var camPos = cam.transform.position;
        var camRot = cam.transform.rotation;

        // cam.fieldOfView = 45;
        // cam.farClipPlane = 4000;
        // cam.allowMSAA = false;

        cam.transform.position = new Vector3(0, 10, 0);
        cam.transform.rotation = Quaternion.identity;

        RenderToCubeMap(Camera.main);

        cam.transform.position = camPos;
        cam.transform.rotation = camRot;
    }

    static void RenderToCubeMap(Camera Cam)
    {
        Cubemap cubemap = new Cubemap(faceSize, TextureFormat.ARGB32, false);

        var cubeSavePath = Application.dataPath + "/cube" + ".png";

        Cam.RenderToCubemap(cubemap, 63);
        Texture2D flattenedTexture = new Texture2D(faceSize * 4, faceSize * 3, TextureFormat.ARGB32, false);
        for (int i = 0; i < 6; i++)
        {
            int x = 0, y = 0;
            switch (i)
            {
                case 0: x = faceSize; y = faceSize * 2; break; // Top
                case 1: x = faceSize; y = 0; break;            // Bottom
                case 2: x = faceSize * 3; y = faceSize; break; // Right
                case 3: x = 0; y = faceSize; break;            // Left
                case 4: x = faceSize; y = faceSize; break;     // Front
                case 5: x = faceSize * 2; y = faceSize; break; // Back
            }
            Graphics.CopyTexture(cubemap, i, 0, 0, 0, faceSize, faceSize, flattenedTexture, 0, 0, x, y);
        }

        byte[] bytes = flattenedTexture.EncodeToPNG();
        DestroyImmediate(flattenedTexture, true);
        File.WriteAllBytes(cubeSavePath, bytes);

        // var tex2DSavePath = Application.dataPath + "/360tex" +  ".jpg";
        // renderTexCube.ConvertToEquirect(renderTex2D);
        DestroyImmediate(cubemap, true);
        // Texture2D tex2d = new Texture2D(faceSize, 2048, TextureFormat.RGB24,false);
        // RenderTexture.active = renderTex2D;
        // tex2d.ReadPixels(new Rect(0,0,renderTex2D.width, renderTex2D.height),0,0);
        // DestroyImmediate(renderTex2D, true);
        // tex2d.Apply();
        // bytes = tex2d.EncodeToJPG();
        // DestroyImmediate(tex2d, true);
        // File.WriteAllBytes(tex2DSavePath, bytes);
    }
}
