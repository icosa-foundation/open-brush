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
    /// <summary>
    /// Run some tests that any vector type should be able to pass.
    /// If you add tests here, you probably want to add them to the other
    /// FbxDouble* test classes.
    /// </summary>
    internal class FbxAxisSystemTest : TestBase<FbxAxisSystem>
    {
        [Test]
        public void TestEquality() {
            var a = FbxAxisSystem.MayaZUp;
            var b = FbxAxisSystem.MayaYUp;
            var acopy = new FbxAxisSystem(FbxAxisSystem.EPreDefinedAxisSystem.eMayaZUp);
            EqualityTester<FbxAxisSystem>.TestEquality(a, b, acopy);
        }

        /// <summary>
        /// Test the basics. Subclasses should override and add some calls
        /// e.g. to excercise all the constructors.
        /// </summary>
        [Test]
        public void TestBasics()
        {
            // Use all the constants.
            using (FbxAxisSystem.MayaZUp) { }
            using (FbxAxisSystem.MayaYUp) { }
            using (FbxAxisSystem.Max) { }
            using (FbxAxisSystem.Motionbuilder) { }
            using (FbxAxisSystem.OpenGL) { }
            using (FbxAxisSystem.DirectX) { }
            using (FbxAxisSystem.Lightwave) { }

            // Use this one again (make sure we don't crash) */
            using (FbxAxisSystem.MayaZUp) { }

            // Test the copy constructor.
            var axes = new FbxAxisSystem(FbxAxisSystem.Lightwave);

            // Test equality functions.
            Assert.That(axes.GetHashCode(), Is.LessThan(0));
            Assert.AreEqual(FbxAxisSystem.Lightwave, axes);
            Assert.IsFalse(FbxAxisSystem.MayaZUp == axes);
            Assert.IsTrue(FbxAxisSystem.MayaZUp != axes);

            // Test the predefined-enum constructor.
            Assert.AreEqual(axes, new FbxAxisSystem(FbxAxisSystem.EPreDefinedAxisSystem.eLightwave));
            axes.Dispose();

            // Test the no-arg constructor.
            using (new FbxAxisSystem()) { }

            // Construct from the three axes. Test we can get the three axes, including the sign.
            axes = new FbxAxisSystem(
                FbxAxisSystem.EUpVector.eYAxis,
                FbxAxisSystem.EFrontVector.eParityOddNegative, // negative! check the sign goes through
                FbxAxisSystem.ECoordSystem.eLeftHanded);
            Assert.AreEqual(FbxAxisSystem.EUpVector.eYAxis, axes.GetUpVector());
            Assert.AreEqual(FbxAxisSystem.EFrontVector.eParityOddNegative, axes.GetFrontVector());
            Assert.AreEqual(FbxAxisSystem.ECoordSystem.eLeftHanded, axes.GetCoorSystem());

        }

        [Test]
        public void TestConvertScene()
        {
            var axes = new FbxAxisSystem(
                FbxAxisSystem.EUpVector.eYAxis,
                FbxAxisSystem.EFrontVector.eParityOddNegative, // negative! check the sign goes through
                FbxAxisSystem.ECoordSystem.eLeftHanded);
            using (var Manager = FbxManager.Create()) {
                var scene = FbxScene.Create(Manager, "scene");
                axes.ConvertScene(scene);
            }
        }

        [Test]
        public void TestDeepConvertScene()
        {
            var axes = new FbxAxisSystem(
                FbxAxisSystem.EUpVector.eYAxis,
                FbxAxisSystem.EFrontVector.eParityOddNegative, // negative! check the sign goes through
                FbxAxisSystem.ECoordSystem.eLeftHanded);
            using (var Manager = FbxManager.Create()) {
                var scene = FbxScene.Create(Manager, "scene");
                try {
                    axes.DeepConvertScene(scene);
                } catch(System.EntryPointNotFoundException) {
                    Assert.Ignore("Testing against FBX SDK that doesn't have DeepConvertScene");
                }
            }
        }

    }
}
