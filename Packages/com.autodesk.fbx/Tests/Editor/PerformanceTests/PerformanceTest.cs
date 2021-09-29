// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System;
using UnityEditorInternal;

namespace Autodesk.Fbx.PerformanceTests
{
    [TestFixture]
    internal class PerformanceTest
    {
        protected string exeFileName {
            get {
#if UNITY_EDITOR_WIN
                return Path.GetFullPath("Packages/com.autodesk.fbx/Tests/PerformanceBenchmarks-win-x64.exe");
#elif UNITY_EDITOR_OSX
                return Path.GetFullPath("Packages/com.autodesk.fbx/Tests/PerformanceBenchmarks-mac-x64");
#elif UNITY_EDITOR_LINUX
                return Path.GetFullPath("Packages/com.autodesk.fbx/Tests/PerformanceBenchmarks-linux-x64");
#else
                throw new NotImplementedException();
#endif
            }
        }

        protected void LogError (string msg)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogError (msg);
#endif
        }


        [System.Serializable]
        public class ResultJsonList
        {
            public List<ResultJson> tests;
        }

        [System.Serializable]
        public class ResultJson
        {
            public string testName;
            public double result;
            public bool success;
            public string error = "";
        }

        private ResultJson RunCppTest (string testName)
        {
            // run native C++ tests here + get results to compare against
            // In Windows, the exe has to be in the same folder as the fbxsdk library in order to run

            Process cpp = new Process ();
            cpp.StartInfo.FileName = this.exeFileName;
            cpp.StartInfo.Arguments = testName;
            cpp.StartInfo.RedirectStandardOutput = true;
            cpp.StartInfo.UseShellExecute = false;
            cpp.Start ();

            // TODO: fix mono warning about compatibility with .NET
            StringBuilder output = new StringBuilder ();
            while (!cpp.HasExited) {
                output.Append (cpp.StandardOutput.ReadToEnd ());
            }

            try {
#if UNITY_EDITOR
                ResultJsonList cppJson = UnityEngine.JsonUtility.FromJson<ResultJsonList> (output.ToString ());
#else
                var cppJson = null;
                throw new NotImplementedException();
#endif
                if(cppJson == null){
                    this.LogError("CppError [" + testName + "]:" + output);
                    return null;
                }

                if (cppJson.tests.Count <= 0) {
                    this.LogError ("Error: No json test results received");
                    return null;
                }

                var cppResult = cppJson.tests [0];
                Assert.IsNotNull (cppResult);
                Assert.IsTrue (cppResult.success);

                if (!String.IsNullOrEmpty (cppResult.error)) {
                    this.LogError ("CppError [" + testName + "]: " + cppResult.error);
                }

                return cppResult;

            } catch (System.ArgumentException) {
                this.LogError ("Error [" + testName + "]: Malformed json string: " + output);
                return null;
            }
        }

        private FbxManager m_fbxManager;
        private Stopwatch m_stopwatch;

        [SetUp]
        public void Init()
        {
            m_stopwatch = new Stopwatch ();
            m_fbxManager = FbxManager.Create ();
        }

        [TearDown]
        public void Term()
        {
            m_fbxManager.Destroy ();
        }

        private void CheckAgainstNative (string testName, double managedResult, int sampleSize, int threshold)
        {
            // Check against Native C++ tests
            var cppResult = RunCppTest (testName + ":" + sampleSize);

            Assert.IsNotNull (cppResult);

            LogResult (testName, cppResult.result, managedResult, threshold, sampleSize);

            // Ex: test that the unity test is no more than 4 times slower
            Assert.LessOrEqual (managedResult, threshold * cppResult.result);
        }
        
        // function to run for the test
        private delegate void TestFunc();

        /*
         * Default function for running a block of code a bunch of times. 
         */
        private void DefaultTest(string testName, int sampleSize, int threshold, TestFunc codeToExecute)
        {
            long total = 0;

            m_stopwatch.Reset ();
            m_stopwatch.Start ();

            codeToExecute ();

            m_stopwatch.Stop ();
            total = m_stopwatch.ElapsedMilliseconds;

            CheckAgainstNative (testName, total, sampleSize, threshold);
        }

        [Test]
        public void FbxObjectCreateTest ()
        {
            int N = 5000;
            DefaultTest (
                "FbxObjectCreate",
                N,
                10,
                () => {
                    for (int i = 0; i < N; i++) {
                        FbxObject.Create (m_fbxManager, "");
                    }
                }
            );
        }

        [Test]
        public void SetControlPointAtTest()
        {
            int N = 1000000;
            DefaultTest (
                "SetControlPointAt",
                N,
                40,
                () => {
                    FbxGeometryBase geometryBase = FbxGeometryBase.Create(m_fbxManager, "");
                    geometryBase.InitControlPoints(1);
                    for(int i = 0; i < N; i ++){
                        FbxVector4 vector = new FbxVector4(0,0,0);
                        geometryBase.SetControlPointAt(vector, 0);
                    }
                }
            );
        }

        [Test]
        public void EmptyExportImportTest ()
        {
            int N = 10;
            long total = 0;

            for (int i = 0; i < N; i++) {
                m_stopwatch.Reset ();
                m_stopwatch.Start ();

                FbxIOSettings ioSettings = FbxIOSettings.Create (m_fbxManager, Globals.IOSROOT);
                m_fbxManager.SetIOSettings (ioSettings);

                FbxExporter exporter = FbxExporter.Create (m_fbxManager, "");

                string filename = "test.fbx";

                bool exportStatus = exporter.Initialize (filename, -1, m_fbxManager.GetIOSettings ());

                // Check that export status is True
                Assert.IsTrue (exportStatus);

                // Create an empty scene to export
                FbxScene scene = FbxScene.Create (m_fbxManager, "myScene");

                // Export the scene to the file.
                exporter.Export (scene);

                exporter.Destroy ();

                // Import to make sure file is valid

                FbxImporter importer = FbxImporter.Create (m_fbxManager, "");

                bool importStatus = importer.Initialize (filename, -1, m_fbxManager.GetIOSettings ());

                Assert.IsTrue (importStatus);

                // Create a new scene so it can be populated
                FbxScene newScene = FbxScene.Create (m_fbxManager, "myScene2");

                importer.Import (newScene);

                importer.Destroy ();

                m_stopwatch.Stop ();

                total += m_stopwatch.ElapsedMilliseconds;

                // Delete the file once the test is complete
                File.Delete (filename);
            }

            CheckAgainstNative ("EmptyExportImport", total / (float)N, N, 4);
        }

        private void LogResult(string testName, double native, double managed, int n, int sampleSize){
            UnityEngine.Debug.Log (
                String.Format ("Test [{0}]: Managed must run at most {1} times slower than native to pass. (Native = {2} ms, Managed = {3} ms, SampleSize = {4}, UnityVersion = {5})",
                    testName, n, native, managed, sampleSize, InternalEditorUtility.GetFullUnityVersion())
            );
        }
    }
}