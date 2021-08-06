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
    internal abstract class Base<T> : TestBase<T> where T: Autodesk.Fbx.FbxObject
    {
        // T.Create(FbxManager, string)
        static System.Reflection.MethodInfo s_createFromMgrAndName;

        // T.Create(FbxObject, string)
        static System.Reflection.MethodInfo s_createFromObjAndName;

        static Base() {
            s_createFromMgrAndName = typeof(T).GetMethod("Create", new System.Type[] {typeof(FbxManager), typeof(string)});
            s_createFromObjAndName = typeof(T).GetMethod("Create", new System.Type[] {typeof(FbxObject), typeof(string)});
        }


        protected FbxManager Manager {
            get;
            private set;
        }

        /* Create an object with the default manager. */
        public T CreateObject (string name = "") {
            return CreateObject(Manager, name);
        }

        /* Test all the equality functions we can find. */
        [Test]
        public virtual void TestEquality() {
            var a = CreateObject("a");
            var b = CreateObject("b");
            var acopy = a; // TODO: get a different proxy to the same underlying object
            EqualityTester<T>.TestEquality(a, b, acopy);
        }

        /* Create an object with another manager. Default implementation uses
         * reflection to call T.Create(...); override if reflection is wrong. */
        public virtual T CreateObject (FbxManager mgr, string name = "") {
            return Invoker.InvokeStatic<T>(s_createFromMgrAndName, mgr, name);
        }

        /* Create an object with an object as container. Default implementation uses
         * reflection to call T.Create(...); override if reflection is wrong. */
        public virtual T CreateObject (FbxObject container, string name = "") {
            return Invoker.InvokeStatic<T>(s_createFromObjAndName, container, name);
        }

        [SetUp]
        public virtual void Init ()
        {
            Manager = FbxManager.Create ();
        }

        [TearDown]
        public virtual void Term ()
        {
            try {
                Manager.Destroy ();
            }
            catch (System.ArgumentNullException) {
            }
        }

        /// <summary>
        /// Test that an object created within a scene knows its scene.
        /// Override for objects that can't be in a scene.
        /// </summary>
        protected virtual void TestSceneContainer()
        {
            using(var scene = FbxScene.Create(Manager, "thescene")) {
                var obj = CreateObject(scene, "scene_object");
                Assert.AreEqual(scene, obj.GetScene());
                var child = CreateObject(obj, "scene_object_child");
                Assert.AreEqual(scene, child.GetScene());
            }

            {
                var obj = CreateObject(Manager, "not_scene_object");
                Assert.AreEqual(null, obj.GetScene());
            }
        }

        [Test]
        public virtual void TestCreate()
        {
            var obj = CreateObject("MyObject");
            Assert.IsInstanceOf<T> (obj);
            Assert.AreEqual(Manager, obj.GetFbxManager());

            using(var manager2 = FbxManager.Create()) {
                var obj2 = CreateObject(manager2, "MyOtherObject");
                Assert.AreEqual(manager2, obj2.GetFbxManager());
                Assert.AreNotEqual(Manager, obj2.GetFbxManager());
            }

            var obj3 = CreateObject(obj, "MySubObject");
            Assert.AreEqual(Manager, obj3.GetFbxManager());

            // Test with a null manager or container. Should throw.
            Assert.That (() => { CreateObject((FbxManager)null, "MyObject"); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            Assert.That (() => { CreateObject((FbxObject)null, "MyObject"); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Test having a scene as the container.
            TestSceneContainer();

            // Test with a null string. Should work.
            Assert.IsNotNull(CreateObject((string)null));

            // Test with a destroyed manager. Should throw.
            var mgr = FbxManager.Create();
            mgr.Destroy();
            Assert.That (() => { CreateObject(mgr, "MyObject"); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Test with a disposed manager. Should throw.
            mgr = FbxManager.Create();
            mgr.Dispose();
            Assert.That (() => { CreateObject(mgr, "MyObject"); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public virtual void TestDisposeDestroy ()
        {
           DoTestDisposeDestroy(canDestroyNonRecursive: true);
        }

        public virtual void DoTestDisposeDestroy (bool canDestroyNonRecursive)
        {
            T a, b;

            // Test destroying just yourself.
            a = CreateObject ("a");
            b = CreateObject(a, "b");
            a.Destroy ();
            Assert.That(() => a.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());
            if (canDestroyNonRecursive) {
                b.GetName(); // does not throw! tests that the implicit 'pRecursive: false' got through
                b.Destroy();
            } else {
                Assert.That(() => b.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());
            }

            // Test destroying just yourself, explicitly non-recursive.
            a = CreateObject ("a");
            b = CreateObject(a, "b");
            a.Destroy (false);
            Assert.That(() => a.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());
            if (canDestroyNonRecursive) {
                b.GetName(); // does not throw! tests that the explicit 'false' got through
                b.Destroy();
            } else {
                Assert.That(() => b.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());
            }

            // Test destroying recursively.
            a = CreateObject ("a");
            b = CreateObject(a, "b");
            a.Destroy(true);
            Assert.That(() => b.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());
            Assert.That(() => a.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Test disposing. TODO: how to test that a was actually destroyed?
            a = CreateObject("a");
            a.Dispose();
            Assert.That(() => a.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Test that the using statement works.
            using (a = CreateObject ("a")) {
                a.GetName (); // works here, throws outside using
            }
            Assert.That(() => a.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Test that if we try to use an object after Destroy()ing its
            // manager, the object was destroyed as well.
            a = CreateObject("a");
            Assert.IsNotNull (a);
            Manager.Destroy();
            Assert.That (() => { a.GetName (); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void TestVarious()
        {
            FbxObject obj;

            /************************************************************
             * Test selection
             ************************************************************/
            obj = CreateObject ();
            Assert.IsFalse (obj.GetSelected ());
            obj.SetSelected (true);
            Assert.IsTrue (obj.GetSelected ());

            /************************************************************
             * Test name-related functions.
             ************************************************************/

            /*
             * We use this also for testing that string handling works.
             * Make sure we can pass const char*, FbxString, and const
             * FbxString&.
             * Make sure we can return those too (though I'm not actually
             * seeing a return of a const-ref anywhere).
             */
            // Test a function that takes const char*.
            obj = FbxObject.Create(Manager, "MyObject");
            Assert.IsNotNull (obj);

            // Test a function that returns const char*.
            Assert.AreEqual ("MyObject", obj.GetName ());

            // Test a function that takes an FbxString with an accent in it.
            obj.SetNameSpace("Accentué");

            // Test a function that returns FbxString.
            Assert.AreEqual ("MyObject", obj.GetNameWithoutNameSpacePrefix ());

            // Test a function that returns FbxString with an accent in it.
            Assert.AreEqual ("Accentué", obj.GetNameSpaceOnly());

            // Test a function that takes a const char* and returns an FbxString.
            // We don't want to convert the other StripPrefix functions, which
            // modify their argument in-place.
            Assert.AreEqual("MyObject", FbxObject.StripPrefix("NameSpace::MyObject"));

            obj.SetName("new name");
            Assert.AreEqual("new name", obj.GetName());

            obj.SetInitialName("init");
            Assert.AreEqual("init", obj.GetInitialName());

            /************************************************************
             * Test shader implementations
             ************************************************************/
            using (obj = FbxObject.Create(Manager, "MyObject")) {
                var impl = FbxImplementation.Create(obj, "impl");
                Assert.IsTrue(obj.AddImplementation(impl));
                Assert.IsTrue(obj.RemoveImplementation(impl));
                Assert.IsTrue(obj.AddImplementation(impl));
                Assert.IsTrue(obj.SetDefaultImplementation(impl));
                Assert.AreEqual(impl, obj.GetDefaultImplementation());
                Assert.IsTrue(obj.HasDefaultImplementation());
            }

            /************************************************************
             * Test property functions
             ************************************************************/
            using (obj = CreateObject("theobj")) {
                using(var obj2 = CreateObject("otherobj")) {
                    // Make a property and connect it from obj to obj2.
                    var prop = FbxProperty.Create(obj, Globals.FbxBoolDT, "maybe");
                    var prop2 = FbxProperty.Create(obj, Globals.FbxFloatDT, "probability");

                    Assert.IsTrue(obj.ConnectSrcProperty(prop));
                    Assert.IsTrue(obj.ConnectSrcProperty(prop2));
                    Assert.IsTrue(obj2.ConnectDstProperty(prop));

                    Assert.IsTrue(obj.IsConnectedSrcProperty(prop));
                    Assert.IsTrue(obj2.IsConnectedDstProperty(prop));

                    Assert.AreEqual(2, obj.GetSrcPropertyCount());
                    Assert.AreEqual(1, obj2.GetDstPropertyCount());

                    Assert.AreEqual(prop, obj.GetSrcProperty());
                    Assert.AreEqual(prop, obj.GetSrcProperty(0));
                    Assert.AreEqual(prop2, obj.GetSrcProperty(1));
                    Assert.AreEqual(prop, obj2.GetDstProperty());
                    Assert.AreEqual(prop, obj2.GetDstProperty(0));

                    Assert.AreEqual(prop, obj.FindSrcProperty("maybe"));
                    Assert.AreEqual(prop, obj2.FindDstProperty("maybe"));
                    Assert.IsFalse(obj.FindSrcProperty("maybe", 1).IsValid());
                    Assert.IsFalse(obj2.FindDstProperty("maybe", 1).IsValid());

                    // Iterating over properties
                    Assert.IsTrue(obj.GetFirstProperty().IsValid());
                    Assert.IsTrue(obj.GetNextProperty(obj.GetFirstProperty()).IsValid());
                    Assert.IsTrue(obj.GetClassRootProperty().IsValid());

                    // FindProperty
                    Assert.AreEqual(prop, obj.FindProperty("maybe"));
                    Assert.AreEqual(prop, obj.FindProperty("mayBE", false));
                    Assert.IsFalse(obj.FindProperty("mayBE", true).IsValid());
                    Assert.AreEqual(prop, obj.FindProperty("maybe", Globals.FbxBoolDT));
                    Assert.AreEqual(prop, obj.FindProperty("mayBE", Globals.FbxBoolDT, false));

                    // FindPropertyHierarchical
                    Assert.AreEqual(prop, obj.FindPropertyHierarchical("maybe"));
                    Assert.AreEqual(prop, obj.FindPropertyHierarchical("mayBE", false));
                    Assert.IsFalse(obj.FindPropertyHierarchical("mayBE", true).IsValid());
                    Assert.AreEqual(prop, obj.FindPropertyHierarchical("maybe", Globals.FbxBoolDT));
                    Assert.AreEqual(prop, obj.FindPropertyHierarchical("mayBE", Globals.FbxBoolDT, false));

                    // Disconnecting
                    int nSrc = obj.GetSrcPropertyCount();
                    int nDst = obj2.GetDstPropertyCount();

                    Assert.IsTrue(obj.DisconnectSrcProperty(prop));
                    Assert.IsTrue(obj2.DisconnectDstProperty(prop));

                    Assert.AreEqual(nSrc - 1, obj.GetSrcPropertyCount());
                    Assert.AreEqual(nDst - 1, obj2.GetDstPropertyCount());
                }
            }

            /************************************************************
             * Test object connection functions
             ************************************************************/

            // need to order them this way for FbxScene, which deletes obj if Source Object is destroyed
            using (var ownerObj = CreateObject ("ownerObj")) {
                using (obj = CreateObject ("obj")) {
                    // Test ConnectSrcObject functions
                    int origCount = ownerObj.GetSrcObjectCount ();

                    bool result = ownerObj.ConnectSrcObject (obj);
                    Assert.IsTrue (result);
                    Assert.IsTrue (ownerObj.IsConnectedSrcObject (obj));
                    Assert.AreEqual (origCount + 1, ownerObj.GetSrcObjectCount ());
                    if (origCount == 0) {
                        Assert.AreEqual (obj, ownerObj.GetSrcObject ());
                    } else {
                        // FbxScene has more than one object set as source
                        Assert.AreNotEqual (obj, ownerObj.GetSrcObject ());
                    }
                    Assert.AreEqual (obj, ownerObj.GetSrcObject (origCount));
                    Assert.AreEqual (obj, ownerObj.FindSrcObject ("obj"));
                    Assert.IsNull (ownerObj.FindSrcObject ("obj", origCount + 1));

                    // TODO: Fix so this doesn't crash
                    /*Assert.That (() => {
                        ownerObj.FindSrcObject (null);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());*/

                    result = ownerObj.DisconnectSrcObject (obj);
                    Assert.IsTrue (result);
                    Assert.IsFalse (ownerObj.IsConnectedSrcObject (obj));

                    Assert.That (() => {
                        ownerObj.ConnectSrcObject (null);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());

                    result = ownerObj.ConnectSrcObject (obj, FbxConnection.EType.eData);
                    Assert.IsTrue (result);
                }
            }

            // need to order them this way for FbxScene, which deletes ownerObj if Destination Object is destroyed
            using (obj = CreateObject ("obj")) {
                using (var ownerObj = CreateObject ("ownerObj")) {
                    // Test ConnectDstObject functions
                    int origCount = ownerObj.GetDstObjectCount ();

                    bool result = ownerObj.ConnectDstObject (obj);
                    Assert.IsTrue (result);
                    Assert.IsTrue (ownerObj.IsConnectedDstObject (obj));
                    Assert.AreEqual (origCount + 1, ownerObj.GetDstObjectCount ());
                    if (origCount == 0) {
                        Assert.AreEqual (obj, ownerObj.GetDstObject ());
                    } else {
                        // FbxAnimCurve has the scene as a DstObject
                        Assert.AreNotEqual (obj, ownerObj.GetDstObject ());
                    }
                    Assert.AreEqual (obj, ownerObj.GetDstObject (origCount));
                    Assert.AreEqual (obj, ownerObj.FindDstObject ("obj"));
                    Assert.IsNull (ownerObj.FindDstObject ("obj", origCount+1));

                    // TODO: Fix so this doesn't crash
                    /*Assert.That (() => {
                        ownerObj.FindDstObject (null);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());*/

                    result = ownerObj.DisconnectDstObject (obj);
                    Assert.IsTrue (result);
                    Assert.IsFalse (ownerObj.IsConnectedDstObject (obj));

                    Assert.That (() => {
                        ownerObj.ConnectDstObject (null);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());

                    result = ownerObj.ConnectDstObject (obj, FbxConnection.EType.eData);
                    Assert.IsTrue (result);
                }
            }
        }
    }
}
