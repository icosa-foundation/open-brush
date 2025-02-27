using System;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("An independently controllable brush that can paint independently of user actions")]
    [MoonSharpUserData]
    public class PointerApiWrapper
    {
        public PointerScript _Pointer;
        private bool _IsDrawing;
        private bool _WasDrawing;
        private CanvasScript _Canvas;
        private BrushDescriptor _Brush;
        private Color _Color;
        private float _Size;
        private float _Pressure;

        public PointerApiWrapper(PointerScript pointer)
        {
            _Pointer = pointer;
            _Canvas = App.ActiveCanvas;
            _Brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            _Color = PointerManager.m_Instance.MainPointer.GetCurrentColor();
            _Size = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
            pointer.SetBrush(_Brush);
            pointer.SetColor(_Color);
            pointer.BrushSizeAbsolute = _Size;
            pointer.SetPressure(_Pressure);
        }

        [LuaDocsDescription(@"Creates a new pointer for drawing")]
        [LuaDocsExample(@"Pointer:New()")]
        [LuaDocsReturnValue(@"The new pointer")]
        public static PointerApiWrapper New()
        {
            var pointer = PointerManager.m_Instance.CreateScriptedPointer();
            return new PointerApiWrapper(pointer);
        }

        public override string ToString()
        {
            return $"Pointer({_Pointer})";
        }

        [LuaDocsDescription(@"True if the pointer is currently drawing a stroke, otherwise false")]
        public bool isDrawing
        {
            get
            {
                return _IsDrawing;
            }
            set
            {
                _IsDrawing = value;
                _UpdateDrawingState();
            }
        }

        [LuaDocsDescription(@"Sets the layer that the pointer will draw on. Must be set before starting a new stroke")]
        public LayerApiWrapper layer
        {
            get
            {
                return new LayerApiWrapper(_Canvas);
            }
            set
            {
                var previousCanvas = _Canvas;
                _Canvas = value._CanvasScript;
                if (previousCanvas != _Canvas)
                {
                    _Pointer.CreateNewLine(_Canvas, _Canvas.AsCanvas[_Pointer.transform], null);
                }
            }
        }

        [LuaDocsDescription(@"Sets the color of the strokes created by this pointer. Must be set before starting a new stroke")]
        public ColorApiWrapper color
        {
            get
            {
                return new ColorApiWrapper(_Color);
            }
            set
            {
                var previousColor = _Color;
                _Color = value._Color;
                if (previousColor != _Color)
                {
                    _Pointer.CreateNewLine(_Canvas, _Canvas.AsCanvas[_Pointer.transform], null);
                }
            }
        }

        [LuaDocsDescription(@"Sets the brush type the pointer will draw. Must be set before starting a new stroke")]
        public string brush
        {
            get
            {
                return _Brush.Description;
            }
            set
            {
                var previousBrush = _Brush.Description;
                _Brush = ApiMethods.LookupBrushDescriptor(value);
                if (previousBrush != value)
                {
                    _Pointer.CreateNewLine(_Canvas, _Canvas.AsCanvas[_Pointer.transform], null);
                }
            }
        }

        [LuaDocsDescription(@"Sets the size of the brush strokes this pointer will draw. Must be set before starting a new stroke")]
        public float size
        {
            get
            {
                return _Size;
            }
            set
            {
                var previousSize = _Size;
                _Size = value;
                if (Math.Abs(previousSize - _Size) > .0000001f)
                {
                    _Pointer.CreateNewLine(_Canvas, _Canvas.AsCanvas[_Pointer.transform], null);
                }
            }
        }

        [LuaDocsDescription(@"Sets the pressure of the stroke being drawn")]
        public float pressure
        {
            get => _Pressure;
            set
            {
                _Pressure = value;
                _Pointer.SetPressure(_Pressure);
            }
        }

        private void _UpdateDrawingState()
        {
            if (_IsDrawing && !_WasDrawing)
            {
                _Pointer.CreateNewLine(_Canvas, _Canvas.AsCanvas[_Pointer.transform], null);
            }
            else if (!_IsDrawing && _WasDrawing)
            {
                _Pointer.DetachLine(false, null);
            }
            _WasDrawing = _IsDrawing;
        }

        [LuaDocsDescription(@"The position and orientation of the pointer")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_Pointer.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_Pointer.transform] = value;
            }
        }

        [LuaDocsDescription("The 3D position of this pointer")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription("The 3D orientation of the Pointer")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }
    }
}
