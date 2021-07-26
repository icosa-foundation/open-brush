// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;
using System.Collections.Generic;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxImplementationTest : Base<FbxImplementation>
    {
        [Test]
        public void TestBasics() {
            var impl = FbxImplementation.Create(Manager, "impl");

            // Call the getters, make sure they get.
            GetSetProperty(impl.Language, "klingon");
            GetSetProperty(impl.LanguageVersion, "0.1");
            GetSetProperty(impl.RenderAPI, "bogosity");
            GetSetProperty(impl.RenderAPIVersion, "0.1");
            GetSetProperty(impl.RootBindingName, "root");

            impl.RootBindingName.Set("root");
            var table = impl.AddNewTable("root", "shader");
            Assert.AreEqual(table, impl.GetRootTable());
        }

        void GetSetProperty(FbxPropertyString prop, string value) {
            prop.Set(value);
            Assert.AreEqual(value, prop.Get());
        }
    }
}
