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

    internal class FbxObjectTest : Base<FbxObject>
    {
        [Test]
        public void TestUTF8()
        {
            // make sure japanese survives the round-trip.
            string katakana = "片仮名";
            FbxObject obj = FbxObject.Create(Manager, katakana);
            Assert.AreEqual(katakana, obj.GetName());
        }

        [Test]
        public void TestGetManager ()
        {
            using (FbxObject obj = FbxObject.Create (Manager, "")) {
                FbxManager fbxManager2 = obj.GetFbxManager();
                Assert.AreEqual(Manager, fbxManager2);
            }
        }
    }
}
