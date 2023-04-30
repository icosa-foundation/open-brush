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
    // From https://gist.github.com/Kryzarel

    [MoonSharpUserData]
    public static class EasingApiWrapper
    {
        public static float linear(float t) => t;

        public static float inQuad(float t) => t * t;
        public static float outQuad(float t) => 1 - inQuad(1 - t);
        public static float inOutQuad(float t)
        {
            if (t < 0.5) return inQuad(t * 2) / 2;
            return 1 - inQuad((1 - t) * 2) / 2;
        }

        public static float inCubic(float t) => t * t * t;
        public static float outCubic(float t) => 1 - inCubic(1 - t);
        public static float inOutCubic(float t)
        {
            if (t < 0.5) return inCubic(t * 2) / 2;
            return 1 - inCubic((1 - t) * 2) / 2;
        }

        public static float inQuart(float t) => t * t * t * t;
        public static float outQuart(float t) => 1 - inQuart(1 - t);
        public static float inOutQuart(float t)
        {
            if (t < 0.5) return inQuart(t * 2) / 2;
            return 1 - inQuart((1 - t) * 2) / 2;
        }

        public static float inQuint(float t) => t * t * t * t * t;
        public static float outQuint(float t) => 1 - inQuint(1 - t);
        public static float inOutQuint(float t)
        {
            if (t < 0.5) return inQuint(t * 2) / 2;
            return 1 - inQuint((1 - t) * 2) / 2;
        }

        public static float inSine(float t) => -Mathf.Cos(t * Mathf.PI / 2);
        public static float outSine(float t) => Mathf.Sin(t * Mathf.PI / 2);
        public static float inOutSine(float t) => (Mathf.Cos(t * Mathf.PI) - 1) / -2;

        public static float inExpo(float t) => Mathf.Pow(2, 10 * (t - 1));
        public static float outExpo(float t) => 1 - inExpo(1 - t);
        public static float inOutExpo(float t)
        {
            if (t < 0.5) return inExpo(t * 2) / 2;
            return 1 - inExpo((1 - t) * 2) / 2;
        }

        public static float inCirc(float t) => -(Mathf.Sqrt(1 - t * t) - 1);
        public static float outCirc(float t) => 1 - inCirc(1 - t);
        public static float inOutCirc(float t)
        {
            if (t < 0.5) return inCirc(t * 2) / 2;
            return 1 - inCirc((1 - t) * 2) / 2;
        }

        public static float inElastic(float t) => 1 - outElastic(1 - t);
        public static float outElastic(float t)
        {
            float p = 0.3f;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - p / 4) * (2 * Mathf.PI) / p) + 1;
        }
        public static float inOutElastic(float t)
        {
            if (t < 0.5) return inElastic(t * 2) / 2;
            return 1 - inElastic((1 - t) * 2) / 2;
        }

        public static float inBack(float t)
        {
            float s = 1.70158f;
            return t * t * ((s + 1) * t - s);
        }
        public static float outBack(float t) => 1 - inBack(1 - t);
        public static float inOutBack(float t)
        {
            if (t < 0.5) return inBack(t * 2) / 2;
            return 1 - inBack((1 - t) * 2) / 2;
        }

        public static float inBounce(float t) => 1 - outBounce(1 - t);
        public static float outBounce(float t)
        {
            float div = 2.75f;
            float mult = 7.5625f;

            if (t < 1 / div)
            {
                return mult * t * t;
            }
            else if (t < 2 / div)
            {
                t -= 1.5f / div;
                return mult * t * t + 0.75f;
            }
            else if (t < 2.5 / div)
            {
                t -= 2.25f / div;
                return mult * t * t + 0.9375f;
            }
            else
            {
                t -= 2.625f / div;
                return mult * t * t + 0.984375f;
            }
        }
        public static float inOutBounce(float t)
        {
            if (t < 0.5) return inBounce(t * 2) / 2;
            return 1 - inBounce((1 - t) * 2) / 2;
        }
    }
}
