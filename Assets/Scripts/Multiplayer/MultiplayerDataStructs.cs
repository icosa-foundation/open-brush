using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    [System.Serializable]
    public struct PlayerRigData
    {
        public Vector3 HeadPosition;
        public Quaternion HeadRotation;
        public Vector3 HeadScale;

        public Vector3 ToolPosition;
        public Quaternion ToolRotation;

        public BrushData BrushData;
        public ExtraData ExtraData;
        
    }

    [System.Serializable]
    public struct BrushData
    {
        public Color Color;
        public string Guid;
        public float Size; 
    }

    [System.Serializable]
    public struct ExtraData
    {
        public ulong OculusPlayerId;
    }
}