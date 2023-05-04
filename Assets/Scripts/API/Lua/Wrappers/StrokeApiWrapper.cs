using MoonSharp.Interpreter;

namespace TiltBrush
{
    [MoonSharpUserData]
    public class StrokeApiWrapper
    {
        public Stroke _Stroke;

        public StrokeApiWrapper(Stroke stroke)
        {
            _Stroke = stroke;
        }

        public StrokeApiWrapper(StrokeApiWrapper stroke)
        {
            _Stroke = stroke._Stroke;
        }

        // public int index => SketchMemoryScript.m_Instance.GetAllActiveStrokes().IndexOf(this._Stroke);

        // public static StrokesApiWrapper New(StrokesApiWrapper stroke)
        // {
        //     var instance = new StrokesApiWrapper(stroke);
        //     return instance;
        // }

        public override string ToString()
        {
            return $"{_Stroke.m_BatchSubset.m_ParentBatch.Brush.m_Description} stroke on {_Stroke.Canvas.name})";
        }

        // Highly experimental
        public void ChangeMaterial(string brushName)
        {
            var brush = ApiMethods.LookupBrushDescriptor(brushName);
            _Stroke.m_BatchSubset.m_ParentBatch.ReplaceMaterial(brush.Material);
        }

        public Stroke this[int index] => SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
        public Stroke last => this[count - 1];
        public  Stroke main => this[0];

        public static int count => SketchMemoryScript.m_Instance.StrokeCount;

        public void Delete(int index) => ApiMethods.DeleteStroke(index);
        public void Select(int index) => ApiMethods.SelectStroke(index);
        // public void add(int index) => ApiMethods.AddPointToStroke(index);
        public void SelectMultiple(int from, int to) => ApiMethods.SelectStrokes(from, to);
        // public void quantize() => ApiMethods.QuantizeSelection(index);
        // public void addNoise(Vector3 a) => ApiMethods.PerlinNoiseSelection(a);
        public void Join(int from, int to) => ApiMethods.JoinStrokes(from, to);
        public void JoinPrevious() => ApiMethods.JoinStroke();
        public void Import(string name) => ApiMethods.MergeNamedFile(name);
    }
}
