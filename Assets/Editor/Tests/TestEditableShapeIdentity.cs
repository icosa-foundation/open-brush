// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiltBrush
{
    public class TestEditableShapeIdentity
    {
        [UnityTearDown]
        public IEnumerator TearDown() { yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator EditingSharedShapesPreservesBindingsAcrossUndoRedoAndSketchMetadata()
        {
            yield return new EnterPlayMode();
#if UNITY_EDITOR_OSX && USD_SUPPORTED
            // The optional USD plug-in ships an Intel-only macOS binary. Main initializes
            // it on startup. Account for that one log only if initialization actually fails.
            if (!Unity.Formats.USD.InitUsd.Initialize())
            {
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex(@"^DllNotFoundException: .*UsdCs"));
            }
#endif
            SceneManager.LoadScene("Main");
            yield return null;
            float deadline = Time.realtimeSinceStartup + 60;
            while (App.CurrentState != App.AppState.Standard)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Open Brush did not finish starting");
                yield return null;
            }

            GeometryRecipeData Panel(int width) => GeometryRecipeData.Parse(
                "{'Version':1,'Generator':{'Type':'panel','Parameters':{'height':1,'depth':1,'width':" + width + "}}}");
            var original = new EditableModelDefinitionData(Panel(1));
            var definitions = new Dictionary<string, EditableModelDefinitionData>
                { { "shared", original }, { "independent", original } };
            TiltEditableModels Instances(string id, params int[] positions) => new TiltEditableModels
            {
                Location = Model.Location.Generated(id),
                RawTransforms = positions.Select(x => TrTransform.T(Vector3.right * x)).ToArray()
            };
            EditableModelManager.LoadFromTilt(definitions,
                new[] { Instances("shared", 1, 2), Instances("independent", 3) });
            var widgets = WidgetManager.m_Instance.EditableModelWidgets.OrderBy(w => w.LocalTransform.translation.x).ToArray();
            Assert.AreEqual(3, widgets.Length);
            var shared = widgets[0].Definition;
            Assert.AreSame(shared, widgets[1].Definition);

            var wide = Panel(2);
            var sharedEdit = new ModifyShapeCommand(widgets[0], wide, ShapeEditScope.Definition, null);
            sharedEdit.Redo();
            Assert.AreEqual("shared", widgets[1].Definition.Id);
            Assert.AreSame(wide, widgets[1].m_PolyRecipe);
            Assert.AreSame(original.Recipe, widgets[2].m_PolyRecipe);
            sharedEdit.Undo();
            Assert.AreSame(shared, widgets[1].Definition);
            sharedEdit.Redo();

            var gesture = new object();
            var edit = new ModifyShapeCommand(widgets[0], Panel(3), ShapeEditScope.Instance, gesture);
            edit.Redo();
            string forkId = widgets[0].Definition.Id;
            Assert.AreNotEqual("shared", forkId);
            var next = new ModifyShapeCommand(widgets[0], Panel(4), ShapeEditScope.Instance, gesture);
            Assert.IsTrue(edit.Merge(next));
            next.Redo();
            edit.Undo();
            Assert.AreSame(widgets[1].Definition, widgets[0].Definition);
            edit.Redo();
            Assert.AreEqual(forkId, widgets[0].Definition.Id);

            var metadata = new SketchMetadata
            {
                EditableModelDefinitions = MetadataUtils.GetEditableModelDefinitions(),
                EditableModelIndex = MetadataUtils.GetTiltEditableModels(new GroupIdMapping())
            };
            var settings = new JsonSerializerSettings { ContractResolver = new CustomJsonContractResolver() };
            using var text = new StringWriter();
            using (var writer = new CustomJsonWriter(text)) { JsonSerializer.Create(settings).Serialize(writer, metadata); }
            var restored = JsonConvert.DeserializeObject<SketchMetadata>(text.ToString(), settings);
            foreach (var widget in widgets) { widget.gameObject.SetActive(false); }
            EditableModelManager.LoadFromTilt(restored.EditableModelDefinitions, restored.EditableModelIndex);
            var loaded = WidgetManager.m_Instance.EditableModelWidgets.Where(w => w.gameObject.activeSelf)
                .OrderBy(w => w.LocalTransform.translation.x).ToArray();
            CollectionAssert.AreEqual(new[] { forkId, "shared", "independent" }, loaded.Select(w => w.Definition.Id));
            CollectionAssert.AreEqual(new[] { 4f, 2f, 1f }, loaded.Select(w => w.Model.GeneratedMesh.bounds.size.x / App.METERS_TO_UNITS));
        }
    }
}
