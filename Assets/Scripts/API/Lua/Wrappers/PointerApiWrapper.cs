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

        public static PointerApiWrapper New()
        {
            var pointer = PointerManager.m_Instance.CreateScriptedPointer();
            return new PointerApiWrapper(pointer);
        }

        public override string ToString()
        {
            return $"Pointer({_Pointer})";
        }

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

        public float pressure
        {
            get => _Pressure;
            set => _Pressure = value;
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

        public TrTransform transform
        {
            get =>  App.Scene.MainCanvas.AsCanvas[_Pointer.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_Pointer.transform] = value;
            }
        }

        public Vector3 position
        {
            get => transform.translation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.T(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.translation = newTransform.translation;
                transform = tr_CS;
            }
        }

        public Quaternion rotation
        {
            get => transform.rotation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.R(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.rotation = newTransform.rotation;
                transform = tr_CS;
            }
        }

        public float scale
        {
            get => transform.scale;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.S(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.scale = newTransform.scale;
                transform = tr_CS;
            }
        }
    }
}
