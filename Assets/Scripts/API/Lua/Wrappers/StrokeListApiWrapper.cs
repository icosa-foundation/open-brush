using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of Strokes in the scene. (You don't instantiate this yourself. Access this via Sketch.strokes ")]
    [MoonSharpUserData]
    public class StrokeListApiWrapper
    {
        [MoonSharpHidden]
        public List<Stroke> _Strokes;
        public StrokeApiWrapper lastSelected => new StrokeApiWrapper(SelectionManager.m_Instance.LastSelectedStroke);
        public StrokeApiWrapper last => _Strokes == null || _Strokes.Count == 0 ? null : new StrokeApiWrapper(_Strokes[^1]);

        public StrokeApiWrapper this[int index] => new StrokeApiWrapper(_Strokes[index]);
        public int count => _Strokes?.Count ?? 0;

        public StrokeListApiWrapper()
        {
            _Strokes = new List<Stroke>();
        }

        public StrokeListApiWrapper(List<Stroke> strokes)
        {
            _Strokes = strokes;
        }
    }
}
