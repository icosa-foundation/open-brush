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
    internal abstract class FbxConstraintTestBase<T> : Base<T> where T : FbxConstraint
    {
        protected virtual FbxConstraint.EType ConstraintType { get { return FbxConstraint.EType.eUnknown; } }

        [Test]
        public virtual void TestBasics ()
        {
            T constraint = CreateObject ("constraint");

            TestGetter (constraint.Active);
            TestGetter (constraint.Lock);
            TestGetter (constraint.Weight);
            TestGetter (constraint.GetConstrainedObject ());
            TestGetter (constraint.GetConstraintSource (-1));
            TestGetter (constraint.GetConstraintSource (0));
            TestGetter (constraint.GetSourceWeight (FbxNode.Create (Manager, "Node")));
            Assert.That (() => constraint.GetSourceWeight (null), Throws.Exception.TypeOf<System.ArgumentNullException> ());
            Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (0));
            Assert.That (constraint.GetConstraintType (), Is.EqualTo (ConstraintType));
        }
    }

    /// <summary>
    /// For testing functions that classes that derive from FbxConstraint share, but are not implemented in FbxConstraint.
    /// </summary>
    internal abstract class FbxConstraintDescendantTestBase<T> : FbxConstraintTestBase<T> where T : FbxConstraint
    {
        static System.Reflection.MethodInfo s_AddConstraintSource;
        static System.Reflection.MethodInfo s_AddConstraintSourceDouble;
        static System.Reflection.MethodInfo s_SetConstrainedObject;

        static FbxConstraintDescendantTestBase ()
        {
            s_AddConstraintSource = typeof(T).GetMethod ("AddConstraintSource", new System.Type[] { typeof(FbxObject) });
            s_AddConstraintSourceDouble = typeof(T).GetMethod ("AddConstraintSource", new System.Type[] {
                typeof(FbxObject),
                typeof(double)
            });
            s_SetConstrainedObject = typeof(T).GetMethod ("SetConstrainedObject", new System.Type[] { typeof(FbxObject) });
        }

        public void AddConstraintSourceDouble (T instance, FbxObject obj, double weight)
        {
            Invoker.Invoke (s_AddConstraintSourceDouble, instance, obj, weight);
        }

        public void AddConstraintSource (T instance, FbxObject obj)
        {
            Invoker.Invoke (s_AddConstraintSource, instance, obj);
        }

        public void SetConstrainedObject (T instance, FbxObject obj)
        {
            Invoker.Invoke (s_SetConstrainedObject, instance, obj);
        }

        [Test]
        public virtual void TestAddConstraintSource ()
        {
            using (var constraint = CreateObject ("constraint")) {
                Assert.That (() => AddConstraintSource (constraint, null), Throws.Exception.TypeOf<System.ArgumentNullException> ());
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (0));

                var fbxNode = FbxNode.Create (Manager, "rootnode");

                AddConstraintSource (constraint, fbxNode);
                Assert.That (constraint.GetConstraintSource (0), Is.EqualTo (fbxNode));
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (1));

                fbxNode = FbxNode.Create (Manager, "node2");
                AddConstraintSourceDouble (constraint, fbxNode, 2.0);
                Assert.That (constraint.GetConstraintSource (1), Is.EqualTo (fbxNode));
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (2));
            }
        }

        [Test]
        public virtual void TestSetConstrainedObject ()
        {
            if (ConstraintType == FbxConstraint.EType.eUnknown) {
                return;
            }

            using (var constraint = CreateObject ("constraint")) {
                Assert.That (() => SetConstrainedObject (constraint, null), Throws.Exception.TypeOf<System.ArgumentNullException> ());

                var fbxNode = FbxNode.Create (Manager, "rootnode");

                SetConstrainedObject (constraint, fbxNode);
                Assert.That (constraint.GetConstrainedObject (), Is.EqualTo (fbxNode));
            }
        }
    }


    internal class FbxConstraintTest : FbxConstraintTestBase<FbxConstraint>
    {

    }

    internal class FbxConstraintAimTest : FbxConstraintDescendantTestBase<FbxConstraintAim>
    {
        protected override FbxConstraint.EType ConstraintType {
            get {
                return FbxConstraint.EType.eAim;
            }
        }

        [Test]
        public void TestGetters ()
        {
            using (var constraint = FbxConstraintAim.Create (Manager, "aimConstraint")) {
                TestGetter (constraint.AffectX);
                TestGetter (constraint.AffectY);
                TestGetter (constraint.AffectZ);
                TestGetter (constraint.AimVector);
                TestGetter (constraint.RotationOffset);
                TestGetter (constraint.UpVector);
                TestGetter (constraint.WorldUpType);
                TestGetter (constraint.WorldUpVector);
            }
        }

        [Test]
        public void TestWorldUpObject ()
        {
            using (var constraint = FbxConstraintAim.Create (Manager, "aimConstraint")) {
                Assert.That (() => constraint.SetWorldUpObject (null), Throws.Exception.TypeOf<System.ArgumentNullException> ());

                var fbxNode = FbxNode.Create (Manager, "rootnode");

                constraint.SetWorldUpObject (fbxNode);
                Assert.That (constraint.GetWorldUpObject (), Is.EqualTo (fbxNode));
            }
        }
    }

    internal class FbxConstraintParentTest : FbxConstraintDescendantTestBase<FbxConstraintParent>
    {
        protected override FbxConstraint.EType ConstraintType {
            get {
                return FbxConstraint.EType.eParent;
            }
        }

        [Test]
        public void TestGetters ()
        {
            using (var constraint = FbxConstraintParent.Create (Manager, "pConstraint")) {
                TestGetter (constraint.AffectRotationX);
                TestGetter (constraint.AffectRotationY);
                TestGetter (constraint.AffectRotationZ);
                TestGetter (constraint.AffectScalingX);
                TestGetter (constraint.AffectScalingY);
                TestGetter (constraint.AffectScalingZ);
                TestGetter (constraint.AffectTranslationX);
                TestGetter (constraint.AffectTranslationY);
                TestGetter (constraint.AffectTranslationZ);
            }
        }

        [Test]
        public void TestSetTranslationOffset()
        {
            using (var constraint = FbxConstraintParent.Create(Manager, "pConstraint"))
            {
                // test valid input
                var fbxNode = FbxNode.Create(Manager, "rootnode");
                var fbxNode2 = FbxNode.Create(Manager, "node2");

                var offset = new FbxVector4(1, 2, 3);
                constraint.AddConstraintSource(fbxNode);
                constraint.SetTranslationOffset(fbxNode, offset);

                var offset2 = new FbxVector4(0.5, 0.5, 0.25);
                constraint.AddConstraintSource(fbxNode2, 2.0);
                constraint.SetTranslationOffset(fbxNode2, offset2);

                Assert.That(constraint.GetTranslationOffset(fbxNode), Is.EqualTo(offset));
                Assert.That(constraint.GetTranslationOffset(fbxNode2), Is.EqualTo(offset2));
                Assert.That(constraint.GetTranslationOffsetProperty(fbxNode2).IsValid(), Is.True);

                // test null input
                Assert.That(() => constraint.SetTranslationOffset(null, offset), Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That(() => constraint.GetTranslationOffset(null), Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That(() => constraint.GetTranslationOffsetProperty(null), Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test on non source fbx node
                var fbxNode3 = FbxNode.Create(Manager, "node3");
                var offset3 = new FbxVector4(1, 1, 1);
                
                Assert.That(() => constraint.SetTranslationOffset(fbxNode3, offset3), Throws.Nothing);
                Assert.That(constraint.GetTranslationOffset(fbxNode3), Is.EqualTo(new FbxVector4(0,0,0)));
            }
        }

        [Test]
        public void TestSetRotationOffset()
        {
            using (var constraint = FbxConstraintParent.Create(Manager, "pConstraint"))
            {
                // test valid input
                var fbxNode = FbxNode.Create(Manager, "rootnode");
                var fbxNode2 = FbxNode.Create(Manager, "node2");

                var offset = new FbxVector4(1, 2, 3);
                constraint.AddConstraintSource(fbxNode);
                constraint.SetRotationOffset(fbxNode, offset);

                var offset2 = new FbxVector4(0.5, 0.5, 0.25);
                constraint.AddConstraintSource(fbxNode2, 2.0);
                constraint.SetRotationOffset(fbxNode2, offset2);

                Assert.That(constraint.GetRotationOffset(fbxNode), Is.EqualTo(offset));
                Assert.That(constraint.GetRotationOffset(fbxNode2), Is.EqualTo(offset2));
                Assert.That(constraint.GetRotationOffsetProperty(fbxNode2).IsValid(), Is.True);

                // test null input
                Assert.That(() => constraint.SetRotationOffset(null, offset), Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That(() => constraint.GetRotationOffset(null), Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That(() => constraint.GetRotationOffsetProperty(null), Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test on non source fbx node
                var fbxNode3 = FbxNode.Create(Manager, "node3");
                var offset3 = new FbxVector4(1, 1, 1);

                Assert.That(() => constraint.SetRotationOffset(fbxNode3, offset3), Throws.Nothing);
                Assert.That(constraint.GetRotationOffset(fbxNode3), Is.EqualTo(new FbxVector4(0, 0, 0)));
            }
        }
    }

    internal class FbxConstraintPositionTest : FbxConstraintDescendantTestBase<FbxConstraintPosition>
    {
        protected override FbxConstraint.EType ConstraintType {
            get {
                return FbxConstraint.EType.ePosition;
            }
        }

        [Test]
        public void TestGetters ()
        {
            using (var constraint = FbxConstraintPosition.Create (Manager, "posConstraint")) {
                TestGetter (constraint.AffectX);
                TestGetter (constraint.AffectY);
                TestGetter (constraint.AffectZ);
                TestGetter (constraint.Translation);
            }
        }

        [Test]
        public override void TestAddConstraintSource ()
        {
            // overriding implementation because FbxConstraintPosition also has a RemoveConstraintSource() function

            using (var constraint = FbxConstraintPosition.Create (Manager, "pConstraint")) {
                Assert.That (() => constraint.AddConstraintSource (null), Throws.Exception.TypeOf<System.ArgumentNullException> ());
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (0));

                var fbxNode = FbxNode.Create (Manager, "rootnode");

                constraint.AddConstraintSource (fbxNode);
                Assert.That (constraint.GetConstraintSource (0), Is.EqualTo (fbxNode));
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (1));

                var fbxNode2 = FbxNode.Create (Manager, "node2");
                constraint.AddConstraintSource (fbxNode2, 2);
                Assert.That (constraint.GetConstraintSource (1), Is.EqualTo (fbxNode2));
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (2));

                Assert.That (() => constraint.RemoveConstraintSource (null), Throws.Exception.TypeOf<System.ArgumentNullException> ());

                constraint.RemoveConstraintSource (fbxNode);
                Assert.That (constraint.GetConstraintSourceCount (), Is.EqualTo (1));
                Assert.That (constraint.GetConstraintSource (0), Is.EqualTo (fbxNode2));
            }
        }
    }

    internal class FbxConstraintRotationTest : FbxConstraintDescendantTestBase<FbxConstraintRotation>
    {
        protected override FbxConstraint.EType ConstraintType {
            get {
                return FbxConstraint.EType.eRotation;
            }
        }

        [Test]
        public void TestGetters ()
        {
            using (var constraint = FbxConstraintRotation.Create (Manager, "rConstraint")) {
                TestGetter (constraint.AffectX);
                TestGetter (constraint.AffectY);
                TestGetter (constraint.AffectZ);
                TestGetter (constraint.Rotation);
            }
        }
    }

    internal class FbxConstraintScaleTest : FbxConstraintDescendantTestBase<FbxConstraintScale>
    {
        protected override FbxConstraint.EType ConstraintType {
            get {
                return FbxConstraint.EType.eScale;
            }
        }

        [Test]
        public void TestGetters ()
        {
            using (var constraint = FbxConstraintScale.Create (Manager, "sConstraint")) {
                TestGetter (constraint.AffectX);
                TestGetter (constraint.AffectY);
                TestGetter (constraint.AffectZ);
                TestGetter (constraint.Scaling);
            }
        }
    }
}