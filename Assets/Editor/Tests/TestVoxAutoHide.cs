// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TiltBrush
{
    public sealed class VoxAutoHideTestWidget : ModelWidget
    {
        protected override void Awake() { }
        protected override void OnHide() { }
        protected override void OnDestroy() { }
        public void MakeVisible() => m_CurrentState = State.Visible;
    }

    internal class TestVoxAutoHide
    {
        private static void SetMember(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var property = target.GetType().GetProperty(name, flags);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, flags).SetValue(target, value);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyDocument_CachesLiveCanvasPoseOnce(bool undoRecorded)
        {
            var previousApp = App.Instance;
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject Create(string name)
            {
                var go = new GameObject($"[VoxAutoHideTest] {name}");
                go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go, preview);
                return go;
            }
            try
            {
                var app = Create("App").AddComponent<App>();
                var scene = Create("Scene").AddComponent<SceneScript>();
                var canvas = Create("Canvas").AddComponent<CanvasScript>();
                canvas.transform.SetPositionAndRotation(new Vector3(10, 20, 30), Quaternion.Euler(0, 35, 0));
                canvas.transform.localScale = Vector3.one * 3;
                var accessor = typeof(CanvasScript).GetField("AsCanvas");
                SetMember(canvas, "AsCanvas", Activator.CreateInstance(accessor.FieldType, new object[] { canvas.transform }));
                SetMember(scene, "m_ActiveCanvas", canvas);
                SetMember(scene, "m_bInitialized", true);
                SetMember(app, "m_SceneScript", scene);
                App.Instance = app;

                var widget = Create("Widget").AddComponent<VoxAutoHideTestWidget>();
                widget.transform.SetParent(canvas.transform, false);
                widget.transform.localPosition = new Vector3(4, 5, 6);
                widget.transform.localRotation = Quaternion.Euler(15, 25, 35);
                widget.transform.localScale = Vector3.one * 2;
                widget.MakeVisible();
                var expected = canvas.AsCanvas[widget.transform];
                var document = new RuntimeVoxDocument();
                var model = document.CreateModel("empty", new Vector3Int(2, 2, 2));
                model.AddOrUpdateVoxel(Vector3Int.zero, 1);
                var wrapper = new VoxDocumentApiWrapper(document, widget);
                model.RemoveVoxel(Vector3Int.zero);
                wrapper.OnDocumentMutated(undoRecorded);
                Assert.AreEqual(expected, document.ViewState.WidgetTransform);
                Assert.IsFalse(widget.Showing);

                widget.transform.localPosition = Vector3.zero;
                widget.transform.localRotation = Quaternion.identity;
                widget.transform.localScale = Vector3.one * .01f;
                wrapper.OnDocumentMutated(undoRecorded);
                Assert.AreEqual(expected, document.ViewState.WidgetTransform);
            }
            finally
            {
                App.Instance = previousApp;
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
