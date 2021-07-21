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
    internal class FbxPropertyTest : TestBase<FbxProperty>
    {
        [Test]
        public void TestEquality() {
            using(var manager = FbxManager.Create()) {
                // FbxProperty
                var node = FbxNode.Create(manager, "node");
                var prop1 = FbxProperty.Create(node, Globals.FbxBoolDT, "bool1");
                var prop2 = FbxProperty.Create(node, Globals.FbxBoolDT, "bool2");
                var prop1copy = node.FindProperty("bool1");
                EqualityTester<FbxProperty>.TestEquality(prop1, prop2, prop1copy);

                // FbxPropertyT<bool>
                var vis1 = node.VisibilityInheritance;
                var vis2 = FbxNode.Create(manager, "node2").VisibilityInheritance;
                var vis1copy = vis1; // TODO: node.FindProperty("Visibility Inheritance"); -- but that has a different proxy type
                EqualityTester<FbxPropertyBool>.TestEquality(vis1, vis2, vis1copy);

                // FbxPropertyT<EInheritType>
                var inhType1 = node.InheritType;
                var inhType2 = FbxNode.Create (manager, "node3").InheritType;
                var inhType1Copy = inhType1; // TODO: node.FindProperty("InheritType");
                EqualityTester<FbxPropertyEInheritType>.TestEquality (inhType1, inhType2, inhType1Copy);

                // FbxPropertyT<double>
                var lambert = FbxSurfaceLambert.Create(manager, "lambert");
                var emissiveCopy = lambert.EmissiveFactor; // TODO: lambert.FindProperty("EmissiveFactor");
                EqualityTester<FbxPropertyDouble>.TestEquality(lambert.EmissiveFactor, lambert.AmbientFactor, emissiveCopy);

                // FbxPropertyT<FbxDouble3>
                var lclTranslationCopy = node.LclTranslation; // TODO: node.FindProperty("Lcl Translation");
                EqualityTester<FbxPropertyDouble3>.TestEquality(node.LclTranslation, node.LclRotation, lclTranslationCopy);

                // FbxPropertyT<float>
                var light = FbxLight.Create(manager, "light");
                EqualityTester<FbxPropertyFloat>.TestEquality(light.LeftBarnDoor, light.RightBarnDoor, light.LeftBarnDoor);

                // FbxPropertyT<int>
                var constraint = FbxConstraintAim.Create (manager, "constraint");
                var constraint2 = FbxConstraintAim.Create (manager, "constraint2");
                var worldUpTypeCopy = constraint.WorldUpType; // TODO: constraint.FindProperty("WorldUpType");
                EqualityTester<FbxPropertyInt>.TestEquality (constraint.WorldUpType, constraint2.WorldUpType, worldUpTypeCopy);

                // FbxPropertyT<> for FbxTexture enums
                var tex1 = FbxTexture.Create(manager, "tex1");
                var tex2 = FbxTexture.Create(manager, "tex2");
                var blendCopy = tex1.CurrentTextureBlendMode; // TODO: tex1.FindProperty(...)
                EqualityTester<FbxPropertyEBlendMode>.TestEquality(tex1.CurrentTextureBlendMode, tex2.CurrentTextureBlendMode, blendCopy);
                var wrapCopy = tex1.WrapModeU; // TODO: tex1.FindProperty(...)
                EqualityTester<FbxPropertyEWrapMode>.TestEquality(tex1.WrapModeU, tex2.WrapModeU, wrapCopy);

                // FbxPropertyT<FbxNull.ELook>
                var null1 = FbxNull.Create(manager, "null1");
                var null2 = FbxNull.Create(manager, "null2");
                EqualityTester<FbxPropertyNullELook>.TestEquality(null1.Look, null2.Look, null1.Look);

                // FbxPropertyT<FbxMarker.ELook>
                var marker1 = FbxMarker.Create(manager, "marker1");
                var marker2 = FbxMarker.Create(manager, "marker2");
                EqualityTester<FbxPropertyMarkerELook>.TestEquality(marker1.Look, marker2.Look, marker1.Look);

                // FbxPropertyT<string>
                var impl = FbxImplementation.Create(manager, "impl");
                var renderAPIcopy = impl.RenderAPI; // TODO: impl.FindProperty("RenderAPI");
                EqualityTester<FbxPropertyString>.TestEquality(impl.RenderAPI, impl.RenderAPIVersion, renderAPIcopy);

                // FbxPropertyT<> for FbxCamera enum EProjectionType
                var cam1 = FbxCamera.Create(manager, "cam1");
                var cam2 = FbxCamera.Create(manager, "cam2");
                var projectionCopy = cam1.ProjectionType;
                EqualityTester<FbxPropertyEProjectionType>.TestEquality(cam1.ProjectionType, cam2.ProjectionType, projectionCopy);

                // FbxPropertyT<> for FbxLight enum EType
                var light1 = FbxLight.Create(manager, "light1");
                var light2 = FbxLight.Create(manager, "light2");
                var typeCopy = light1.LightType;
                EqualityTester<FbxPropertyELightType>.TestEquality(light1.LightType, light2.LightType, typeCopy);
                var lightShapeCopy = light1.AreaLightShape;
                EqualityTester<FbxPropertyEAreaLightShape>.TestEquality(light1.AreaLightShape, light2.AreaLightShape, lightShapeCopy);
                var decayCopy = light1.DecayType;
                EqualityTester<FbxPropertyEDecayType>.TestEquality(light1.DecayType, light2.DecayType, decayCopy);
                var floatCopy = light1.LeftBarnDoor;
                EqualityTester<FbxPropertyFloat>.TestEquality(light1.LeftBarnDoor, light2.LeftBarnDoor, floatCopy);
            }
        }

        // tests that should work for any subclass of FbxProperty
        public static void GenericPropertyTests<T>(T property, FbxObject parent, string propertyName, FbxDataType dataType) where T:FbxProperty{
            Assert.IsTrue(property.IsValid());
            Assert.AreEqual(dataType, property.GetPropertyDataType());
            Assert.AreEqual(propertyName, property.GetName());
            Assert.AreEqual(propertyName, property.ToString());
            Assert.AreEqual(propertyName, property.GetHierarchicalName());
            Assert.AreEqual(propertyName, property.GetLabel(true));
            property.SetLabel("label");
            Assert.AreEqual("label", property.GetLabel());
            Assert.AreEqual(parent, property.GetFbxObject());
            Assert.AreEqual(property.GetFbxObject(), parent); // test it both ways just in case equals is busted

            // test the flags using the animatable flag
            property.ModifyFlag(FbxPropertyFlags.EFlags.eAnimatable, true);
            Assert.IsTrue(property.GetFlag(FbxPropertyFlags.EFlags.eAnimatable));
            Assert.AreNotEqual(0, property.GetFlags() | FbxPropertyFlags.EFlags.eAnimatable);
            property.SetFlagInheritType(FbxPropertyFlags.EFlags.eAnimatable, FbxPropertyFlags.EInheritType.eInherit);
            Assert.AreEqual(FbxPropertyFlags.EInheritType.eInherit, property.GetFlagInheritType(FbxPropertyFlags.EFlags.eAnimatable));

            // not clear when this ever returns true: whether we set animatable
            // to true or false it says it has the default value.
            Assert.IsFalse(property.ModifiedFlag(FbxPropertyFlags.EFlags.eAnimatable));

            // Test setting the value with the generic float accessor.
            // The value may not round-trip: a bool property will go to 1.0
            property.Set(5.0f);
            TestGetter (property.GetFloat());
            TestGetter (property.GetBool ());
            TestGetter (property.GetDouble ());
            TestGetter (property.GetFbxColor ());
            TestGetter (property.GetFbxDouble3 ());
            TestGetter (property.GetString ());
            TestGetter (property.GetInt ());

            // Test setting the value with color accessor
            property.Set (new FbxColor ());

            // Test setting the value with string accessor
            property.Set ("MyCustomProperty");

            // test GetCurve(). Just make sure it doesn't crash. We can't
            // generically test actually getting curves, because the details
            // (channel names etc) depend on the type of property and its
            // flags.
            FbxAnimLayer layer = FbxAnimLayer.Create(parent, "layer");
            property.GetCurve (layer);
            property.GetCurve (layer, true);
            property.GetCurve (layer, "asdf");
            property.GetCurve (layer, "asdf", true);
            property.GetCurve (layer, "asdf", "hjkl", true);
            Assert.That (() => { property.GetCurve(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // test GetCurveNode() (make sure it doesn't crash)
            FbxAnimCurveNode curveNode = property.GetCurveNode();
            Assert.IsNull (curveNode); // didn't create one so should be null

            curveNode = property.GetCurveNode (true);
            // TODO: figure out why the curve node doesn't get created
            //Assert.IsNotNull (curveNode);

            property.GetCurveNode (FbxAnimStack.Create (parent, "anim stack"));
            property.GetCurveNode (FbxAnimStack.Create (parent, "anim stack"), true);
            property.GetCurveNode (FbxAnimLayer.Create (parent, "anim layer"));
            property.GetCurveNode (FbxAnimLayer.Create (parent, "anim layer"), true);

            Assert.That (() => { property.GetCurveNode((FbxAnimStack)null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            Assert.That (() => { property.GetCurveNode((FbxAnimLayer)null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            using (FbxManager manager = FbxManager.Create ()) {
                // Test ConnectSrcObject functions
                FbxObject obj = FbxObject.Create (manager, "obj");
                bool result = property.ConnectSrcObject (obj);
                Assert.IsTrue (result);
                Assert.IsTrue (property.IsConnectedSrcObject (obj));
                Assert.AreEqual (1, property.GetSrcObjectCount ());
                Assert.AreEqual (obj, property.GetSrcObject ());
                Assert.AreEqual (obj, property.GetSrcObject (0));
                Assert.AreEqual (obj, property.FindSrcObject ("obj"));
                Assert.IsNull (property.FindSrcObject ("obj", 1));
                Assert.That (() => { property.FindSrcObject(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                Assert.IsTrue (property.DisconnectSrcObject (obj));
                Assert.IsFalse (property.IsConnectedSrcObject (obj));

                Assert.That (() => { property.ConnectSrcObject(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                Assert.IsTrue (property.ConnectSrcObject (obj, FbxConnection.EType.eData));

                Assert.IsTrue(property.DisconnectAllSrcObject());

                // Test ConnectDstObject functions
                result = property.ConnectDstObject (obj);
                Assert.IsTrue (result);
                Assert.IsTrue (property.IsConnectedDstObject (obj));
                Assert.AreEqual (1, property.GetDstObjectCount ());
                Assert.AreEqual (obj, property.GetDstObject ());
                Assert.AreEqual (obj, property.GetDstObject (0));
                Assert.AreEqual (obj, property.FindDstObject ("obj"));
                Assert.IsNull (property.FindDstObject ("obj", 1));
                Assert.That (() => { property.FindDstObject(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                Assert.IsTrue (property.DisconnectDstObject (obj));
                Assert.IsFalse (property.IsConnectedDstObject (obj));

                Assert.That (() => { property.ConnectDstObject(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                Assert.IsTrue (property.ConnectDstObject (obj, FbxConnection.EType.eData));

                Assert.IsTrue(property.DisconnectAllDstObject());
            }

            // verify this in the future: will dispose destroy?
            property.Dispose();
        }

        [Test]
        public void BasicTests ()
        {
            using (var manager = FbxManager.Create()) {
                // FbxPropertyT<FbxBool> example: VisibilityInheritance on a node
                var node = FbxNode.Create(manager, "node");
                GenericPropertyTests<FbxPropertyBool> (node.VisibilityInheritance, node, "Visibility Inheritance", Globals.FbxVisibilityInheritanceDT);

                var property = node.VisibilityInheritance;
                property.Set(false);
                Assert.AreEqual(false, property.Get());
                Assert.AreEqual(false, property.EvaluateValue());
                Assert.AreEqual(false, property.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(false, property.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using(var manager = FbxManager.Create()) {
                // FbxPropertyT<FbxDouble> example: several of them on a Lambert shader
                var obj = FbxSurfaceLambert.Create(manager, "lambert");
                GenericPropertyTests<FbxPropertyDouble> (obj.EmissiveFactor, obj, "EmissiveFactor", Globals.FbxDoubleDT);

                var property = obj.EmissiveFactor;
                property.Set(5.0); // bool Set<float> is not accessible here!
                Assert.AreEqual(5.0, property.Get());
                Assert.AreEqual(5.0, property.EvaluateValue());
                Assert.AreEqual(5.0, property.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(5.0, property.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using(var manager = FbxManager.Create()) {
                // FbxPropertyT<Double3> example: the LclTranslation on a node
                var node = FbxNode.Create(manager, "node");
                GenericPropertyTests<FbxPropertyDouble3> (node.LclTranslation, node, "Lcl Translation", Globals.FbxLocalTranslationDT);

                var property = node.LclTranslation;
                property.Set(new FbxDouble3(1,2,3));
                Assert.AreEqual(new FbxDouble3(1, 2, 3), property.Get());
                Assert.AreEqual(new FbxDouble3(1, 2, 3), property.EvaluateValue());
                Assert.AreEqual(new FbxDouble3(1, 2, 3), property.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(new FbxDouble3(1, 2, 3), property.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using(var manager = FbxManager.Create()) {
                // FbxPropertyT<float> example: the LeftBarnDoor on a light
                var light = FbxLight.Create(manager, "light");
                GenericPropertyTests(light.LeftBarnDoor, light, "LeftBarnDoor", Globals.FbxFloatDT);

                var property = light.LeftBarnDoor;
                light.LeftBarnDoor.Set(5.0f);
                Assert.AreEqual(5.0f, light.LeftBarnDoor.Get());
                Assert.AreEqual(5.0f, property.EvaluateValue());
                Assert.AreEqual(5.0f, property.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(5.0f, property.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create ()) {
                // FbxPropertyT<int> example: the WorldUpType on an aim constraint
                var constraint = FbxConstraintAim.Create (manager, "constraint");
                GenericPropertyTests (constraint.WorldUpType, constraint, "WorldUpType", Globals.FbxEnumDT);

                var property = constraint.WorldUpType;
                int value = (int)FbxConstraintAim.EWorldUp.eAimAtObjectUp;
                constraint.WorldUpType.Set (value);
                Assert.That (constraint.WorldUpType.Get (), Is.EqualTo (value));
                Assert.That (property.EvaluateValue (), Is.EqualTo (value));
                Assert.That (property.EvaluateValue (FbxTime.FromSecondDouble (5)), Is.EqualTo (value));
                Assert.That (property.EvaluateValue (FbxTime.FromSecondDouble (5), true), Is.EqualTo (value));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT<FbxString> example: the description of a shader implementation
                var impl = FbxImplementation.Create(manager, "name");
                GenericPropertyTests<FbxPropertyString> (impl.RenderAPI, impl, "RenderAPI", Globals.FbxStringDT);

                var property = impl.RenderAPI;
                property.Set("a value");
                Assert.AreEqual("a value", property.Get());

                // animated strings come out as empty-string
                Assert.AreEqual("", property.EvaluateValue());
                Assert.AreEqual("", property.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual("", property.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT for FbxTexture enums EBlendMode and EWrapMode
                var tex = FbxTexture.Create(manager, "tex");

                FbxPropertyTest.GenericPropertyTests(tex.CurrentTextureBlendMode, tex, "CurrentTextureBlendMode", Globals.FbxEnumDT);
                tex.CurrentTextureBlendMode.Set(FbxTexture.EBlendMode.eAdditive);
                Assert.AreEqual(FbxTexture.EBlendMode.eAdditive, tex.CurrentTextureBlendMode.Get());
                Assert.AreEqual(FbxTexture.EBlendMode.eAdditive, tex.CurrentTextureBlendMode.EvaluateValue());
                Assert.AreEqual(FbxTexture.EBlendMode.eAdditive, tex.CurrentTextureBlendMode.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxTexture.EBlendMode.eAdditive, tex.CurrentTextureBlendMode.EvaluateValue(FbxTime.FromSecondDouble(5), true));

                FbxPropertyTest.GenericPropertyTests(tex.WrapModeU, tex, "WrapModeU", Globals.FbxEnumDT);
                tex.WrapModeU.Set(FbxTexture.EWrapMode.eClamp);
                Assert.AreEqual(FbxTexture.EWrapMode.eClamp, tex.WrapModeU.Get());
                Assert.AreEqual(FbxTexture.EWrapMode.eClamp, tex.WrapModeU.EvaluateValue());
                Assert.AreEqual(FbxTexture.EWrapMode.eClamp, tex.WrapModeU.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxTexture.EWrapMode.eClamp, tex.WrapModeU.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT<FbxNull.ELook>
                var null1 = FbxNull.Create(manager, "null1");

                FbxPropertyTest.GenericPropertyTests(null1.Look, null1, "Look", Globals.FbxEnumDT);
                null1.Look.Set(FbxNull.ELook.eCross);
                Assert.AreEqual(FbxNull.ELook.eCross, null1.Look.Get());
                Assert.AreEqual(FbxNull.ELook.eCross, null1.Look.EvaluateValue());
                Assert.AreEqual(FbxNull.ELook.eCross, null1.Look.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxNull.ELook.eCross, null1.Look.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT<FbxMarker.ELook>
                var marker1 = FbxMarker.Create(manager, "marker1");

                FbxPropertyTest.GenericPropertyTests(marker1.Look, marker1, "Look", Globals.FbxEnumDT);
                marker1.Look.Set(FbxMarker.ELook.eCapsule);
                Assert.AreEqual(FbxMarker.ELook.eCapsule, marker1.Look.Get());
                Assert.AreEqual(FbxMarker.ELook.eCapsule, marker1.Look.EvaluateValue());
                Assert.AreEqual(FbxMarker.ELook.eCapsule, marker1.Look.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxMarker.ELook.eCapsule, marker1.Look.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT for FbxCamera enum EProjectionType
                var camera = FbxCamera.Create(manager, "camera");

                FbxPropertyTest.GenericPropertyTests(camera.ProjectionType, camera, "CameraProjectionType", Globals.FbxEnumDT);
                camera.ProjectionType.Set(FbxCamera.EProjectionType.ePerspective);
                Assert.AreEqual(FbxCamera.EProjectionType.ePerspective, camera.ProjectionType.Get());
                Assert.AreEqual(FbxCamera.EProjectionType.ePerspective, camera.ProjectionType.EvaluateValue());
                Assert.AreEqual(FbxCamera.EProjectionType.ePerspective, camera.ProjectionType.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxCamera.EProjectionType.ePerspective, camera.ProjectionType.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT for FbxCamera enum EGateFit
                var camera = FbxCamera.Create(manager, "camera");

                FbxPropertyTest.GenericPropertyTests(camera.GateFit, camera, "GateFit", Globals.FbxEnumDT);
                camera.GateFit.Set(FbxCamera.EGateFit.eFitHorizontal);
                Assert.AreEqual(FbxCamera.EGateFit.eFitHorizontal, camera.GateFit.Get());
                Assert.AreEqual(FbxCamera.EGateFit.eFitHorizontal, camera.GateFit.EvaluateValue());
                Assert.AreEqual(FbxCamera.EGateFit.eFitHorizontal, camera.GateFit.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxCamera.EGateFit.eFitHorizontal, camera.GateFit.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT<EInheritType>
                var node = FbxNode.Create(manager, "node");

                FbxPropertyTest.GenericPropertyTests(node.InheritType, node, "InheritType", Globals.FbxEnumDT);
                node.InheritType.Set(FbxTransform.EInheritType.eInheritRSrs);
                Assert.AreEqual(FbxTransform.EInheritType.eInheritRSrs, node.InheritType.Get());
                Assert.AreEqual(FbxTransform.EInheritType.eInheritRSrs, node.InheritType.EvaluateValue());
                Assert.AreEqual(FbxTransform.EInheritType.eInheritRSrs, node.InheritType.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxTransform.EInheritType.eInheritRSrs, node.InheritType.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // FbxPropertyT for FbxLight enums
                var light = FbxLight.Create(manager, "light");

                FbxPropertyTest.GenericPropertyTests(light.LightType, light, "LightType", Globals.FbxEnumDT);
                light.LightType.Set(FbxLight.EType.eSpot);
                Assert.AreEqual(FbxLight.EType.eSpot, light.LightType.Get());
                Assert.AreEqual(FbxLight.EType.eSpot, light.LightType.EvaluateValue());
                Assert.AreEqual(FbxLight.EType.eSpot, light.LightType.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxLight.EType.eSpot, light.LightType.EvaluateValue(FbxTime.FromSecondDouble(5), true));

                FbxPropertyTest.GenericPropertyTests(light.AreaLightShape, light, "AreaLightShape", Globals.FbxEnumDT);
                light.AreaLightShape.Set(FbxLight.EAreaLightShape.eSphere);
                Assert.AreEqual(FbxLight.EAreaLightShape.eSphere, light.AreaLightShape.Get());
                Assert.AreEqual(FbxLight.EAreaLightShape.eSphere, light.AreaLightShape.EvaluateValue());
                Assert.AreEqual(FbxLight.EAreaLightShape.eSphere, light.AreaLightShape.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxLight.EAreaLightShape.eSphere, light.AreaLightShape.EvaluateValue(FbxTime.FromSecondDouble(5), true));

                FbxPropertyTest.GenericPropertyTests(light.DecayType, light, "DecayType", Globals.FbxEnumDT);
                light.DecayType.Set(FbxLight.EDecayType.eCubic);
                Assert.AreEqual(FbxLight.EDecayType.eCubic, light.DecayType.Get());
                Assert.AreEqual(FbxLight.EDecayType.eCubic, light.DecayType.EvaluateValue());
                Assert.AreEqual(FbxLight.EDecayType.eCubic, light.DecayType.EvaluateValue(FbxTime.FromSecondDouble(5)));
                Assert.AreEqual(FbxLight.EDecayType.eCubic, light.DecayType.EvaluateValue(FbxTime.FromSecondDouble(5), true));
            }

            using (var manager = FbxManager.Create()) {
                // Test all the create and destroy operations
                FbxProperty root, child;
                var obj = FbxObject.Create(manager, "obj");

                Assert.IsNotNull(FbxProperty.Create(obj, Globals.FbxStringDT, "a"));
                Assert.IsNotNull(FbxProperty.Create(obj, Globals.FbxStringDT, "b", "label"));
                Assert.IsNotNull(FbxProperty.Create(obj, Globals.FbxStringDT, "c", "label", false));
                bool didFind;
                Assert.IsNotNull(FbxProperty.Create(obj, Globals.FbxStringDT, "c", "label", true, out didFind));
                Assert.IsTrue(didFind);

                root = FbxProperty.Create(obj, Globals.FbxCompoundDT, "root");

                child = FbxProperty.Create(root, Globals.FbxStringDT, "a");
                Assert.IsNotNull(child);
                Assert.IsNotNull(FbxProperty.Create(root, Globals.FbxStringDT, "b", "label"));
                Assert.IsNotNull(FbxProperty.Create(root, Globals.FbxStringDT, "c", "label", false));
                Assert.IsNotNull(FbxProperty.Create(root, Globals.FbxStringDT, "c", "label", true, out didFind));
                Assert.IsTrue(didFind);

                child.Destroy();

                root.DestroyChildren();
                Assert.IsNotNull(FbxProperty.Create(root, Globals.FbxStringDT, "c", "label", true, out didFind));
                Assert.IsFalse(didFind);

                root.DestroyRecursively();
            }
        }
    }
}
