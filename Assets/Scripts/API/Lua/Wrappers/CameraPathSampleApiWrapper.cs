﻿using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A sampled point on a camera path")]
    [MoonSharpUserData]
    public class CameraPathSampleApiWrapper
    {
        [MoonSharpHidden] public TrTransform _Transform;

        [LuaDocsDescription("The time used to sample the camera path")]
        public float time { get; }

        [LuaDocsDescription("The sampled transform")]
        public TrTransform transform => _Transform;

        [LuaDocsDescription("The sampled position")]
        public Vector3 position => _Transform.translation;

        [LuaDocsDescription("The sampled rotation")]
        public Quaternion rotation => _Transform.rotation;

        [LuaDocsDescription("The sampled camera speed")]
        public float speed { get; }

        [LuaDocsDescription("The sampled camera field of view")]
        public float fov { get; }

        [LuaDocsDescription("The sampled position along the path as a ratio from 0 to 1")]
        public float pathRatio { get; }

        public CameraPathSampleApiWrapper(float time, TrTransform transform, float speed, float fov, float pathRatio)
        {
            this.time = time;
            _Transform = transform;
            this.speed = speed;
            this.fov = fov;
            this.pathRatio = pathRatio;
        }
    }
}
