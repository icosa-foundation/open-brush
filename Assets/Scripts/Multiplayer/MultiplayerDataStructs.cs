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
        public ExtraData ExtraData;
    }

    [System.Serializable]
    public struct ExtraData
    {
        public ulong OculusPlayerId;
    }
}