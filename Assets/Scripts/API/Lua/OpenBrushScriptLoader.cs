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
using System.Threading;

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
            if (TryGetStorageRelativePath(name, out string relativePath))
            {
                return UserStorage.Backend.Exists(StorageArea.Plugins, relativePath);
            }
            if (!TryGetSafeModulePath(name, out string path))
            {
                return false;
            }
            return File.Exists(path);
        }

        public override object LoadFile(string file, Table globalContext)
        {
            // MoonSharp's loader contract is stream-based - LoadFile returns a Stream - so a
            // module can be read straight out of shared storage. The path this receives is only
            // how the default implementation happened to obtain one.
            if (TryGetStorageRelativePath(file, out string relativePath))
            {
                return UserStorage.Backend.OpenRead(
                    StorageArea.Plugins, relativePath, requireSeekable: false, CancellationToken.None);
            }
            if (!TryGetSafeModulePath(file, out string path))
            {
                throw new ArgumentException($"Invalid Lua module path: {file}");
            }
            FileStream result = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return result;
        }

        /// Maps a module path produced from ModulePaths onto a path relative to the Plugins area,
        /// for backends that have no filesystem to resolve against. False means "use the ordinary
        /// filesystem route", which is every non-SAF platform and the test override.
        private bool TryGetStorageRelativePath(string path, out string relativePath)
        {
            relativePath = null;
            if (m_ModuleRootOverride != null ||
                UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string pluginsRoot = LuaManager.Instance?.UserPluginsPath();
            if (string.IsNullOrEmpty(pluginsRoot))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            string root = Path.GetFullPath(pluginsRoot).Replace('\\', '/').TrimEnd('/');
            string full = Path.IsPathRooted(path)
                ? Path.GetFullPath(path).Replace('\\', '/')
                : $"{root}/{normalized.TrimStart('/')}";

            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!full.StartsWith(root + "/", comparison))
            {
                return false;
            }

            relativePath = full.Substring(root.Length + 1);
            return !string.IsNullOrEmpty(relativePath) && !relativePath.Contains("..");
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
