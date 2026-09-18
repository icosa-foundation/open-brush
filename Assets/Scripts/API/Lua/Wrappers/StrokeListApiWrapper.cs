using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The list of Strokes in the scene. (You don't instantiate this yourself. Access this via Sketch.strokes)")]
    [MoonSharpUserData]
    public class StrokeListApiWrapper
    {
        [MoonSharpHidden]
        public List<Stroke> _Strokes;

        [LuaDocsDescription("Returns the last stroke that was selected")]
        public StrokeApiWrapper lastSelected
        {
            get
            {
                Stroke stroke = SelectionManager.m_Instance.LastSelectedStroke;
                return stroke == null ? null : new StrokeApiWrapper(stroke);
            }
        }

        [LuaDocsDescription("Returns the last Stroke")]
        public StrokeApiWrapper last => _Strokes == null || _Strokes.Count == 0 ? null : new StrokeApiWrapper(_Strokes[^1]);

        [LuaDocsDescription("Returns the Stroke at the given index")]
        public StrokeApiWrapper this[int index] => new(Utils.WrappedIndexerGet(() => _Strokes[index]));

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

        [LuaDocsDescription("Adds these strokes to the current selection")]
        [LuaDocsExample("myStrokes:Select()")]
        public void Select()
        {
            SelectionManager.m_Instance.SelectStrokes(_Strokes);
        }

        [LuaDocsDescription("Removes these strokes from the current selection")]
        [LuaDocsExample("myStrokes:Deselect()")]
        public void Deselect()
        {
            SelectionManager.m_Instance.DeselectStrokes(_Strokes);
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

        [LuaDocsDescription("Crops these strokes to a sphere and updates this list with the remaining strokes")]
        [LuaDocsParameter("center", "The center of the sphere")]
        [LuaDocsParameter("radius", "The radius of the sphere")]
        [LuaDocsExample("Sketch.strokes:CropSphere(Vector3:New(0, 10, 0), 7.5)")]
        [LuaDocsParameter("keepInside", "True keeps the inside; false keeps the outside (default true)")]
        public void CropSphere(Vector3 center, float radius, bool keepInside = true)
        {
            var pose = App.Scene.Pose;
            _Strokes = StrokeCropping.CropStrokesToSphere(pose * center, pose.scale * radius, _Strokes, keepInside);
        }

        [LuaDocsDescription("Crops these strokes to a box and updates this list with the remaining strokes")]
        [LuaDocsParameter("center", "The center of the box")]
        [LuaDocsParameter("size", "The full width, height and depth")]
        [LuaDocsParameter("rotation", "Optional Euler rotation in degrees")]
        [LuaDocsExample("Sketch.strokes:CropBox(Vector3.zero, Vector3:New(10, 10, 10))")]
        [LuaDocsParameter("keepInside", "True keeps the inside; false keeps the outside (default true)")]
        public void CropBox(Vector3 center, Vector3 size, Vector3 rotation = default, bool keepInside = true)
        {
            var pose = App.Scene.Pose;
            _Strokes = StrokeCropping.CropStrokesToBox(pose * center, pose.scale * size,
                pose.rotation * Quaternion.Euler(rotation), _Strokes, keepInside);
        }

        [LuaDocsDescription("Crops these strokes to a capsule and updates this list with the remaining strokes. Its axis is Y before rotation")]
        [LuaDocsParameter("center", "The center of the capsule")]
        [LuaDocsParameter("radius", "The radius of the capsule")]
        [LuaDocsParameter("height", "The full height of the capsule")]
        [LuaDocsParameter("rotation", "Optional Euler rotation in degrees")]
        [LuaDocsExample("Sketch.strokes:CropCapsule(Vector3.zero, 5, 10)")]
        [LuaDocsParameter("keepInside", "True keeps the inside; false keeps the outside (default true)")]
        public void CropCapsule(Vector3 center, float radius, float height, Vector3 rotation = default, bool keepInside = true)
        {
            var pose = App.Scene.Pose;
            _Strokes = StrokeCropping.CropStrokesToCapsule(pose * center, pose.scale * radius,
                pose.scale * height, pose.rotation * Quaternion.Euler(rotation), _Strokes, keepInside);
        }

        [LuaDocsDescription("Crops these strokes to an ellipsoid and updates this list with the remaining strokes")]
        [LuaDocsParameter("center", "The center of the ellipsoid")]
        [LuaDocsParameter("size", "The full width, height and depth")]
        [LuaDocsParameter("rotation", "Optional Euler rotation in degrees")]
        [LuaDocsExample("Sketch.strokes:CropEllipsoid(Vector3.zero, Vector3:New(10, 20, 10))")]
        [LuaDocsParameter("keepInside", "True keeps the inside; false keeps the outside (default true)")]
        public void CropEllipsoid(Vector3 center, Vector3 size, Vector3 rotation = default, bool keepInside = true)
        {
            var pose = App.Scene.Pose;
            _Strokes = StrokeCropping.CropStrokesToEllipsoid(pose * center, pose.scale * size,
                pose.rotation * Quaternion.Euler(rotation), _Strokes, keepInside);
        }

        [LuaDocsDescription("Keeps the side of a plane its normal points toward and updates this list with the remaining strokes")]
        [LuaDocsParameter("point", "A point on the plane")]
        [LuaDocsParameter("normal", "The direction of the side to keep; reverse it to keep the other side")]
        [LuaDocsExample("Sketch.strokes:CropPlane(Vector3:New(0, 10, 0), Vector3.up)")]
        [LuaDocsParameter("keepInside", "True keeps the inside; false keeps the outside (default true)")]
        public void CropPlane(Vector3 point, Vector3 normal, bool keepInside = true)
        {
            var pose = App.Scene.Pose;
            _Strokes = StrokeCropping.CropStrokesToPlane(pose * point, pose.rotation * normal, _Strokes, keepInside);
        }

        [LuaDocsDescription("Hides the section of the stroke that is outside the specified range")]
        [LuaDocsParameter("clipStart", "The amount of the stroke to hide from the start (0-1)")]
        [LuaDocsParameter("clipEnd", "The amount of the stroke to hide from the end (0-1)")]
        [LuaDocsExample("myStroke:SetShaderClipping(0.1, 0.9)")]
        public void SetShaderClipping(float clipStart, float clipEnd)
        {
            foreach (var stroke in _Strokes)
            {
                stroke.SetShaderClipping(clipStart, clipEnd);
            }
        }

        [LuaDocsDescription("Changes a shader float parameter.")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("value", "The new value")]
        [LuaDocsExample("myStrokes:SetShaderFloat(\"_EmissionGain\", 0.5)")]
        public void SetShaderFloat(string parameter, float value)
        {
            foreach (var stroke in _Strokes)
            {
                try
                {
                    stroke.SetShaderFloat(parameter, value);
                }
                catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
            }
        }

        [LuaDocsDescription("Changes a shader color parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("color", "The new color")]
        [LuaDocsExample("myStrokes:SetShaderColor(\"_TintColor\", Color.red)")]
        public void SetShaderColor(string parameter, ColorApiWrapper color)
        {
            foreach (var stroke in _Strokes)
            {
                try
                {
                    stroke.SetShaderColor(parameter, color);
                }
                catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
            }
        }

        [LuaDocsDescription("Changes a shader texture parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("image", "The new image to use as a texture")]
        [LuaDocsExample("myStrokes:SetShaderTexture(\"_MainTex\", myImage)")]
        public void SetShaderTexture(string parameter, ImageApiWrapper image)
        {
            var texture = image._ImageWidget.ReferenceImage.FullSize;
            foreach (var stroke in _Strokes)
            {
                try
                {
                    stroke.SetShaderTexture(parameter, texture);
                }
                catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
            }
        }

        [LuaDocsDescription("Changes a shader vector parameter")]
        [LuaDocsParameter("parameter", "The shader parameter name")]
        [LuaDocsParameter("x", "The new x value")]
        [LuaDocsParameter("y", "The new y value")]
        [LuaDocsParameter("z", "The new z value")]
        [LuaDocsParameter("w", "The new w value")]
        [LuaDocsExample("myStrokes:SetShaderVector(\"_TimeOverrideValue\", 0.5, 0, 0, 0)")]
        public void SetShaderVector(string parameter, float x, float y = 0, float z = 0, float w = 0)
        {
            foreach (var stroke in _Strokes)
            {
                try
                {
                    stroke.SetShaderVector(parameter, x, y, z, w);
                }
                catch (StrokeShaderModifierException e) { LuaManager.LogLuaError(e); }
            }
        }
    }
}
