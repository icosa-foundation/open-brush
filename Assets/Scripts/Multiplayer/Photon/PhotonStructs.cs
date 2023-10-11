
using System;
using UnityEngine;
using Fusion;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public struct NetworkedControlPoint : INetworkStruct
    {
        public Vector3 m_Pos;
        public Quaternion m_Orient;

        public const uint EXTENSIONS = (uint)(
                SketchWriter.ControlPointExtension.Pressure |
                SketchWriter.ControlPointExtension.Timestamp);
        public float m_Pressure;
        public uint m_TimestampMs; // CurrentSketchTime of creation, in milliseconds

        public NetworkedControlPoint Init(PointerManager.ControlPoint point)
        {
            m_Pos = point.m_Pos;
            m_Orient = point.m_Orient;
            m_Pressure = point.m_Pressure;
            m_TimestampMs = point.m_TimestampMs;

            return this;
        }

        internal static PointerManager.ControlPoint ToControlPoint(NetworkedControlPoint networkedControlPoint)
        {
            var point = new PointerManager.ControlPoint
            {
                m_Pos = networkedControlPoint.m_Pos,
                m_Orient = networkedControlPoint.m_Orient,
                m_Pressure = networkedControlPoint.m_Pressure,
                m_TimestampMs = networkedControlPoint.m_TimestampMs
            };

            return point;
        }
    }

    [System.Serializable]
    public struct NetworkedStroke : INetworkStruct
    {
        public const int k_MaxCapacity = 128;
        public Stroke.Type m_Type;
        [Networked][Capacity(k_MaxCapacity)] public NetworkArray<bool> m_ControlPointsToDrop => default;
        public Color m_Color;
        public Guid m_BrushGuid;
        // The room-space size of the brush when the stroke was laid down
        public float m_BrushSize;
        // The size of the pointer, relative to  when the stroke was laid down.
        // AKA, the "pointer to local" scale factor.
        // m_BrushSize * m_BrushScale = size in local/canvas space
        public float m_BrushScale;
        [Networked][Capacity(k_MaxCapacity)] public NetworkArray<NetworkedControlPoint> m_ControlPoints => default;

        // Use for determining length.
        public int m_ControlPointsCapacity;
        // Seed for deterministic pseudo-random numbers for geometry generation.
        // Not currently serialized.
        public int m_Seed;

        public static Stroke ToStroke(NetworkedStroke netStroke)
        {
            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = App.Scene.MainCanvas,
                m_BrushGuid = netStroke.m_BrushGuid,
                m_BrushScale = netStroke.m_BrushScale,
                m_BrushSize = netStroke.m_BrushSize,
                m_Color = netStroke.m_Color,
                m_Seed = netStroke.m_Seed,
                m_ControlPoints = new PointerManager.ControlPoint[netStroke.m_ControlPointsCapacity],
                m_ControlPointsToDrop = new bool[netStroke.m_ControlPointsCapacity]
            };

            for (int i = 0; i < netStroke.m_ControlPointsCapacity; ++i)
            {
                var point = NetworkedControlPoint.ToControlPoint(netStroke.m_ControlPoints[i]);
                stroke.m_ControlPoints[i] = point;
            }

            for (int i = 0; i < netStroke.m_ControlPointsCapacity; ++i)
            {
                stroke.m_ControlPointsToDrop[i] = netStroke.m_ControlPointsToDrop[i];
            }

            return stroke;
        }

        public NetworkedStroke Init(Stroke data)
        {
            m_Type = data.m_Type;
            m_BrushGuid = data.m_BrushGuid;
            m_BrushScale = data.m_BrushScale;
            m_BrushSize = data.m_BrushSize;
            m_Color = data.m_Color;
            m_Seed = data.m_Seed;

            m_ControlPointsCapacity = data.m_ControlPoints.Length;

            for(int i = 0; i < data.m_ControlPoints.Length; i++)
            {
                var point = new NetworkedControlPoint().Init(data.m_ControlPoints[i]);
                m_ControlPoints.Set(i, point);
            }

            for(int i = 0; i < data.m_ControlPointsToDrop.Length; i++)
            {
                m_ControlPointsToDrop.Set(i, data.m_ControlPointsToDrop[i]);
            }

            return this;
        }
    }
    
}