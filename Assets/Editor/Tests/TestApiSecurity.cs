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

using System;
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    internal class TestApiSecurity
    {
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("../evil.png")]
        [TestCase("..\\evil.png")]
        [TestCase("subdir/evil.png")]
        [TestCase("subdir\\evil.png")]
        public void TestSafeFilenameRejectsUnsafeNames(string filename)
        {
            Assert.Throws<ArgumentException>(
                () => ApiMethods.ValidateSafeFilename(filename, "test filename"));
        }

        [TestCase("image.png")]
        [TestCase("image.jpeg")]
        [TestCase("sketch.tilt")]
        [TestCase("Untitled_1")]
        public void TestSafeFilenameAcceptsPlainNames(string filename)
        {
            Assert.DoesNotThrow(
                () => ApiMethods.ValidateSafeFilename(filename, "test filename"));
        }

        [TestCase("image.png")]
        [TestCase("../evil.png")]
        [TestCase("..\\evil.png")]
        [TestCase("subdir/evil.png")]
        [TestCase("subdir\\evil.png")]
        public void TestSafePathInDirectoryStaysUnderBaseDirectory(string filename)
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "OpenBrushApiSecurityTests");

            if (filename == "image.png")
            {
                string safePath = ApiMethods.GetSafePathInDirectory(baseDirectory, filename, "test filename");
                Assert.AreEqual(Path.GetFullPath(Path.Combine(baseDirectory, filename)), safePath);
            }
            else
            {
                Assert.Throws<ArgumentException>(
                    () => ApiMethods.GetSafePathInDirectory(baseDirectory, filename, "test filename"));
            }
        }

        [TestCase("image.png", true)]
        [TestCase("subdir/image.png", true)]
        [TestCase("subdir\\image.png", true)]
        [TestCase("../evil.png", false)]
        [TestCase("subdir/../../evil.png", false)]
        public void TestSafeRelativePathInDirectoryEnforcesContainment(
            string relativePath, bool expectedSafe)
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "OpenBrushApiSecurityTests");
            if (expectedSafe)
            {
                string path = ApiMethods.GetSafeRelativePathInDirectory(
                    baseDirectory, relativePath, "test path");
                StringAssert.StartsWith(Path.GetFullPath(baseDirectory), path);
            }
            else
            {
                Assert.Throws<ArgumentException>(() =>
                    ApiMethods.GetSafeRelativePathInDirectory(
                        baseDirectory, relativePath, "test path"));
            }
        }

        [Test]
        public void TestSafeRelativePathInDirectoryRejectsRootedPath()
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "OpenBrushApiSecurityTests");
            string rootedPath = Path.Combine(Path.GetPathRoot(baseDirectory), "evil.png");
            Assert.Throws<ArgumentException>(() =>
                ApiMethods.GetSafeRelativePathInDirectory(
                    baseDirectory, rootedPath, "test path"));
        }

        [TestCase(
            "attachment; filename=../../Startup/update.png",
            "https://example.com/fallback.png",
            "update.png")]
        [TestCase(
            "attachment; filename=..\\..\\Startup\\update.png",
            "https://example.com/fallback.png",
            "update.png")]
        [TestCase(
            "attachment; filename*=UTF-8''folder%2Fimage%20name.png",
            "https://example.com/fallback.png",
            "image name.png")]
        [TestCase(null, "https://example.com/media/image.png", "image.png")]
        public void TestDownloadFilenameIsReducedToSafeLeafName(
            string contentDisposition, string url, string expectedFilename)
        {
            Assert.AreEqual(
                expectedFilename,
                ApiMethods.GetSafeDownloadFilename(new Uri(url), contentDisposition));
        }

        [Test]
        public void TestWebRequestUserAgentIdentifiesOpenBrush()
        {
            StringAssert.StartsWith("OpenBrush/", ApiManager.WebRequestUserAgent);
            StringAssert.Contains(
                "(https://openbrush.app/)", ApiManager.WebRequestUserAgent);
        }

        [Test]
        public void TestRandomPanoramaUsesStandardWikimediaThumbnail()
        {
            TextAsset example = Resources.Load<TextAsset>(
                "LuaScriptExamples/BackgroundScript.RandomPanorama");
            Assert.IsNotNull(example);
            Script script = LuaManager.CreatePluginScript();
            script.DoString(example.text);
            Assert.AreEqual(
                DataType.Function,
                script.Globals.Get("getWikimediaThumbnailUrl").Type);

            DynValue thumbnailUrl = script.DoString(@"
                return getWikimediaThumbnailUrl({
                    url = ""https://upload.wikimedia.org/wikipedia/commons/a/ab/Panorama.jpg"",
                    width = 5000
                })
            ");

            Assert.AreEqual(
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Panorama.jpg/3840px-Panorama.jpg",
                thumbnailUrl.String);
        }

        [TestCase("http://example.com/image.png", true)]
        [TestCase("https://example.com/image.png", true)]
        [TestCase("HTTPS://example.com/image.png", true)]
        [TestCase("image.png", false)]
        [TestCase("file:///tmp/image.png", false)]
        [TestCase(null, false)]
        public void TestHttpMediaLocationsAreIdentifiedForLuaNetworkGating(
            string location, bool expectedHttp)
        {
            Assert.AreEqual(expectedHttp, ApiMethods.IsHttpLocation(location));
        }

        [TestCase("https://example.com/resource", "GET", "json", true)]
        [TestCase("https://EXAMPLE.com/resource", "GET", "json", true)]
        [TestCase("https://example.com./resource", "GET", "json", true)]
        [TestCase("https://sub.example.com/resource", "GET", "json", false)]
        [TestCase("https://notexample.com/resource", "GET", "json", false)]
        [TestCase("https://example.com@evil.test/resource", "GET", "json", false)]
        [TestCase("https://example.com/resource", "POST", "json", false)]
        [TestCase("https://example.com/resource", "GET", "image", false)]
        [TestCase("not a URL", "GET", "json", false)]
        public void TestLuaNetworkRulesMatchExactHostMethodAndFileType(
            string url, string method, string fileType, bool expectedAllowed)
        {
            var rules = new[]
            {
                new UserConfig.PluginWebRequestRule
                {
                    Host = "example.com",
                    Methods = new[] { "GET" },
                    FileTypes = new[] { "json" }
                }
            };

            Assert.AreEqual(expectedAllowed, WebRequestApiWrapper.IsLuaNetworkAccessAllowed(
                url, method, rules, fileType));
        }

        [Test]
        public void TestAnyFileTypePermitsOperationSpecificFileTypes()
        {
            var rules = new[]
            {
                new UserConfig.PluginWebRequestRule
                {
                    Host = "example.com",
                    Methods = new[] { "GET" },
                    FileTypes = new[] { "any" }
                }
            };

            Assert.IsTrue(WebRequestApiWrapper.IsLuaNetworkAccessAllowed(
                "https://example.com/resource", "GET", rules, "image"));
            Assert.IsTrue(WebRequestApiWrapper.IsLuaNetworkAccessAllowed(
                "https://example.com/resource", "GET", rules, "video"));
        }

        [TestCase("application/json", "json", true)]
        [TestCase("application/problem+json; charset=utf-8", "json", true)]
        [TestCase("text/plain", "text", true)]
        [TestCase("text/xml", "text", false)]
        [TestCase("application/xml", "xml", true)]
        [TestCase("application/atom+xml", "xml", true)]
        [TestCase("image/jpeg", "image", true)]
        [TestCase("image/svg+xml; charset=utf-8", "image", true)]
        [TestCase("audio/ogg", "audio", true)]
        [TestCase("video/mp4", "video", true)]
        [TestCase("model/gltf-binary", "model", true)]
        [TestCase("application/zip", "archive", true)]
        [TestCase("application/x-7z-compressed", "archive", true)]
        [TestCase("application/octet-stream", "binary", true)]
        [TestCase("application/vnd.example", "any", true)]
        [TestCase(null, "any", true)]
        [TestCase("text/html", "json", false)]
        [TestCase("application/vnd.example", "unknown", false)]
        [TestCase(null, "image", false)]
        public void TestLuaNetworkResponseContentTypeMatchesConfiguredFileType(
            string contentType, string fileType, bool expectedAllowed)
        {
            Assert.AreEqual(expectedAllowed, WebRequestApiWrapper.IsContentTypeAllowed(
                contentType, new[] { fileType }));
        }

        [TestCase("image/jpeg", "image/", true)]
        [TestCase("image/png; charset=binary", "image/", true)]
        [TestCase("application/json", "image/", false)]
        [TestCase(null, "image/", false)]
        public void TestLuaMediaDownloadContentTypeMatchesRequiredPrefix(
            string contentType, string requiredPrefix, bool expectedAllowed)
        {
            Assert.AreEqual(expectedAllowed, ApiMethods.ContentTypeMatchesPrefix(
                contentType, requiredPrefix));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestAllowlistedLuaNetworkAccessDisablesRedirects(bool unrestrictedAccess)
        {
            using var request = UnityWebRequest.Get("https://example.com/resource");
            int initialRedirectLimit = request.redirectLimit;
            WebRequestApiWrapper.ConfigureRedirects(request, unrestrictedAccess);
            Assert.AreEqual(
                unrestrictedAccess ? initialRedirectLimit : 0,
                request.redirectLimit);
        }

        [TestCase("{}", "examples.openbrush.test")]
        [TestCase("{\"Flags\":{\"PluginWebRequestRules\":null}}", "examples.openbrush.test")]
        [TestCase("{\"Flags\":{\"PluginWebRequestRules\":[]}}", "")]
        [TestCase("{\"Flags\":{\"PluginWebRequestRules\":[{\"Host\":\"custom.test\",\"Methods\":[\"GET\"],\"FileTypes\":[\"image\"]}]}}", "custom.test")]
        public void TestUserConfigInheritsOnlyMissingOrNullWebRequestRuleDefaults(
            string userConfigText, string expectedHosts)
        {
            const string defaultConfigText =
                "{\"Flags\":{\"PluginWebRequestRules\":[{\"Host\":\"examples.openbrush.test\",\"Methods\":[\"GET\"],\"FileTypes\":[\"json\"]}]}}";

            UserConfig config = App.DeserializeUserConfigWithDefaults(
                defaultConfigText, userConfigText, out string warning);

            Assert.IsNull(warning);
            Assert.AreEqual(
                expectedHosts,
                string.Join(",", config.Flags.PluginWebRequestRules.Select(rule => rule.Host)));
        }

        [Test]
        public void TestLegacyStringOnlyLuaHostAllowlistIsNotAccepted()
        {
            const string defaultConfigText =
                "{\"Flags\":{\"PluginWebRequestRules\":[{\"Host\":\"default.test\",\"Methods\":[\"GET\"],\"FileTypes\":[\"json\"]}]}}";
            const string userConfigText =
                "{\"Flags\":{\"LuaWebRequestAllowedHosts\":[\"legacy.test\"]}}";

            UserConfig config = App.DeserializeUserConfigWithDefaults(
                defaultConfigText, userConfigText, out string warning);

            StringAssert.Contains("LuaWebRequestAllowedHosts", warning);
            Assert.AreEqual("default.test", config.Flags.PluginWebRequestRules.Single().Host);
        }

        [TestCase("module.lua", true)]
        [TestCase("subdir/module.lua", true)]
        [TestCase("../module.lua", false)]
        [TestCase("..\\module.lua", false)]
        public void TestLuaModuleLoaderConstrainsRelativePaths(string modulePath, bool expectedValid)
        {
            string moduleRoot = Path.Combine(Path.GetTempPath(), "OpenBrushLuaModules");

            bool valid = OpenBrushScriptLoader.TryGetSafeModulePath(
                modulePath,
                moduleRoot,
                out string fullPath);

            Assert.AreEqual(expectedValid, valid);
            if (expectedValid)
            {
                Assert.IsTrue(fullPath.StartsWith(Path.GetFullPath(moduleRoot), StringComparison.OrdinalIgnoreCase));
            }
        }

        [Test]
        public void TestLuaModuleLoaderAllowsOnlyAbsolutePathsUnderModuleRoot()
        {
            string moduleRoot = Path.Combine(Path.GetTempPath(), "OpenBrushLuaModules");
            string safeModulePath = Path.Combine(moduleRoot, "module.lua");
            string unsafeModulePath = Path.Combine(Path.GetTempPath(), "OpenBrushOtherModules", "module.lua");

            Assert.IsTrue(OpenBrushScriptLoader.TryGetSafeModulePath(
                safeModulePath,
                moduleRoot,
                out _));
            Assert.IsFalse(OpenBrushScriptLoader.TryGetSafeModulePath(
                unsafeModulePath,
                moduleRoot,
                out _));
        }

        [Test]
        public void TestLuaModuleLoaderUsesPlatformPathCaseSemantics()
        {
            string moduleRoot = Path.Combine(Path.GetTempPath(), "OpenBrushLuaModulesCaseTest");
            string alternateCasePath = Path.Combine(
                Path.GetTempPath(), "openbrushluamodulescasetest", "module.lua");

            bool valid = OpenBrushScriptLoader.TryGetSafeModulePath(
                alternateCasePath,
                moduleRoot,
                out _);

            Assert.AreEqual(Path.DirectorySeparatorChar == '\\', valid);
        }

        [Test]
        public void TestLuaSoftSandboxRemovesFilesystemAndCommandExecutionModules()
        {
            Script script = LuaManager.CreatePluginScript();
            DynValue result = script.DoString(@"
                return io == nil
                    and loadfile == nil
                    and loadfilesafe == nil
                    and dofile == nil
                    and load == nil
                    and loadsafe == nil
                    and require ~= nil
                    and os ~= nil
                    and os.execute == nil
                    and os.exit == nil
                    and os.getenv == nil
                    and os.remove == nil
                    and os.rename == nil
                    and os.setlocale == nil
                    and os.tmpname == nil
                    and os.clock ~= nil
                    and os.date ~= nil
                    and os.difftime ~= nil
                    and os.time ~= nil
            ");

            Assert.IsTrue(result.Boolean);
        }

        [Test]
        public void TestLuaSoftSandboxCanRequireModulesThroughOpenBrushLoader()
        {
            string moduleRoot = Path.Combine(Path.GetTempPath(), "OpenBrushLuaModuleRequireTest");
            Directory.CreateDirectory(moduleRoot);
            string modulePath = Path.Combine(moduleRoot, "safe_module.lua");
            File.WriteAllText(modulePath, "return { value = 42 }");

            IScriptLoader originalLoader = Script.DefaultOptions.ScriptLoader;
            try
            {
                var loader = new OpenBrushScriptLoader(moduleRoot);
                loader.ModulePaths = new[] { Path.Combine(moduleRoot, "?.lua") };
                Script.DefaultOptions.ScriptLoader = loader;

                Script script = LuaManager.CreatePluginScript();
                DynValue result = script.DoString("return require('safe_module').value");

                Assert.AreEqual(42, result.Number);
            }
            finally
            {
                Script.DefaultOptions.ScriptLoader = originalLoader;
                File.Delete(modulePath);
                Directory.Delete(moduleRoot);
            }
        }

        [TestCase("scripts.initPluginScripting")]
        [TestCase("scripts.toolscript.activate")]
        [TestCase("scripts.toolscript.deactivate")]
        [TestCase("scripts.symmetryscript.activate")]
        [TestCase("scripts.symmetryscript.deactivate")]
        [TestCase("scripts.pointerscript.activate")]
        [TestCase("scripts.pointerscript.deactivate")]
        [TestCase("scripts.backgroundscript.activate")]
        [TestCase("scripts.backgroundscript.deactivate")]
        [TestCase("scripts.backgroundscript.activateall")]
        [TestCase("scripts.backgroundscript.deactivateall")]
        public void TestWebPluginControlEndpointsRequireExplicitPermission(string command)
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                ApiManager.EnsureWebPluginControlAllowed(command, false));
            Assert.DoesNotThrow(() =>
                ApiManager.EnsureWebPluginControlAllowed(command, true));
        }

        [Test]
        public void TestWebPluginControlPermissionDoesNotAffectOtherApiEndpoints()
        {
            Assert.DoesNotThrow(() =>
                ApiManager.EnsureWebPluginControlAllowed("brush.draw", false));
        }

        [Test]
        public void TestApiAuthorizationFailureBecomesCompletedErrorStatus()
        {
            string status = ApiManager.InvokeEndpointForStatus(() =>
                throw new UnauthorizedAccessException("permission denied"));

            Assert.AreEqual("error: permission denied", status);
        }

        [Test]
        public void TestReflectedApiAuthorizationFailureBecomesCompletedErrorStatus()
        {
            string status = ApiManager.InvokeEndpointForStatus(() =>
                throw new System.Reflection.TargetInvocationException(
                    new UnauthorizedAccessException("permission denied")));

            Assert.AreEqual("error: permission denied", status);
        }

        [Test]
        public void TestUnexpectedApiFailureIsNotSilenced()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ApiManager.InvokeEndpointForStatus(() =>
                    throw new InvalidOperationException("unexpected failure")));
        }
    }
}
