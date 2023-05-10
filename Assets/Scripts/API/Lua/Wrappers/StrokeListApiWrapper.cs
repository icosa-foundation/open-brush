using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class StrokeListApiWrapper
    {
        [MoonSharpHidden]
        public List<Stroke> _Strokes;
        public StrokeApiWrapper lastSelected => new StrokeApiWrapper(SelectionManager.m_Instance.LastSelectedStroke);
        public StrokeApiWrapper last => _Strokes == null || _Strokes.Count == 0 ? null : new StrokeApiWrapper(_Strokes[^1]);
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
