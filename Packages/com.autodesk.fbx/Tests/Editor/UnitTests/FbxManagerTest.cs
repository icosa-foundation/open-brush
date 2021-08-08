// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.  
//
// Licensed under the ##LICENSENAME##. 
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{

    internal class FbxManagerTest
    {

        FbxManager m_fbxManager;

        [SetUp]
        public void Init ()
        {
            m_fbxManager = FbxManager.Create ();
        }

        [TearDown]
        public void End ()
        {
            m_fbxManager.Destroy ();
        }

        [Test]
        public void TestVersion ()
        {
            string version = FbxManager.GetVersion ();
            Assert.IsNotEmpty (version);
            
            string versionLong = FbxManager.GetVersion (true);
            Assert.IsNotEmpty (versionLong);

            string versionShort = FbxManager.GetVersion (false);
            Assert.IsNotEmpty (versionShort);
        }

        [Test]
        public void TestGetFileFormatVersion ()
        {
            int major = -1, minor = -1, revision = -1;

            FbxManager.GetFileFormatVersion (out major, out minor, out revision);

            Assert.GreaterOrEqual (major, 0);
            Assert.GreaterOrEqual (minor, 0);
            Assert.GreaterOrEqual (revision, 0);

        }

        [Test]
        public void TestIOSettings ()
        {
            FbxIOSettings ioSettings = m_fbxManager.GetIOSettings ();
            Assert.IsNull(ioSettings);

            using (FbxIOSettings ioSettings1 = FbxIOSettings.Create (m_fbxManager, "")) {
                m_fbxManager.SetIOSettings (ioSettings1);

                FbxIOSettings ioSettings2 = m_fbxManager.GetIOSettings ();
                Assert.IsNotNull (ioSettings2);
            }
        }

        [Test]
        public void TestIdentity ()
        {
            using (FbxObject obj = FbxObject.Create (m_fbxManager, "")) {
                FbxManager fbxManager2 = obj.GetFbxManager();
                
                Assert.AreEqual (m_fbxManager, fbxManager2);
            }
        }

        [Test]
        public void TestUsing ()
        {
            // Test that the using statement works, and destroys the manager.
            FbxObject obj;
            using (var mgr = FbxManager.Create ()) {
                obj = FbxObject.Create(mgr, "asdf");
            }
            Assert.That(() => { obj.GetName (); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Also test explicit dispose.
            var mgr2 = FbxManager.Create();
            obj = FbxObject.Create(mgr2, "hjkl");
            mgr2.Dispose();
            Assert.That(() => { obj.GetName (); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void TestGetIOPluginRegistry()
        {
            // pretty much just want to check that it doesn't crash
            var ioPluginRegistry = m_fbxManager.GetIOPluginRegistry();
            Assert.IsInstanceOf<FbxIOPluginRegistry> (ioPluginRegistry);
        }
    }
}
