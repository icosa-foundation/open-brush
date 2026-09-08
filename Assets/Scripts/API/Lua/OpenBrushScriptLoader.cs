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
using MoonSharp.Interpreter.Loaders;
using System;
using System.IO;

namespace TiltBrush
{
    public class OpenBrushScriptLoader : ScriptLoaderBase
    {
        private readonly string m_ModuleRootOverride;

        public OpenBrushScriptLoader()
        {
        }

        internal OpenBrushScriptLoader(string moduleRootOverride)
        {
            m_ModuleRootOverride = moduleRootOverride;
        }

        public override bool ScriptFileExists(string name)
        {
            if (!TryGetSafeModulePath(name, out string path))
            {
                return false;
            }
            return File.Exists(path);
        }

        public override object LoadFile(string file, Table globalContext)
        {
            if (!TryGetSafeModulePath(file, out string path))
            {
                throw new ArgumentException($"Invalid Lua module path: {file}");
            }
            FileStream result = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return result;
        }

        private bool TryGetSafeModulePath(string path, out string fullPath)
        {
            return TryGetSafeModulePath(
                path,
                m_ModuleRootOverride ?? LuaManager.Instance.LuaModulesPath,
                out fullPath);
        }

        internal static bool TryGetSafeModulePath(string path, string moduleRoot, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(moduleRoot))
            {
                return false;
            }

            string fullModuleRoot = Path.GetFullPath(moduleRoot);
            string resolvedPath = Path.GetFullPath(
                Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(fullModuleRoot, path));
            string moduleRootWithSeparator = fullModuleRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!resolvedPath.StartsWith(moduleRootWithSeparator, pathComparison))
            {
                return false;
            }

            fullPath = resolvedPath;
            return true;
        }
    }
}
