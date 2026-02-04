using UnityEngine;

namespace ImmPlayer
{
    /// <summary>
    /// Test component for verifying ImmStrokeReader plugin integration.
    /// Attach to a GameObject and assign an IMM file path to test loading.
    /// </summary>
    public class ImmStrokeReaderTest : MonoBehaviour
    {
        [Tooltip("Path to an IMM file to load on start")]
        public string immFilePath;

        [Tooltip("Log file path (leave empty for default)")]
        public string logFilePath;

        [Tooltip("Load file automatically on Start")]
        public bool loadOnStart = true;

        private StrokeReaderDocument _document;

        void Start()
        {
            if (loadOnStart && !string.IsNullOrEmpty(immFilePath))
            {
                LoadAndLogStats();
            }
        }

        void OnDestroy()
        {
            _document?.Dispose();
            _document = null;
        }

        [ContextMenu("Load IMM File")]
        public void LoadAndLogStats()
        {
            if (string.IsNullOrEmpty(immFilePath))
            {
                Debug.LogError("[StrokeReaderTest] No IMM file path specified");
                return;
            }

            // Dispose previous document if any
            _document?.Dispose();

            _document = new StrokeReaderDocument();
            string logPath = string.IsNullOrEmpty(logFilePath) ? null : logFilePath;

            Debug.Log($"[StrokeReaderTest] Loading: {immFilePath}");

            if (!_document.Load(immFilePath, logPath))
            {
                Debug.LogError("[StrokeReaderTest] Failed to load document");
                return;
            }

            LogDocumentStats();
        }

        [ContextMenu("Log Document Stats")]
        public void LogDocumentStats()
        {
            if (_document == null || !_document.IsLoaded)
            {
                Debug.LogWarning("[StrokeReaderTest] No document loaded");
                return;
            }

            int layerCount = _document.LayerCount;
            Debug.Log($"[StrokeReaderTest] Document loaded with {layerCount} layers");

            int totalStrokes = 0;
            int totalPoints = 0;

            for (int l = 0; l < layerCount; l++)
            {
                if (_document.GetLayerInfo(l, out StrokeLayerInfo layerInfo))
                {
                    int drawingCount = _document.GetDrawingCount(l);
                    Debug.Log($"[StrokeReaderTest]   Layer {l}: id={layerInfo.id}, type={layerInfo.type}, name='{layerInfo.name}', drawings={drawingCount}");

                    for (int d = 0; d < drawingCount; d++)
                    {
                        int strokeCount = _document.GetStrokeCount(l, d);
                        totalStrokes += strokeCount;

                        for (int s = 0; s < strokeCount; s++)
                        {
                            if (_document.GetStrokeInfo(l, d, s, out StrokeInfo strokeInfo))
                            {
                                totalPoints += strokeInfo.numPoints;
                            }
                        }

                        if (strokeCount > 0)
                        {
                            Debug.Log($"[StrokeReaderTest]     Drawing {d}: {strokeCount} strokes");
                        }
                    }
                }
            }

            Debug.Log($"[StrokeReaderTest] Total: {totalStrokes} strokes, {totalPoints} points");
        }

        [ContextMenu("Log First Stroke Points")]
        public void LogFirstStrokePoints()
        {
            if (_document == null || !_document.IsLoaded)
            {
                Debug.LogWarning("[StrokeReaderTest] No document loaded");
                return;
            }

            // Find first stroke with points
            for (int l = 0; l < _document.LayerCount; l++)
            {
                int drawingCount = _document.GetDrawingCount(l);
                for (int d = 0; d < drawingCount; d++)
                {
                    int strokeCount = _document.GetStrokeCount(l, d);
                    for (int s = 0; s < strokeCount; s++)
                    {
                        if (_document.GetStrokeInfo(l, d, s, out StrokeInfo info) && info.numPoints > 0)
                        {
                            StrokePoint[] points = _document.GetStrokePoints(l, d, s);
                            if (points != null && points.Length > 0)
                            {
                                Debug.Log($"[StrokeReaderTest] First stroke: layer={l}, drawing={d}, stroke={s}");
                                Debug.Log($"[StrokeReaderTest]   Brush={info.brushType}, VisMode={info.visibilityMode}, Points={info.numPoints}");
                                Debug.Log($"[StrokeReaderTest]   Bounds: ({info.bboxMinX:F3}, {info.bboxMinY:F3}, {info.bboxMinZ:F3}) to ({info.bboxMaxX:F3}, {info.bboxMaxY:F3}, {info.bboxMaxZ:F3})");

                                // Log first few points
                                int numToLog = Mathf.Min(5, points.Length);
                                for (int p = 0; p < numToLog; p++)
                                {
                                    var pt = points[p];
                                    Debug.Log($"[StrokeReaderTest]   Point {p}: pos=({pt.px:F3}, {pt.py:F3}, {pt.pz:F3}), color=({pt.r:F2}, {pt.g:F2}, {pt.b:F2}, {pt.alpha:F2}), width={pt.width:F3}");
                                }

                                if (points.Length > numToLog)
                                {
                                    Debug.Log($"[StrokeReaderTest]   ... and {points.Length - numToLog} more points");
                                }
                                return;
                            }
                        }
                    }
                }
            }

            Debug.LogWarning("[StrokeReaderTest] No strokes with points found");
        }

        [ContextMenu("Unload Document")]
        public void UnloadDocument()
        {
            if (_document != null)
            {
                _document.Dispose();
                _document = null;
                Debug.Log("[StrokeReaderTest] Document unloaded");
            }
        }
    }
}
