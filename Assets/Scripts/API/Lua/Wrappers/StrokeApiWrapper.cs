using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A specific brush stroke")]
    [MoonSharpUserData]
    public class StrokeApiWrapper
    {
        public Stroke _Stroke;

        private PathApiWrapper m_Path;

        [LuaDocsDescription("The control points of this stroke from a Path")]
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

        [LuaDocsDescription("The stroke's brush type")]
        public string brushType
        {
            get => _Stroke?.m_BatchSubset.m_ParentBatch.Brush.Description;
            set
            {
                _Stroke.m_BrushGuid = ApiMethods.LookupBrushDescriptor(value).m_Guid;
                _Stroke.Recreate();
            }
        }

        [LuaDocsDescription("The stroke's size")]
        public float brushSize
        {
            get => _Stroke.m_BrushSize;
            set
            {
                _Stroke.m_BrushSize = value;
                _Stroke.Recreate();
            }
        }

        [LuaDocsDescription("The stroke's Color")]
        public ColorApiWrapper brushColor
        {
            get => new ColorApiWrapper(_Stroke.m_Color);
            set
            {
                _Stroke.m_Color = value._Color;
                _Stroke.Recreate();
            }
        }

        [LuaDocsDescription("The layer the stroke is on")]
        public LayerApiWrapper layer
        {
            get => _Stroke != null ? new LayerApiWrapper(_Stroke.Canvas) : null;
            set => _Stroke.SetParentKeepWorldPosition(value._CanvasScript);
        }

        [LuaDocsDescription("The group this stroke is part of")]
        public GroupApiWrapper group
        {
            get => _Stroke != null ? new GroupApiWrapper(_Stroke.Group, layer._CanvasScript) : null;
            set => _Stroke.Group = value._Group;
        }

        public StrokeApiWrapper(Stroke stroke)
        {
            _Stroke = stroke;
        }

        public override string ToString()
        {
            return _Stroke == null ? "Empty Stroke" : $"{brushType} stroke on {layer._CanvasScript.name})";
        }

        // Highly experimental
        [LuaDocsDescription("Assigns the material from another brush type to this stroke (Experimental. Results are unpredictable and are not saved with the scene)")]
        [LuaDocsExample(@"myStroke.ChangeMaterial(""Light"")")]
        [LuaDocsParameter("brushName", "The name (or guid) of the brush to get the material from")]
        public void ChangeMaterial(string brushName)
        {
            var brush = ApiMethods.LookupBrushDescriptor(brushName);
            _Stroke.m_BatchSubset.m_ParentBatch.ReplaceMaterial(brush.Material);
        }

        [LuaDocsDescription("Gets or sets a control point by index")]
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

        [LuaDocsDescription("The number of control points in this stroke")]
        public int count => _Stroke?.m_ControlPoints.Length ?? 0;

        [LuaDocsDescription("Deletes the current stroke")]
        [LuaDocsExample("myStroke:Delete()")]
        public void Delete()
        {
            SketchMemoryScript.m_Instance.RemoveMemoryObject(_Stroke);
            _Stroke.Uncreate();
            _Stroke = null;
        }

        [LuaDocsDescription("Adds this stroke to the current selection")]
        [LuaDocsExample("myStroke:Select()")]
        public void Select()
        {
            SelectionManager.m_Instance.SelectStrokes(new List<Stroke> { _Stroke });
        }

        [LuaDocsDescription("Removes this stroke from the current selection")]
        [LuaDocsExample("myStroke:Deselect()")]
        public void Deselect() => SelectionManager.m_Instance.DeselectStrokes(new[] { _Stroke });

        [LuaDocsDescription("Adds multiple strokes to the current selection")]
        [LuaDocsExample("Stroke:SelectMultiple(0, 4) --Adds the first 5 strokes on the sketch")]
        [LuaDocsParameter("from", "Start stroke index (0 is the first stroke that was drawn")]
        [LuaDocsParameter("to", "End stroke index")]
        public static void SelectRange(int from, int to) => ApiMethods.SelectStrokes(from, to);

        [LuaDocsDescription("Joins joins multiple strokes into one stroke")]
        [LuaDocsExample("newStroke = Stroke:Join(0, 10)")]
        [LuaDocsParameter("from", "Start stroke index (0 is the first stroke that was drawn")]
        [LuaDocsParameter("to", "End stroke index")]
        public StrokeApiWrapper JoinRange(int from, int to) => new StrokeApiWrapper(ApiMethods.JoinStrokes(from, to));

        [LuaDocsDescription("Joins a stroke with the previous stroke")]
        [LuaDocsExample("newStroke = myStroke:JoinPrevious()")]
        public StrokeApiWrapper JoinToPrevious() => new StrokeApiWrapper(ApiMethods.JoinStroke());

        [LuaDocsDescription("Joins a stroke with the previous stroke")]
        [LuaDocsExample("newStroke = myStroke:JoinPrevious()")]
        [LuaDocsParameter("stroke2", "The stroke to join to this one")]
        public StrokeApiWrapper Join(StrokeApiWrapper stroke2) => new StrokeApiWrapper(ApiMethods.JoinStrokes(_Stroke, stroke2._Stroke));

        [LuaDocsDescription("Imports the file with the specified name from the user's Sketches folder and merges it's strokes into the current sketch")]
        [LuaDocsExample("Stroke:MergeFrom(string name)")]
        [LuaDocsParameter("name", "Name of the file to be merged")]
        public void MergeFrom(string name) => ApiMethods.MergeNamedFile(name);

        [LuaDocsDescription("Hides the section of the stroke that is outside the specified range")]
        [LuaDocsParameter("clipStart", "The amount of the stroke to hide from the start (0-1)")]
        [LuaDocsParameter("clipEnd", "The amount of the stroke to hide from the end (0-1)")]
        [LuaDocsExample("myStroke:SetShaderClipping(0.1, 0.9)")]
        public void SetShaderClipping(float clipStart, float clipEnd)
        {
            _Stroke.SetShaderClipping(clipStart, clipEnd);
        }

        [LuaDocsDescription("Changes a shader float parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("value", "The new value")]
        [LuaDocsExample("myStroke:SetShaderFloat(\"_EmissionGain\", 0.5)")]
        public void SetShaderFloat(string parameter, float value)
        {
            try
            {
                _Stroke.SetShaderFloat(parameter, value);
            }
            catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
        }

        [LuaDocsDescription("Changes a shader color parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("color", "The new color")]
        [LuaDocsExample("myStroke:SetShaderColor(\"_TintColor\", Color.red)")]
        public void SetShaderColor(string parameter, ColorApiWrapper color)
        {
            try
            {
                _Stroke.SetShaderColor(parameter, color);
            }
            catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
        }

        [LuaDocsDescription("Changes a shader texture parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("image", "The new image to use as a texture")]
        [LuaDocsExample("myStroke:SetShaderTexture(\"_MainTex\", myImage)")]
        public void SetShaderTexture(string parameter, ImageApiWrapper image)
        {
            var texture = image._ImageWidget.ReferenceImage.FullSize;
            try
            {
                _Stroke.SetShaderTexture(parameter, texture);
            }
            catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
        }

        [LuaDocsDescription("Changes a shader vector parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("x", "The new x value")]
        [LuaDocsParameter("y", "The new y value")]
        [LuaDocsParameter("z", "The new z value")]
        [LuaDocsParameter("w", "The new w value")]
        [LuaDocsExample("myStroke:SetShaderVector(\"_TimeOverrideValue\", 0.5, 0, 0, 0)")]
        public void SetShaderVector(string parameter, float x, float y = 0, float z = 0, float w = 0)
        {
            try
            {
                _Stroke.SetShaderVector(parameter, x, y, z, w);
            }
            catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
        }
    }
}
