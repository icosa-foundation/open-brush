// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class MaquetteDocumentTests
    {
        private string m_Root;
        private string m_Project;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), $"OpenBrush-ImportTest-{Guid.NewGuid():N}");
            m_Project = Path.Combine(m_Root, "project");
            Directory.CreateDirectory(m_Project);
            Write("manifest.json.mq", new JObject { ["guid"] = "project", ["version"] = "V020_ImageSupportsMaterial" });
            Write("primitives.json", new JObject { ["version"] = "20", ["primitives"] = new JArray(
                new JObject { ["guid"] = "box", ["primitiveType"] = "ModelFile", ["future"] = 42 }) });
            Write("scenes.json", new JObject
            {
                ["version"] = "20", ["lastSelectedSceneGuid"] = "second",
                ["slides"] = new JArray(Scene("first", 0), Scene("second", 1))
            });
        }

        [TearDown]
        public void TearDown() { Directory.Delete(m_Root, true); }

        private static JObject Scene(string id, int order) => new JObject
        {
            ["sceneGuid"] = id, ["sceneName"] = id, ["sequenceIndex"] = order,
            ["treeRoots"] = new JArray(new JObject
            {
                ["entityStateGuid"] = $"{id}-group", ["targetEntityGuid"] = "implicit-group", ["visible"] = false,
                ["cachedTransform"] = new JObject { ["position"] = new JObject { ["x"] = 10 } },
                ["children"] = new JArray(new JObject
                {
                    ["entityStateGuid"] = $"{id}-box", ["targetEntityGuid"] = "box", ["visible"] = true,
                    ["cachedTransform"] = new JObject { ["scale"] = new JObject { ["x"] = 2, ["y"] = 3, ["z"] = -1 } }
                })
            })
        };

        private void Write(string name, JObject data) => File.WriteAllText(Path.Combine(m_Project, name), data.ToString());

        [Test]
        public void SlideCompositionIncludesMasterEntitiesAndUsesSelectedParentOverrides()
        {
            Write("primitives.json", JObject.Parse(@"{'version':20,'primitives':[{'guid':'box','primitiveType':'ModelFile'},
                {'guid':'stage-only','primitiveType':'ModelFile'}]}"));
            var master = Scene("master", 9);
            var masterGroup = master["treeRoots"][0];
            masterGroup["targetEntityGuid"] = "shared-group";
            masterGroup["visible"] = true;
            masterGroup["cachedTransform"] = JObject.Parse(@"{'position':{'x':100}}");
            ((JArray)masterGroup["children"]).Add(JObject.Parse(@"{'entityStateGuid':'stage-only-state',
                'targetEntityGuid':'stage-only','visible':true,'cachedTransform':{'position':{'x':2}}}"));
            var slide = Scene("slide", 0);
            var slideGroup = slide["treeRoots"][0];
            slideGroup["targetEntityGuid"] = "shared-group";
            slideGroup["visible"] = true;
            slideGroup["cachedTransform"] = JObject.Parse(@"{'position':{'x':10},'scale':{'x':3,'y':1,'z':1}}");
            slideGroup["children"][0]["cachedTransform"]["position"] = JObject.Parse(@"{'x':3}");
            Write("scenes.json", new JObject
            {
                ["version"] = 20, ["lastSelectedSceneGuid"] = "slide", ["applyMasterStageEnvironment"] = false,
                ["slides"] = new JArray(slide), ["stages"] = new JArray(master, Scene("other-stage", 0))
            });
            using (var document = new MaquetteDocument(m_Project))
            {
                var composed = document.ReadComposedStates(document.SelectScene());
                Assert.AreEqual(3, composed.Count);
                Assert.AreEqual(1, composed.Count(value => value.EntityId == "box"));
                var background = composed.Single(value => value.EntityId == "stage-only");
                Assert.AreEqual("slide-group", background.Parent.Id);
                Assert.AreEqual(16, new MaquettePlacement(background).Position[0], 1e-5);
                Assert.AreEqual(19, new MaquettePlacement(composed.Single(value => value.EntityId == "box")).Position[0], 1e-5);
                var rawMaster = document.ReadStates(document.Scenes.First(value => value.Collection == "stages"));
                Assert.AreEqual(102, new MaquettePlacement(rawMaster.Single(value => value.EntityId == "stage-only")).Position[0], 1e-5);
                Assert.AreEqual(3, document.ReadComposedStates(document.Scenes.First(value => value.Collection == "stages")).Count);
            }
        }

        [Test]
        public void LightsKeepSavedWorldRangeAndIndependentEnabledAndShadowFlags()
        {
            Write("lights.json", JObject.Parse(@"{'version':20,'lights':[{'guid':'light','LightType':'Spotlight'}]}"));
            var scene = Scene("light-scene", 0);
            var child = scene["treeRoots"][0]["children"][0];
            child["targetEntityGuid"] = "light";
            child["lightEntityState"] = JObject.Parse(@"{'Radius':3,'Intensity':4,'SpotAngle':35,'TurnedOn':false,'IsShadowEnabled':false}");
            Write("scenes.json", new JObject { ["version"] = 20, ["slides"] = new JArray(scene) });
            using (var document = new MaquetteDocument(m_Project))
            {
                var state = document.ReadStates(document.SelectScene())[1];
                var light = MaquetteLightAdapter.Read(state, 10);
                Assert.IsTrue(light.IsSpot);
                Assert.AreEqual(30, light.Range);
                Assert.AreEqual(4, light.Intensity);
                Assert.AreEqual(35, light.SpotAngle);
                Assert.IsFalse(light.Enabled);
                Assert.IsFalse(light.Shadows);
                Assert.AreEqual(10, light.Placement.Position[0]);
                state.Data["lightEntityState"] = null;
                light = MaquetteLightAdapter.Read(state, 10);
                Assert.AreEqual(100, light.Range);
                Assert.IsTrue(light.Enabled);
                Assert.IsTrue(light.Shadows);
            }
        }

        [Test]
        public void ImagePlacementRetainsShearAndKeepsHeightInTheOrdinaryWidgetSize()
        {
            using (var document = new MaquetteDocument(m_Project))
            {
                var states = document.ReadStates(document.SelectScene());
                states[0].Data["cachedTransform"]["scale"] = JObject.Parse(@"{'x':2,'y':1,'z':1}");
                states[1].Data["cachedTransform"] = JObject.Parse(@"{'scale':{'x':1,'y':1,'z':1},
                    'rotation':{'x':0,'y':0,'z':0.38268343,'w':0.92387953}}");
                states[1].Data["materialId"] = "Additive";
                var primitive = MaquettePrimitiveData.Read(JObject.Parse(@"{'primitiveType':'ImageFile','primitivePath':'Images/test.png'}"), 21, null);
                var image = MaquetteImageAdapter.Read(states[1], primitive, 10);
                var m = image.GeometryTransform;
                Assert.AreEqual(Math.Sqrt(2.5) * 10, image.Height, 1e-5);
                Assert.AreEqual(1, Math.Sqrt(m[4] * m[4] + m[5] * m[5] + m[6] * m[6]), 1e-6);
                Assert.AreEqual(-.6, m[0] * m[4] + m[1] * m[5] + m[2] * m[6], 1e-6);
                Assert.AreEqual("Additive", image.MaterialId);
                primitive = MaquettePrimitiveData.Read(JObject.Parse(@"{'primitiveType':'ImageFile'}"), 19, null);
                Assert.AreEqual("TranslucentUnlit", MaquetteImageAdapter.Read(states[1], primitive, 10).MaterialId);
            }
        }

        [Test]
        public void NativeTextUsesTheSelectedStateAndTheShippedBoxOriginAndAutosizingScale()
        {
            const string id = "229a97df-4436-410e-8a32-b6386938d06a";
            Write("textindex.json", new JObject { ["textEntities"] = new JArray(new JObject
            {
                ["guid"] = id, ["FontSize"] = 4f, ["FontStyle"] = 0, ["TextAlignmentOption"] = 257
            }) });
            var sceneData = JObject.Parse(File.ReadAllText(Path.Combine(m_Project, "scenes.json")));
            sceneData["slides"][1]["treeRoots"][0]["children"][0]["targetEntityGuid"] = id;
            Write("scenes.json", sceneData);
            using (var document = new MaquetteDocument(m_Project))
            {
                var states = document.ReadStates(document.SelectScene());
                states[0].Data["cachedTransform"]["scale"] = JObject.Parse(@"{'x':2,'y':3,'z':1}");
                states[1].Data["cachedTransform"]["scale"] = JObject.Parse(@"{'x':1,'y':1,'z':1}");
                states[1].Data["textEntityState"] = JObject.Parse(@"{'Text':'Selected text','FontSize':48.0,
                    'FontStyle':3,'TextAlignmentOption':258,'IsFontAutoSize':false}");
                var text = MaquetteTextAdapter.Read(states[1], _ => throw new Exception("Unexpected text file read"), 10);
                Assert.AreEqual("Selected text", text.Text);
                Assert.AreEqual(48, text.FontSize);
                Assert.AreEqual(3, text.FontStyle);
                Assert.AreEqual(258, text.Alignment);
                Assert.AreEqual(new[] { 20000f, 30000f }, text.BoxSize);
                Assert.AreEqual(.001, text.GlyphTransform[0], 1e-8);
                Assert.AreEqual(.001, text.GlyphTransform[5], 1e-8);
                Assert.AreEqual(10, text.GlyphTransform[12], 1e-6);
                Assert.AreEqual(-15, text.GlyphTransform[13], 1e-6);
                Assert.AreEqual(-.01, text.GlyphTransform[14], 1e-6);
                states[1].Data.Remove("textEntityState");
                text = MaquetteTextAdapter.Read(states[1], guid => guid == id ? "File text" : throw new Exception("Wrong text GUID"), 10);
                Assert.AreEqual("File text", text.Text);
                Assert.AreEqual(new[] { 200000f, 300000f }, text.BoxSize);
            }
        }

        [Test]
        public void GeometryPlacementRetainsHierarchyShearWithRigidWidgetPlacement()
        {
            using (var document = new MaquetteDocument(m_Project))
            {
                var states = document.ReadStates(document.SelectScene());
                states[0].Data["cachedTransform"] = JObject.Parse(@"{'position':{'x':10,'y':20,'z':30},
                    'scale':{'x':2,'y':1,'z':1},'rotation':{'x':0,'y':0,'z':0.70710678,'w':0.70710678}}");
                states[1].Data["cachedTransform"] = JObject.Parse(@"{'position':{'x':1,'y':0,'z':0},
                    'rotation':{'x':0,'y':0,'z':0.38268343,'w':0.92387953}}");
                var placement = new MaquettePlacement(states[1]);
                Assert.AreEqual(new[] { 10f, 22f, 30f }, placement.Position);
                var sourceRecipe = GeometryRecipeData.Parse(@"{'Version':1,'Generator':{'Type':'rectangle','Parameters':{}}}");
                var recipe = placement.BakeLinearTransform(sourceRecipe);
                recipe.GetSteps(out _, out var operations);
                System.Numerics.Vector3 Transform(System.Numerics.Vector3 point)
                {
                    foreach (var step in operations)
                    {
                        var scale = (float[])step.Parameters["scale"];
                        var rotation = (float[])step.Parameters["rotation"];
                        point *= new System.Numerics.Vector3(scale[0], scale[1], scale[2]);
                        point = System.Numerics.Vector3.Transform(point, new System.Numerics.Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]));
                    }
                    var r = placement.Rotation;
                    point = System.Numerics.Vector3.Transform(point, new System.Numerics.Quaternion(r[0], r[1], r[2], r[3]));
                    return point + new System.Numerics.Vector3(placement.Position[0], placement.Position[1], placement.Position[2]);
                }
                var x = Transform(System.Numerics.Vector3.UnitX);
                var y = Transform(System.Numerics.Vector3.UnitY);
                Assert.AreEqual(10 - Math.Sqrt(.5), x.X, 1e-5);
                Assert.AreEqual(22 + Math.Sqrt(2), x.Y, 1e-5);
                Assert.AreEqual(10 - Math.Sqrt(.5), y.X, 1e-5);
                Assert.AreEqual(22 - Math.Sqrt(2), y.Y, 1e-5);
                Assert.AreEqual(-1.5, System.Numerics.Vector3.Dot(x - Transform(System.Numerics.Vector3.Zero), y - Transform(System.Numerics.Vector3.Zero)), 1e-5);
                Assert.AreEqual(30, x.Z, 1e-5);
                Assert.IsNull(sourceRecipe.ToJson()["Operations"]);
            }
        }

        [Test]
        public void ResolvesSelectedSceneAndPreservesHierarchyAndUnknownValues()
        {
            using (var document = new MaquetteDocument(m_Project))
            {
                Assert.AreEqual(20, document.Version);
                Assert.AreEqual("second", document.SelectScene().Id);
                var states = document.ReadStates(document.SelectScene());
                Assert.AreEqual(2, states.Count);
                Assert.IsNull(states[0].Entity); // Implicit groups need not have a named-group definition.
                Assert.AreSame(states[0], states[1].Parent);
                Assert.IsFalse(states[1].Visible);
                Assert.AreEqual("box", states[1].Entity.Id);
                Assert.AreEqual(42, (int)states[1].Entity.Data["future"]);
                Assert.AreEqual(-1, (int)states[1].Data["cachedTransform"]["scale"]["z"]);
                Assert.Throws<InvalidDataException>(() => document.SelectScene("absent"));
            }
        }

        [Test]
        public void DirectoryAndRootLayoutArchiveReadTheSameData()
        {
            string archive = Path.Combine(m_Root, "project.mqp");
            ZipFile.CreateFromDirectory(m_Project, archive);
            using (var directory = new MaquetteDocument(m_Project))
            using (var zipped = new MaquetteDocument(archive))
            {
                CollectionAssert.AreEquivalent(directory.Source.Files, zipped.Source.Files);
                Assert.IsTrue(JToken.DeepEquals(directory.SelectScene().Data, zipped.SelectScene().Data));
                Assert.AreEqual(2, zipped.ReadStates(zipped.SelectScene()).Count);
            }
        }

        [Test]
        public void RejectsDuplicateEntityIdentities()
        {
            Write("brushes.json", new JObject { ["strokes"] = new JArray(new JObject { ["guid"] = "box" }) });
            Assert.Throws<InvalidDataException>(() => { using (new MaquetteDocument(m_Project)) { } });
        }

        [Test]
        public void BuilderScaleMultipliesComponentsEvenWhenTheParentIsRotated()
        {
            using (var document = new MaquetteDocument(m_Project))
            {
                var states = document.ReadStates(document.SelectScene());
                states[0].Data["cachedTransform"]["scale"] = JObject.Parse("{\"x\":2,\"y\":3,\"z\":4}");
                states[0].Data["cachedTransform"]["rotation"] = JObject.Parse("{\"x\":0,\"y\":0,\"z\":0.70710678,\"w\":0.70710678}");
                CollectionAssert.AreEqual(new float[] { 4, 9, -4 }, states[1].GetBuilderScale());
            }
        }

        [Test]
        public void RejectsDuplicateJsonMembers()
        {
            File.WriteAllText(Path.Combine(m_Project, "manifest.json.mq"), "{\"guid\":\"a\",\"guid\":\"b\"}");
            Assert.Throws<JsonReaderException>(() => { using (new MaquetteDocument(m_Project)) { } });
        }

        [Test]
        public void RejectsResourceTraversal()
        {
            using (var source = new NativeProjectSource(m_Project))
            {
                Assert.Throws<InvalidDataException>(() => source.OpenRead("../outside"));
                Assert.Throws<InvalidDataException>(() => source.OpenRead("C:/outside"));
            }
            string archive = Path.Combine(m_Root, "unsafe.mqp");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) { zip.CreateEntry("../outside"); }
            Assert.Throws<InvalidDataException>(() => { using (new NativeProjectSource(archive)) { } });
        }

        [Test]
        public void RejectsAmbiguousArchiveResourceNames()
        {
            string archive = Path.Combine(m_Root, "ambiguous.mqp");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                zip.CreateEntry("Objects/model.glb");
                zip.CreateEntry("objects/model.glb");
            }
            Assert.Throws<InvalidDataException>(() => { using (new NativeProjectSource(archive)) { } });
        }
    }
}
