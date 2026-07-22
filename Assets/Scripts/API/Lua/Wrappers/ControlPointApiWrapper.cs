using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A brush control point emitted by a Tool Script preview")]
    [MoonSharpUserData]
    public class ControlPointApiWrapper
    {
        [MoonSharpHidden]
        public PointerManager.ControlPoint _ControlPoint;

        public ControlPointApiWrapper(PointerManager.ControlPoint controlPoint)
        {
            _ControlPoint = controlPoint;
        }

        [LuaDocsDescription("The position of the control point")]
        public Vector3 position => _ControlPoint.m_Pos;

        [LuaDocsDescription("The orientation of the control point")]
        public Quaternion rotation => _ControlPoint.m_Orient;

        [LuaDocsDescription("The pressure associated with the control point")]
        public float pressure => _ControlPoint.m_Pressure;

        [LuaDocsDescription("The timestamp in milliseconds for this control point")]
        public uint timestampMs => _ControlPoint.m_TimestampMs;
    }

    [LuaDocsDescription("A list of brush control points")]
    [MoonSharpUserData]
    public class ControlPointListApiWrapper
    {
        [MoonSharpHidden]
        public List<PointerManager.ControlPoint> _ControlPoints { get; }

        public ControlPointListApiWrapper(IEnumerable<PointerManager.ControlPoint> controlPoints)
        {
            _ControlPoints = controlPoints?.ToList() ?? new List<PointerManager.ControlPoint>();
        }

        [LuaDocsDescription("Number of control points in the list")]
        public int count => _ControlPoints.Count;

        [LuaDocsDescription("Access a control point by index")]
        public ControlPointApiWrapper this[int index] => new(_ControlPoints[index]);

        [LuaDocsDescription("Enumerate the control points")]
        public IEnumerable<ControlPointApiWrapper> items => _ControlPoints.Select(cp => new ControlPointApiWrapper(cp));
    }
}
