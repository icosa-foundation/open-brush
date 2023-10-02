using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    public struct PlayerRigData
    {
        public Vector3 HeadPosition;
        public Quaternion HeadRotation;
        public ExtraData ExtraData;
    }

    public struct ExtraData
    {
        public ulong OculusPlayerId;
    }
}