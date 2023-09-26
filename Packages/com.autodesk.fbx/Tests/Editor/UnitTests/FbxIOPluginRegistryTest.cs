// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxIOPluginRegistryTest
    {
        [Test]
        public void TestBasics ()
        {
            using (FbxManager manager = FbxManager.Create ()) {
                int fileFormat = manager.GetIOPluginRegistry ().FindWriterIDByDescription ("FBX ascii (*.fbx)");
                Assert.GreaterOrEqual (fileFormat, 0); // just check that it is something other than -1

                // test an invalid format
                fileFormat = manager.GetIOPluginRegistry ().FindWriterIDByDescription ("invalid format");
                Assert.AreEqual (-1, fileFormat);

                // test null
                Assert.That (() => { manager.GetIOPluginRegistry ().FindWriterIDByDescription (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test dispose
                // TODO: Dispose doesn't really seem useful here, should we do anything about it?
                manager.GetIOPluginRegistry ().Dispose ();
                fileFormat = manager.GetIOPluginRegistry ().FindWriterIDByDescription ("invalid format");
                Assert.AreEqual (-1, fileFormat);
            }
        }
    }
}
