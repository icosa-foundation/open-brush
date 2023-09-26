// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UseCaseTests
{
    internal class CustomPropertiesExportTest : HierarchyExportTest
    {
        protected string m_customPropName = "customProp";

        protected override FbxScene CreateScene (FbxManager manager)
        {
            FbxScene scene = base.CreateScene (manager);

            AddCustomProperties (scene.GetRootNode ().GetChild (0), m_customPropName, 1);

            return scene;
        }

        private void AddCustomProperties(FbxNode fbxNode, string propName, int propValue)
        {
            var fbxProperty = FbxProperty.Create(fbxNode, Globals.FbxIntDT, propName);
            Assert.IsTrue (fbxProperty.IsValid ());
            fbxProperty.Set (propValue);

            // Must be marked user-defined or it won't be shown in most DCCs
            fbxProperty.ModifyFlag(FbxPropertyFlags.EFlags.eUserDefined, true);
            fbxProperty.ModifyFlag(FbxPropertyFlags.EFlags.eAnimatable, true);

            for (int i = 0; i < fbxNode.GetChildCount (); i++) {
                AddCustomProperties (fbxNode.GetChild (i), propName, propValue + 1);
            }
        }

        protected override void CheckScene (FbxScene scene)
        {
            base.CheckScene (scene);

            FbxScene origScene = CreateScene (FbxManager);

            FbxNode origRoot = origScene.GetRootNode ().GetChild (0);
            FbxNode importRoot = scene.GetRootNode ().GetChild (0);

            CheckCustomProperties (origRoot, importRoot, m_customPropName);
        }

        private void CheckCustomProperties(FbxNode origNode, FbxNode importNode, string propName)
        {
            var origProperty = origNode.FindProperty (propName);
            var importProperty = importNode.FindProperty (propName);

            Assert.IsNotNull (origProperty);
            Assert.IsNotNull (importProperty);
            Assert.IsTrue (origProperty.IsValid ());
            Assert.IsTrue (importProperty.IsValid ());

            Assert.AreEqual(origProperty.GetInt(), importProperty.GetInt());
            Assert.AreEqual(origProperty.GetFlag(FbxPropertyFlags.EFlags.eUserDefined), importProperty.GetFlag(FbxPropertyFlags.EFlags.eUserDefined));
            Assert.AreEqual (origProperty.GetFlag (FbxPropertyFlags.EFlags.eAnimatable), importProperty.GetFlag (FbxPropertyFlags.EFlags.eAnimatable));

            Assert.AreEqual (origNode.GetChildCount (), importNode.GetChildCount ());
            for (int i = 0; i < origNode.GetChildCount (); i++) {
                CheckCustomProperties (origNode.GetChild (i), importNode.GetChild (i), propName);
            }
        }
    }
}