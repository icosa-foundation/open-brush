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
    internal abstract class FbxLayerElementArrayTestBase<T> : TestBase<T> where T : Autodesk.Fbx.FbxLayerElementArray
    {
        public void TestBasics(T layerElementArray)
        {
            // Test SetCount()
            layerElementArray.SetCount (1);
            Assert.AreEqual (layerElementArray.GetCount (), 1);

            // test invalid
            layerElementArray.SetCount (-1);

            // Test AddInt()
            layerElementArray.Add (0);
            layerElementArray.Add (-1);

            // Test AddFbxColor()
            layerElementArray.Add (new FbxColor ());
            layerElementArray.Add (new FbxColor (1, 0, 0));

            // Test AddFbxVector2()
            layerElementArray.Add (new FbxVector2 ());
            layerElementArray.Add (new FbxVector2 (1, 0));

            // Test AddFbxVector4()
            layerElementArray.Add (new FbxVector4 ());
            layerElementArray.Add (new FbxVector4 (1, 0, 0));

            // Test SetAtInt()
            layerElementArray.SetAt (0, 1);

            // test invalid index
            layerElementArray.SetAt (-1, 1);

            // test negative int
            layerElementArray.SetAt (1, -1);

            // Test SetAtFbxColor()
            layerElementArray.SetAt (0, new FbxColor ());

            // test invalid index
            layerElementArray.SetAt (-1, new FbxColor ());

            // Test SetFbxVector2()
            layerElementArray.SetAt (0, new FbxVector2 ());

            // test invalid index
            layerElementArray.SetAt (-1, new FbxVector2 ());

            // Test SetAtFbxVector4()
            layerElementArray.SetAt (0, new FbxVector4 ());

            // test invalid index
            layerElementArray.SetAt (-1, new FbxVector4 ());

            // test dispose
            layerElementArray.Dispose ();
            Assert.That (() => {
                layerElementArray.SetCount (1);
            }, Throws.Exception.TypeOf<System.ArgumentNullException> ());
        }

        [Test]
        public abstract void TestBasics();
    }

    internal abstract class FbxLayerElementArrayTemplateTestBase<T,U> : FbxLayerElementArrayTestBase<T> where T : Autodesk.Fbx.FbxLayerElementArray {

        static System.Reflection.MethodInfo s_getAt;
        static System.Reflection.ConstructorInfo s_constructor;

        static FbxLayerElementArrayTemplateTestBase() {
            s_getAt = typeof(T).GetMethod("GetAt", new System.Type[] { typeof(int) });
            s_constructor = typeof(T).GetConstructor (System.Type.EmptyTypes);
        }

        public T CreateObject()
        {
            return Invoker.InvokeConstructor<T> (s_constructor);
        }

        public U GetAt(T layerElementArray, int index){
            return Invoker.Invoke<U> (s_getAt, layerElementArray, index);
        }

        [Test]
        public override void TestBasics ()
        {
            base.TestBasics (CreateObject());
        }

        [Test]
        public void TestGetAt ()
        {
            T layerElementArrayTemplate = CreateObject();

            Assert.IsNotNull (layerElementArrayTemplate);

            layerElementArrayTemplate.SetCount (1);

            // make sure doesn't crash
            GetAt (layerElementArrayTemplate, 0);

            Assert.That (() => {
                GetAt (layerElementArrayTemplate, int.MinValue);
            }, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException> ());

            Assert.That (() => {
                GetAt (layerElementArrayTemplate, int.MaxValue);
            }, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException> ());
        }
    }

    internal class FbxLayerElementArrayTest : FbxLayerElementArrayTestBase<FbxLayerElementArray> {
        [Test]
        public override void TestBasics()
        {
            TestBasics(new FbxLayerElementArray (EFbxType.eFbxBlob));
        }
    }

    internal class FbxLayerElementArrayTemplateFbxColorTest : 
        FbxLayerElementArrayTemplateTestBase<FbxLayerElementArrayTemplateFbxColor,FbxColor> {
    }

    internal class FbxLayerElementArrayTemplateFbxSurfaceMaterialTest :
        FbxLayerElementArrayTemplateTestBase<FbxLayerElementArrayTemplateFbxSurfaceMaterial,FbxSurfaceMaterial> {
    }

    internal class FbxLayerElementArrayTemplateFbxVector2Test : 
        FbxLayerElementArrayTemplateTestBase<FbxLayerElementArrayTemplateFbxVector2,FbxVector2> {
    }

    internal class FbxLayerElementArrayTemplateFbxVector4Test : 
        FbxLayerElementArrayTemplateTestBase<FbxLayerElementArrayTemplateFbxVector4,FbxVector4> {
    }

    internal class FbxLayerElementArrayTemplateIntTest : 
        FbxLayerElementArrayTemplateTestBase<FbxLayerElementArrayTemplateInt,int> {
    }
}
