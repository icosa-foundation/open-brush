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
    [LuaDocsDescription("Each easing function takes a value between 0 and 1 and modifies it to speed up or slow down at either end")]
    [MoonSharpUserData]
    public static class EasingApiWrapper
    {
        [LuaDocsDescription("Linear easing function")]
        [LuaDocsExample("value = Easing:Linear(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The input is returned unchanged")]
        public static float Linear(float t) => t;

        [LuaDocsDescription("InQuad easing function")]
        [LuaDocsExample("value = Easing:InQuad(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InQuad(float t) => t * t;

        [LuaDocsDescription("OutQuad easing function")]
        [LuaDocsExample("value = Easing:OutQuad(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutQuad(float t) => 1 - InQuad(1 - t);

        [LuaDocsDescription("InOutQuad easing function")]
        [LuaDocsExample("value = Easing:InOutQuad(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutQuad(float t)
        {
            if (t < 0.5) return InQuad(t * 2) / 2;
            return 1 - InQuad((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InCubic easing function")]
        [LuaDocsExample("value = Easing:InCubic(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InCubic(float t) => t * t * t;

        [LuaDocsDescription("OutCubic easing function")]
        [LuaDocsExample("value = Easing:OutCubic(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutCubic(float t) => 1 - InCubic(1 - t);

        [LuaDocsDescription("InOutCubic easing function")]
        [LuaDocsExample("value = Easing:InOutCubic(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutCubic(float t)
        {
            if (t < 0.5) return InCubic(t * 2) / 2;
            return 1 - InCubic((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InQuart easing function")]
        [LuaDocsExample("value = Easing:InQuart(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InQuart(float t) => t * t * t * t;

        [LuaDocsDescription("OutQuart easing function")]
        [LuaDocsExample("value = Easing:OutQuart(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutQuart(float t) => 1 - InQuart(1 - t);

        [LuaDocsDescription("InQuart easing function")]
        [LuaDocsExample("value = Easing:InQuart(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutQuart(float t)
        {
            if (t < 0.5) return InQuart(t * 2) / 2;
            return 1 - InQuart((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InQuint easing function")]
        [LuaDocsExample("value = Easing:InQuint(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InQuint(float t) => t * t * t * t * t;

        [LuaDocsDescription("OutQuint easing function")]
        [LuaDocsExample("value = Easing:OutQuint(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutQuint(float t) => 1 - InQuint(1 - t);

        [LuaDocsDescription("InOutQuint easing function")]
        [LuaDocsExample("value = Easing:InOutQuint(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutQuint(float t)
        {
            if (t < 0.5) return InQuint(t * 2) / 2;
            return 1 - InQuint((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InSine easing function")]
        [LuaDocsExample("value = Easing:InSine(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InSine(float t) => -Mathf.Cos(t * Mathf.PI / 2);

        [LuaDocsDescription("OutSine easing function")]
        [LuaDocsExample("value = Easing:OutSine(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutSine(float t) => Mathf.Sin(t * Mathf.PI / 2);

        [LuaDocsDescription("InOutSine easing function")]
        [LuaDocsExample("value = Easing:InOutSine(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutSine(float t) => (Mathf.Cos(t * Mathf.PI) - 1) / -2;

        [LuaDocsDescription("InExpo easing function")]
        [LuaDocsExample("value = Easing:InExpo(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InExpo(float t) => Mathf.Pow(2, 10 * (t - 1));

        [LuaDocsDescription("OutExpo easing function")]
        [LuaDocsExample("value = Easing:OutExpo(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutExpo(float t) => 1 - InExpo(1 - t);

        [LuaDocsDescription("InOutExpo easing function")]
        [LuaDocsExample("value = Easing:InOutExpo(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutExpo(float t)
        {
            if (t < 0.5) return InExpo(t * 2) / 2;
            return 1 - InExpo((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InCirc easing function")]
        [LuaDocsExample("value = Easing:InCirc(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InCirc(float t) => -(Mathf.Sqrt(1 - t * t) - 1);

        [LuaDocsDescription("OutCirc easing function")]
        [LuaDocsExample("value = Easing:OutCirc(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutCirc(float t) => 1 - InCirc(1 - t);

        [LuaDocsDescription("InOutCirc easing function")]
        [LuaDocsExample("value = Easing:InOutCirc(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutCirc(float t)
        {
            if (t < 0.5) return InCirc(t * 2) / 2;
            return 1 - InCirc((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InElastic easing function")]
        [LuaDocsExample("value = Easing:InElastic(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InElastic(float t) => 1 - OutElastic(1 - t);

        [LuaDocsDescription("OutElastic easing function")]
        [LuaDocsExample("value = Easing:OutElastic(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutElastic(float t)
        {
            float p = 0.3f;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - p / 4) * (2 * Mathf.PI) / p) + 1;
        }

        [LuaDocsDescription("InOutElastic easing function")]
        [LuaDocsExample("value = Easing:InOutElastic(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutElastic(float t)
        {
            if (t < 0.5) return InElastic(t * 2) / 2;
            return 1 - InElastic((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InBack easing function")]
        [LuaDocsExample("value = Easing:InBack(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InBack(float t)
        {
            float s = 1.70158f;
            return t * t * ((s + 1) * t - s);
        }

        [LuaDocsDescription("OutBack easing function")]
        [LuaDocsExample("value = Easing:OutBack(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutBack(float t) => 1 - InBack(1 - t);

        [LuaDocsDescription("InOutBack easing function")]
        [LuaDocsExample("value = Easing:InOutBack(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutBack(float t)
        {
            if (t < 0.5) return InBack(t * 2) / 2;
            return 1 - InBack((1 - t) * 2) / 2;
        }

        [LuaDocsDescription("InBounce easing function")]
        [LuaDocsExample("value = Easing:InBounce(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the in direction only")]
        public static float InBounce(float t) => 1 - OutBounce(1 - t);

        [LuaDocsDescription("OutBounce easing function")]
        [LuaDocsExample("value = Easing:OutBounce(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in the out direction only")]
        public static float OutBounce(float t)
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

        [LuaDocsDescription("InOutBounce easing function")]
        [LuaDocsExample("value = Easing:InOutBounce(value)")]
        [LuaDocsParameter("t", "The input value between 0 and 1")]
        [LuaDocsReturnValue("The value smoothed in and out")]
        public static float InOutBounce(float t)
        {
            if (t < 0.5) return InBounce(t * 2) / 2;
            return 1 - InBounce((1 - t) * 2) / 2;
        }
    }
}
