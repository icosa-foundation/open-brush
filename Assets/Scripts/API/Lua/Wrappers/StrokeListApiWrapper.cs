using System.Collections.Generic;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class StrokeListApiWrapper
    {
        [MoonSharpHidden]
        public List<Stroke> _Strokes;
        public static StrokeApiWrapper lastSelected => new StrokeApiWrapper(SelectionManager.m_Instance.LastSelectedStroke);
        public static StrokeApiWrapper last => new StrokeApiWrapper(SelectionManager.m_Instance.LastStroke);

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
