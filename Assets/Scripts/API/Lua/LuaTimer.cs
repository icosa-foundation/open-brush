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
    public class LuaTimer
    {
        public Closure m_Fn;
        public float m_Interval;
        public float m_Delay;
        public int m_Repeats;
        public float m_TimeLastRun;
        public int m_CallCount;
        public float m_TimeAdded;

        public LuaTimer(Closure fn, float interval, float delay, int repeats)
        {
            m_Fn = fn;
            m_Interval = interval;
            m_Delay = delay;
            m_Repeats = repeats;
            m_TimeAdded = Time.time;
        }
    }
}
