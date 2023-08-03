using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of Strokes in the scene. (You don't instantiate this yourself. Access this via Sketch.strokes)")]
    [MoonSharpUserData]
    public class StrokeListApiWrapper
    {
        [MoonSharpHidden]
        public List<Stroke> _Strokes;

        [LuaDocsDescription("Returns the last stroke that was selected")]
        public StrokeApiWrapper lastSelected => new StrokeApiWrapper(SelectionManager.m_Instance.LastSelectedStroke);

        [LuaDocsDescription("Returns the last Stroke")]
        public StrokeApiWrapper last => _Strokes == null || _Strokes.Count == 0 ? null : new StrokeApiWrapper(_Strokes[^1]);

        [LuaDocsDescription("Returns the Stroke at the given index")]
        public StrokeApiWrapper this[int index] => new StrokeApiWrapper(_Strokes[index]);

        [LuaDocsDescription("The number of strokes")]
        public int count => _Strokes?.Count ?? 0;

        public StrokeListApiWrapper()
        {
            _Strokes = new List<Stroke>();
        }

        public StrokeListApiWrapper(List<Stroke> strokes)
        {
            _Strokes = strokes;
        }

        [LuaDocsDescription("Deletes all the strokes in the list")]
        [LuaDocsExample("myStrokes:Delete()")]
        public void Delete()
        {
            foreach (var stroke in _Strokes)
            {
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.Uncreate();
            }
        }
    }
}
