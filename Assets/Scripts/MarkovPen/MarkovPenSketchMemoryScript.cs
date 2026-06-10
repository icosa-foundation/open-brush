using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TiltBrush
{
    /// @brief Tracks strokes that existed before Markov drawing starts and deletes newly created strokes.
    /// Uses reflection to read current stroke references from SketchMemoryScript without modifying it.
    public class MarkovPenSketchMemoryScript
    {
        private static readonly HashSet<Stroke> s_StrokesBeforeMarkovDrawing = new();

        /// @brief Start capturing the current sketch stroke state before Markov drawing begins.
        /// Stores all currently existing strokes so newly created Markov strokes can be detected later.
        public static void BeginMarkovStrokeCapture()
        {
            s_StrokesBeforeMarkovDrawing.Clear();

            foreach (Stroke stroke in GetAllCurrentStrokes())
            {
                if (stroke != null)
                {
                    s_StrokesBeforeMarkovDrawing.Add(stroke);
                }
            }
        }

        /// @brief End Markov stroke capture.
        /// Keeps the captured stroke snapshot available until deletion or the next capture begins.
        public static void EndMarkovStrokeCapture()
        {
        }

        /// @brief Delete all strokes that were created after Markov stroke capture started.
        /// Keeps the saved Markov point lists unchanged.
        public static void DeleteNewMarkovStrokes()
        {
            if (SketchMemoryScript.m_Instance == null)
            {
                return;
            }

            foreach (Stroke stroke in GetAllCurrentStrokes())
            {
                if (stroke == null)
                {
                    continue;
                }

                if (!s_StrokesBeforeMarkovDrawing.Contains(stroke))
                {
                    SketchMemoryScript.m_Instance.MemorizeDeleteSelection(stroke);
                }
            }

            s_StrokesBeforeMarkovDrawing.Clear();
        }

        /// @brief Get all current strokes found inside SketchMemoryScript through reflection.
        /// @return Enumerable collection of current stroke references.
        private static IEnumerable<Stroke> GetAllCurrentStrokes()
        {
            if (SketchMemoryScript.m_Instance == null)
            {
                yield break;
            }

            object memory = SketchMemoryScript.m_Instance;
            Type memoryType = memory.GetType();

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo[] fields = memoryType.GetFields(flags);

            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(memory);

                if (value == null)
                {
                    continue;
                }

                if (value is Stroke singleStroke)
                {
                    yield return singleStroke;
                    continue;
                }

                if (value is IEnumerable enumerable)
                {
                    foreach (object item in enumerable)
                    {
                        if (item is Stroke stroke)
                        {
                            yield return stroke;
                        }
                    }
                }
            }
        }
    }
}
