using UnityEngine;
using UnityEditor;
using ImmPlayer;
using System.IO;

/// <summary>
/// Editor window for testing ImmStrokeReader without entering Play mode.
/// </summary>
public class ImmStrokeReaderTestEditor : EditorWindow
{
    private static class Native
    {
        public static bool IsInitialized() => ImmPlayer.ImmStrokeReader.StrokeReader_IsInitialized();
        public static int Init(string logPath) => ImmPlayer.ImmStrokeReader.StrokeReader_Init(logPath);
        public static int LoadFromFile(string path) => ImmPlayer.ImmStrokeReader.StrokeReader_LoadFromFile(path);
        public static void Unload(int docId) => ImmPlayer.ImmStrokeReader.StrokeReader_Unload(docId);
        public static int GetDocumentCount() => ImmPlayer.ImmStrokeReader.StrokeReader_GetDocumentCount();
        public static int GetLayerCount(int docId) => ImmPlayer.ImmStrokeReader.StrokeReader_GetLayerCount(docId);
        public static bool GetLayerInfo(int docId, int layerIdx, out StrokeLayerInfo info) => ImmPlayer.ImmStrokeReader.StrokeReader_GetLayerInfo(docId, layerIdx, out info);
        public static int GetDrawingCount(int docId, int layerIdx) => ImmPlayer.ImmStrokeReader.StrokeReader_GetDrawingCount(docId, layerIdx);
        public static int GetStrokeCount(int docId, int layerIdx, int drawingIdx) => ImmPlayer.ImmStrokeReader.StrokeReader_GetStrokeCount(docId, layerIdx, drawingIdx);
        public static bool GetStrokeInfo(int docId, int layerIdx, int drawingIdx, int strokeIdx, out StrokeInfo info) => ImmPlayer.ImmStrokeReader.StrokeReader_GetStrokeInfo(docId, layerIdx, drawingIdx, strokeIdx, out info);
        public static bool GetStrokePoints(int docId, int layerIdx, int drawingIdx, int strokeIdx, StrokePoint[] points, int maxPoints) => ImmPlayer.ImmStrokeReader.StrokeReader_GetStrokePoints(docId, layerIdx, drawingIdx, strokeIdx, points, maxPoints);
    }
    private string _logPath = "";
    private Vector2 _scrollPos;
    private string _output = "";

    [MenuItem("IMM/Stroke Reader Test")]
    public static void ShowWindow()
    {
        GetWindow<ImmStrokeReaderTestEditor>("Stroke Reader Test");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("ImmStrokeReader Integration Test", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Run Test on logo_animation.imm"))
        {
            RunTest("Assets/ExampleImmFiles/logo_animation.imm");
        }

        if (GUILayout.Button("Run Test on fail-snail.imm"))
        {
            RunTest("Assets/ExampleImmFiles/fail-snail.imm");
        }

        if (GUILayout.Button("Run Test on sample1.imm"))
        {
            RunTest("Assets/ExampleImmFiles/sample1.imm");
        }

        EditorGUILayout.Space();

        if (!string.IsNullOrEmpty(_logPath) && GUILayout.Button("Open Log File"))
        {
            if (File.Exists(_logPath))
            {
                System.Diagnostics.Process.Start(_logPath);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output:", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(400));
        EditorGUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void RunTest(string relativePath)
    {
        _output = "";
        Log($"=== ImmStrokeReader Test ===");
        Log($"Testing: {relativePath}");

        string fullPath = Path.GetFullPath(relativePath);
        Log($"Full path: {fullPath}");

        if (!File.Exists(fullPath))
        {
            Log($"ERROR: File not found!");
            return;
        }

        // Set up log path
        _logPath = Path.Combine(Application.temporaryCachePath, "stroke_reader_test.log");
        Log($"Log path: {_logPath}");

        // Initialize
        Log("\n--- Initializing ---");
        bool wasInitialized = Native.IsInitialized();
        Log($"Already initialized: {wasInitialized}");

        if (!wasInitialized)
        {
            int initResult = Native.Init(_logPath);
            Log($"Init result: {initResult}");
            if (initResult != 0)
            {
                Log("ERROR: Failed to initialize!");
                return;
            }
        }

        // Load
        Log("\n--- Loading ---");
        int docId = Native.LoadFromFile(fullPath);
        Log($"LoadFromFile result (docId): {docId}");

        if (docId < 0)
        {
            Log("ERROR: Failed to load file!");
            return;
        }

        // Query structure
        Log("\n--- Document Structure ---");
        int layerCount = Native.GetLayerCount(docId);
        Log($"Layer count: {layerCount}");

        int totalStrokes = 0;
        int totalPoints = 0;

        for (int l = 0; l < layerCount; l++)
        {
            if (Native.GetLayerInfo(docId, l, out StrokeLayerInfo layerInfo))
            {
                int drawingCount = Native.GetDrawingCount(docId, l);
                Log($"  Layer {l}: id={layerInfo.id}, type={layerInfo.type}, name='{layerInfo.name}', drawings={drawingCount}");

                for (int d = 0; d < drawingCount; d++)
                {
                    int strokeCount = Native.GetStrokeCount(docId, l, d);
                    totalStrokes += strokeCount;

                    if (strokeCount > 0)
                    {
                        Log($"    Drawing {d}: {strokeCount} strokes");
                    }

                    for (int s = 0; s < strokeCount; s++)
                    {
                        if (Native.GetStrokeInfo(docId, l, d, s, out StrokeInfo strokeInfo))
                        {
                            totalPoints += strokeInfo.numPoints;
                        }
                    }
                }
            }
        }

        Log($"\nTotal: {totalStrokes} strokes, {totalPoints} points");

        // Sample first stroke
        Log("\n--- First Stroke Sample ---");
        for (int l = 0; l < layerCount && totalStrokes > 0; l++)
        {
            int drawingCount = Native.GetDrawingCount(docId, l);
            for (int d = 0; d < drawingCount; d++)
            {
                int strokeCount = Native.GetStrokeCount(docId, l, d);
                for (int s = 0; s < strokeCount; s++)
                {
                    if (Native.GetStrokeInfo(docId, l, d, s, out StrokeInfo info) && info.numPoints > 0)
                    {
                        Log($"Stroke [{l},{d},{s}]: brush={info.brushType}, vis={info.visibilityMode}, pts={info.numPoints}");
                        Log($"  BBox: ({info.bboxMinX:F3}, {info.bboxMinY:F3}, {info.bboxMinZ:F3}) to ({info.bboxMaxX:F3}, {info.bboxMaxY:F3}, {info.bboxMaxZ:F3})");

                        StrokePoint[] points = new StrokePoint[info.numPoints];
                        if (Native.GetStrokePoints(docId, l, d, s, points, info.numPoints))
                        {
                            int numToShow = Mathf.Min(3, points.Length);
                            for (int p = 0; p < numToShow; p++)
                            {
                                var pt = points[p];
                                Log($"  Point {p}: pos=({pt.px:F3}, {pt.py:F3}, {pt.pz:F3}), col=({pt.r:F2}, {pt.g:F2}, {pt.b:F2}, {pt.alpha:F2}), w={pt.width:F1}");
                            }
                            if (points.Length > numToShow)
                            {
                                Log($"  ... and {points.Length - numToShow} more points");
                            }
                        }
                        goto done_sampling;
                    }
                }
            }
        }
        done_sampling:

        // Cleanup
        Log("\n--- Cleanup ---");
        Native.Unload(docId);
        Log($"Document unloaded");

        int remaining = Native.GetDocumentCount();
        Log($"Remaining documents: {remaining}");

        Log("\n=== Test Complete ===");
        Repaint();
    }

    void Log(string message)
    {
        _output += message + "\n";
        Debug.Log("[StrokeReaderTest] " + message);
    }
}
