// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UseCaseTests
{
    internal abstract class RoundTripTestBase
    {
        private string _filePath;
        protected string filePath       { get { return string.IsNullOrEmpty(_filePath) ? "." : _filePath; } set { _filePath = value; } }

        private string _fileNamePrefix;
        protected string fileNamePrefix { get { return string.IsNullOrEmpty(_fileNamePrefix) ? "_safe_to_delete__" : _fileNamePrefix; }
                                          set { _fileNamePrefix = value; } }

        private string _fileNameExt;
        protected string fileNameExt    { get { return string.IsNullOrEmpty(_fileNameExt) ? ".fbx" : _fileNameExt; } set { _fileNameExt = value; } }

        private string MakeFileName(string baseName = null, string prefixName = null, string extName = null)
        {
            if (baseName==null)
                baseName = Path.GetRandomFileName();

            if (prefixName==null)
                prefixName = this.fileNamePrefix;

            if (extName==null)
                extName = this.fileNameExt;

            return prefixName + baseName + extName;
        }

        protected string GetRandomFileNamePath(string pathName = null, string prefixName = null, string extName = null)
        {
            string temp;

            if (pathName==null)
                pathName = this.filePath;

            if (prefixName==null)
                prefixName = this.fileNamePrefix;

            if (extName==null)
                extName = this.fileNameExt;

            // repeat until you find a file that does not already exist
            do {
                temp = Path.Combine (pathName, MakeFileName(prefixName: prefixName, extName: extName));

            } while(File.Exists (temp));

            return temp;
        }

        private FbxManager m_fbxManager;

        protected FbxManager FbxManager { get { return m_fbxManager; } }

        [SetUp]
        public virtual void Init ()
        {
            foreach (string file in Directory.GetFiles (this.filePath, MakeFileName("*"))) {
                File.Delete (file);
            }

            // create fbx manager.
            m_fbxManager = FbxManager.Create ();

            // configure IO settings.
            m_fbxManager.SetIOSettings (FbxIOSettings.Create (m_fbxManager, Globals.IOSROOT));
        }

        [TearDown]
        public virtual void Term ()
        {
            try {
                m_fbxManager.Destroy ();
            } 
            catch (System.ArgumentNullException) {
            }
        }

        protected virtual FbxScene CreateScene (FbxManager manager)
        {
            return FbxScene.Create (manager, "myScene");
        }

        protected virtual void CheckScene (FbxScene scene)
        {}

        protected void ExportScene (string fileName)
        {
            // Export the scene
            using (FbxExporter exporter = FbxExporter.Create (FbxManager, "myExporter")) {

                // Initialize the exporter.
                bool status = exporter.Initialize (fileName, -1, FbxManager.GetIOSettings ());

                // Check that export status is True
                Assert.IsTrue (status);

                // Create a new scene so it can be populated by the imported file.
                FbxScene scene = CreateScene (FbxManager);

                CheckScene (scene);

                // Export the scene to the file.
                exporter.Export (scene);

                // Check if file exists
                Assert.IsTrue (File.Exists (fileName));
            }
        }

        protected void ImportScene (string fileName)
        {
            // Import the scene to make sure file is valid
            using (FbxImporter importer = FbxImporter.Create (FbxManager, "myImporter")) {

                // Initialize the importer.
                bool status = importer.Initialize (fileName, -1, FbxManager.GetIOSettings ());

                Assert.IsTrue (status);

                // Create a new scene so it can be populated by the imported file.
                FbxScene scene = FbxScene.Create (FbxManager, "myScene");

                // Import the contents of the file into the scene.
                importer.Import (scene);

                // check that the scene is valid
                CheckScene (scene);
            }
        }

        [Test]
        public void TestExportScene ()
        {
            var fileName = GetRandomFileNamePath ();

            this.ExportScene (fileName);

            File.Delete (fileName);
        }

        [Test]
        public void TestRoundTrip ()
        {
            var fileName = GetRandomFileNamePath ();

            this.ExportScene (fileName);
            this.ImportScene (fileName);

            File.Delete (fileName);
        }
    }
}