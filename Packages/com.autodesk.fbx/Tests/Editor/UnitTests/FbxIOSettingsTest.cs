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

    internal class FbxIOSettingsTest : Base<FbxIOSettings>
    {
        [Test]
        public void TestFVirtual ()
        {
            // Test the swig -fvirtual flag works properly: we can call virtual
            // functions defined on the base class without the function also
            // being defined in the subclass.

            FbxManager manager = FbxManager.Create ();
            FbxIOSettings ioSettings = FbxIOSettings.Create (manager, "");

            // GetSelected is a virtual method inherited from FbxObject
            Assert.IsFalse (ioSettings.GetSelected ());
            ioSettings.SetSelected (true);
            Assert.IsTrue (ioSettings.GetSelected ());

            ioSettings.Destroy ();
            manager.Destroy ();
        }
		
        [Test]
        public void TestIdentity ()
        {
            using (FbxIOSettings ioSettings1 = FbxIOSettings.Create (Manager, "")) {
                Manager.SetIOSettings (ioSettings1);

                FbxIOSettings ioSettings2 = Manager.GetIOSettings ();
                Assert.AreEqual (ioSettings1, ioSettings2);
            }
        }

        [Test]
        public void TestSetBoolProp()
        {
            // just make sure it doesn't crash
            using (FbxIOSettings ioSettings = FbxIOSettings.Create (Manager, "")) {
                ioSettings.SetBoolProp (Globals.EXP_FBX_EMBEDDED, true);
                ioSettings.SetBoolProp ("", true);

                Assert.That (() => { ioSettings.SetBoolProp (null, true); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }
    }
}
