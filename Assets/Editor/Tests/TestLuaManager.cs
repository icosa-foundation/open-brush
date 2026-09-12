// Copyright 2026 The Open Brush Authors
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

using System.Collections.Generic;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestLuaManager
    {
        [Test]
        public void FindFirstDrawableToolScriptPathIndexSkipsShortPaths()
        {
            var paths = new List<List<TrTransform>>
            {
                null,
                new(),
                new() { TrTransform.identity },
                new() { TrTransform.identity, TrTransform.identity },
                new() { TrTransform.identity, TrTransform.identity, TrTransform.identity }
            };

            Assert.AreEqual(4, LuaManager.FindFirstDrawableToolScriptPathIndex(paths));
        }

        [Test]
        public void FindFirstDrawableToolScriptPathIndexReturnsMinusOneWithoutDrawablePath()
        {
            var paths = new List<List<TrTransform>>
            {
                new(),
                new() { TrTransform.identity, TrTransform.identity }
            };

            Assert.AreEqual(-1, LuaManager.FindFirstDrawableToolScriptPathIndex(paths));
            Assert.AreEqual(-1, LuaManager.FindFirstDrawableToolScriptPathIndex(null));
        }
    }
}
