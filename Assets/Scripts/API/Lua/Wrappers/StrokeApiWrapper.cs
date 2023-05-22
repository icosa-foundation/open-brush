using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public class StrokeApiWrapper
    {
        public Stroke _Stroke;
        private PathApiWrapper m_Path;
        public PathApiWrapper path
        {
            get
            {
                if (_Stroke == null) return new PathApiWrapper();
                if (m_Path == null)
                {
                    int count = _Stroke.m_ControlPoints.Count();
                    var path = new List<TrTransform>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var cp = _Stroke.m_ControlPoints[i];
                        var tr = TrTransform.TR(cp.m_Pos, cp.m_Orient);
                        path.Add(tr);
                    }
                    m_Path = new PathApiWrapper(path);
                }
                return m_Path;
            }
            set
            {
                var startTime = _Stroke.m_ControlPoints[0].m_TimestampMs;
                var endTime = _Stroke.m_ControlPoints[^1].m_TimestampMs;
                _Stroke.m_ControlPoints = new PointerManager.ControlPoint[value._Path.Count];
                for (var i = 0; i < value._Path.Count; i++)
                {
                    var tr = value[i]._TrTransform;
                    _Stroke.m_ControlPoints[i] = new PointerManager.ControlPoint
                    {
                        m_Pos = tr.translation,
                        m_Orient = tr.rotation,
                        m_Pressure = tr.scale,
                        m_TimestampMs = (uint)Mathf.RoundToInt(Mathf.Lerp(startTime, endTime, i))
                    };
                }
                _Stroke.Recreate();
            }
        }

        public string brushType => _Stroke?.m_BatchSubset.m_ParentBatch.Brush.Description;
        public float brushSize => _Stroke.m_BrushSize;
        public ColorApiWrapper brushColor => new ColorApiWrapper(_Stroke.m_Color);

        public LayerApiWrapper layer => _Stroke != null ? new LayerApiWrapper(_Stroke.Canvas) : null;

        public StrokeApiWrapper(Stroke stroke)
        {
            _Stroke = stroke;
        }

        public override string ToString()
        {
            return _Stroke == null ? "Empty Stroke" : $"{brushType} stroke on {layer._CanvasScript.name})";
        }

        // public Transform this[int index] => SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
        // public Stroke last => this[count - 1];
        // public Stroke this[int index] => SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
        // public Stroke last => this[count - 1];
        // public  Stroke main => this[0];
        // public int index => SketchMemoryScript.m_Instance.GetAllActiveStrokes().IndexOf(this._Stroke);

        // public static StrokesApiWrapper New(StrokesApiWrapper stroke)
        // {
        //     var instance = new StrokesApiWrapper(stroke);
        //     return instance;
        // }
        // public void add(int index) => ApiMethods.AddPointToStroke(index);
        // public void quantize() => ApiMethods.QuantizeSelection(index);
        // public void addNoise(Vector3 a) => ApiMethods.PerlinNoiseSelection(a);


        // Highly experimental
        public void ChangeMaterial(string brushName)
        {
            var brush = ApiMethods.LookupBrushDescriptor(brushName);
            _Stroke.m_BatchSubset.m_ParentBatch.ReplaceMaterial(brush.Material);
        }

        public TrTransform this[int index]
        {
            get => path._Path[index];
            set
            {
                var newPath = path._Path.ToList();
                newPath[index] = value;
                path = new PathApiWrapper(newPath);
            }
        }

        public int count => _Stroke?.m_ControlPoints.Length ?? 0;

        public void Delete()
        {
            SketchMemoryScript.m_Instance.RemoveMemoryObject(_Stroke);
            _Stroke.Uncreate();
            _Stroke = null;
        }
        public void Select()
        {
            SelectionManager.m_Instance.SelectStrokes(new List<Stroke> { _Stroke });
        }
        public void SelectMultiple(int from, int to) => ApiMethods.SelectStrokes(from, to);
        public void Join(int from, int to) => ApiMethods.JoinStrokes(from, to);
        public void JoinPrevious() => ApiMethods.JoinStroke();
        public void Import(string name) => ApiMethods.MergeNamedFile(name);

    }

}
