// Copyright 2023 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public static class RandomApiWrapper
    {
        public static Vector2 insideUnitCircle => Random.insideUnitCircle;
        public static Vector3 insideUnitSphere => Random.insideUnitSphere;
        public static Vector3 onUnitSphere => Random.onUnitSphere;
        public static Quaternion rotation => Random.rotation;
        public static Quaternion rotationUniform => Random.rotationUniform;
        public static float value => Random.value;
        public static Color color => Random.ColorHSV();
        public static Color ColorHSV(
            float hueMin, float hueMax,
            float saturationMin, float saturationMax,
            float valueMin, float valueMax)
        {
            return Random.ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax);
        }
        public static void InitState(int seed) => Random.InitState(seed);
        public static float Range(int min, int max) => Random.Range(min, max);
        public static float Range(float min, float max) => Random.Range(min, max);
    }
}
