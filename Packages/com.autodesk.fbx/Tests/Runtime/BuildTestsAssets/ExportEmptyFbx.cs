using UnityEngine;
using System.IO;

namespace Autodesk.Fbx.BuildTests
{
    internal class ExportEmptyFbx : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
#if FBXSDK_RUNTIME
            // Build the fbx scene file path 
            // (player/player_data/emptySceneFromRuntime.fbx)
            string fbxFilePath = Application.dataPath;
            fbxFilePath = Path.Combine(fbxFilePath, "emptySceneFromRuntimeBuild.fbx");
            fbxFilePath = Path.GetFullPath(fbxFilePath);

            Debug.Log(string.Format("The file that will be written is {0}", fbxFilePath));

            using (var fbxManager = FbxManager.Create())
            {
                FbxIOSettings fbxIOSettings = FbxIOSettings.Create(fbxManager, Globals.IOSROOT);

                // Configure the IO settings.
                fbxManager.SetIOSettings(fbxIOSettings);

                // Create the exporter 
                var fbxExporter = FbxExporter.Create(fbxManager, "Exporter");

                // Initialize the exporter.
                int fileFormat = fbxManager.GetIOPluginRegistry().FindWriterIDByDescription("FBX ascii (*.fbx)");

                bool status = fbxExporter.Initialize(fbxFilePath, fileFormat, fbxIOSettings);
                // Check that initialization of the fbxExporter was successful
                if (!status)
                {
                    Debug.LogError(string.Format("failed to initialize exporter, reason: {0}",
                                                    fbxExporter.GetStatus().GetErrorString()));
                    return;
                }

                // Create a scene
                var fbxScene = FbxScene.Create(fbxManager, "Scene");

                // create scene info
                FbxDocumentInfo fbxSceneInfo = FbxDocumentInfo.Create(fbxManager, "SceneInfo");

                // set some scene info values
                fbxSceneInfo.mTitle = "fromRuntime";
                fbxSceneInfo.mSubject = "Exported from a Unity runtime";
                fbxSceneInfo.mAuthor = "Unity Technologies";
                fbxSceneInfo.mRevision = "1.0";
                fbxSceneInfo.mKeywords = "export runtime";
                fbxSceneInfo.mComment = "This is to demonstrate the capability of exporting from a Unity runtime, using the FBX SDK C# bindings";

                fbxScene.SetSceneInfo(fbxSceneInfo);

                // Export the scene to the file.
                status = fbxExporter.Export(fbxScene);

                // cleanup
                fbxScene.Destroy();
                fbxExporter.Destroy();
            }
#endif

            // Immediately close the standalone application after
            // exporting the FBX
            Application.Quit();
        }
    }
}