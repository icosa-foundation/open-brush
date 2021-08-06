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
    internal class FbxGlobalSettingsTest : Base<FbxGlobalSettings>
    {
        [Test]
        public void TestBasics()
        {
            var scene = FbxScene.Create(Manager, "");
            var settings = scene.GetGlobalSettings();

            settings.SetAxisSystem(FbxAxisSystem.MayaYUp);
            var axes = settings.GetAxisSystem();
            Assert.AreEqual(axes, FbxAxisSystem.MayaYUp);

            settings.SetSystemUnit(FbxSystemUnit.m);
            var units = settings.GetSystemUnit();
            Assert.AreEqual(units, FbxSystemUnit.m);

            var settingsB = scene.GetGlobalSettings();
            Assert.AreEqual(settings, settingsB);

            var scene2 = FbxScene.Create(Manager, "");
            var settings2 = scene2.GetGlobalSettings();
            Assert.AreNotEqual(settings, settings2);

            // Cover all the equality and inequality operators
            Assert.That(settings != settings2);
            Assert.That(settings as FbxObject != settings2 as FbxObject);
            Assert.That(settings as FbxEmitter != settings2 as FbxEmitter);

            // test SetDefaultCamera
            settings.SetDefaultCamera("camera");
            Assert.AreEqual ("camera", settings.GetDefaultCamera ());

            // test SetAmbientColor
            settings.SetAmbientColor(new FbxColor(1,1,1));
            Assert.AreEqual (new FbxColor (1, 1, 1), settings.GetAmbientColor ());
			
            // test SetTimeMode
            settings.SetTimeMode(FbxTime.EMode.eFrames100);
            Assert.That(settings.GetTimeMode(), Is.EqualTo(FbxTime.EMode.eFrames100));
        }
    }
}
