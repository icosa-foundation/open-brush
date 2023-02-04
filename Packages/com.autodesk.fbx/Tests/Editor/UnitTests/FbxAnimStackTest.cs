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
    internal class FbxAnimStackTest : Base<FbxAnimStack>
    {
        [Test]
        public void TestBasics(){
            using (var animStack = CreateObject ("anim stack")) {
                FbxCollectionTest.GenericTests (animStack, Manager);

                // test description
                animStack.Description.Set ("this is an anim stack");
                Assert.AreEqual ("this is an anim stack", animStack.Description.Get ());

                // test SetLocalTimeSpan (make sure it doesn't crash)
                animStack.SetLocalTimeSpan(new FbxTimeSpan());

                // test GetLocalTimeSpan
                FbxTimeSpan timeSpan = animStack.GetLocalTimeSpan();
                Assert.IsInstanceOf<FbxTimeSpan> (timeSpan);

                // test SetLocalTimeSpan with null
                Assert.That (() => { animStack.SetLocalTimeSpan(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }
    }
}