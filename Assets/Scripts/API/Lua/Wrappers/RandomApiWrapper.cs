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
    [LuaDocsDescription("Various functions for generating random data")]
    [MoonSharpUserData]
    public static class RandomApiWrapper
    {
        [LuaDocsDescription("Returns a random 2d point inside a circle of radius 1")]
        public static Vector2 insideUnitCircle => Random.insideUnitCircle;

        [LuaDocsDescription("Returns a random 3d point inside a sphere of radius 1")]
        public static Vector3 insideUnitSphere => Random.insideUnitSphere;

        [LuaDocsDescription("Returns a random 3d point on the surface of a sphere of radius 1")]
        public static Vector3 onUnitSphere => Random.onUnitSphere;

        [LuaDocsDescription("Returns a random rotation")]
        public static Quaternion rotation => Random.rotation;

        [LuaDocsDescription("Returns a random rotation with uniform distribution")]
        public static Quaternion rotationUniform => Random.rotationUniform;

        [LuaDocsDescription("Returns a random number between 0 and 1")]
        public static float value => Random.value;

        [LuaDocsDescription("Returns a random color")]
        public static Color color => Random.ColorHSV();

        [LuaDocsDescription("Returns a random color within given ranges")]
        [LuaDocsExample(@"myColor = Random:ColorHSV(0, 1, 0.8, 1, 0.5, 1)")]
        [LuaDocsParameter("hueMin", "Minimum hue")]
        [LuaDocsParameter("hueMax", "Maximum hue")]
        [LuaDocsParameter("saturationMin", "Minimum saturation")]
        [LuaDocsParameter("saturationMax", "Maximum saturation")]
        [LuaDocsParameter("valueMin", "Minimum brightness")]
        [LuaDocsParameter("valueMax", "Maximum brightness")]
        [LuaDocsReturnValue("The new random color")]
        public static Color ColorHSV(
            float hueMin, float hueMax,
            float saturationMin, float saturationMax,
            float valueMin, float valueMax)
        {
            return Random.ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax);
        }

        [LuaDocsDescription("Initializes the random number generator with a specified seed")]
        [LuaDocsExample(@"Random:InitState(seed)")]
        [LuaDocsParameter("seed", "The seed for the random number generator")]
        public static void InitState(int seed) => Random.InitState(seed);

        [LuaDocsDescription("Returns a random float number between min and max (inclusive")]
        [LuaDocsExample(@"value = Random:Range(1, 6)")]
        [LuaDocsParameter("min", "Minimum value")]
        [LuaDocsParameter("max", "Maximum value")]
        [LuaDocsReturnValue("A random whole number >= min and <= max")]
        public static int Range(int min, int max) => Random.Range(min, max);

        [LuaDocsDescription("Returns a random float number between min and max")]
        [LuaDocsExample(@"value = Random:Range(-1, 1)")]
        [LuaDocsParameter("min", "Minimum value")]
        [LuaDocsParameter("max", "Maximum value")]
        [LuaDocsReturnValue("The random number  >= min and <= max")]
        public static float Range(float min, float max) => Random.Range(min, max);
    }
}