// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiltBrush.Tests
{
    internal class TestAnimationCanvasLifecycle
    {
        private const string kLogPrefix = "[OB_ANIM_P3_INTEGRATION]";

        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60f;
            while ((App.Scene == null || App.Scene.animationUI_manager == null ||
                    WidgetManager.m_Instance == null) &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsNotNull(App.Scene, "Open Brush scene did not initialize");
            Assert.IsNotNull(App.Scene.animationUI_manager,
                "Animation manager did not initialize");
            Assert.IsNotNull(WidgetManager.m_Instance,
                "Widget manager did not initialize");
        }

        [UnityTearDown]
        public IEnumerator ExitRuntime()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EmptyCanvasPromotionUndoAndSaveLeasePreserveCanvasLifetime()
        {
            Debug.Log($"{kLogPrefix} test=canvasLifetime state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>> { new List<int> { 1, 2 } },
                new List<bool> { true });

            Assert.AreEqual(3, manager.GetTimelineLength());
            CanvasScript frameZeroEmpty = manager.GetTimelineCanvas(0, 0);
            CanvasScript authoringCanvas = manager.GetTimelineCanvas(0, 1);
            Assert.AreSame(frameZeroEmpty, authoringCanvas,
                "One track should share one transient empty authoring Canvas");
            Assert.AreEqual(1, manager.Timeline[0].Frames
                .Select(frame => frame.Canvas).Distinct().Count());

            manager.SelectTimelineFrame(0, 1);
            var widgetObject = new GameObject("Animation lifecycle test widget");
            widgetObject.transform.SetParent(authoringCanvas.transform, false);
            GrabWidget widget = widgetObject.AddComponent<GrabWidget>();
            manager.NotifyDrawingContentChanged(authoringCanvas);

            Assert.IsTrue(manager.GetFrameFilled(0, 1));
            Assert.AreSame(authoringCanvas, manager.GetTimelineCanvas(0, 1));
            Assert.AreSame(authoringCanvas, manager.GetTimelineCanvas(0, 2));
            Assert.AreNotSame(authoringCanvas, manager.GetTimelineCanvas(0, 0));

            AnimationUI_Manager.DeleteFrameOperation undoOperation =
                manager.RemoveKeyFrame(0, 1);
            Assert.IsTrue(undoOperation.Succeeded);
            Assert.IsFalse(authoringCanvas == null,
                "Undo history must retain the displaced drawing Canvas");
            manager.UndoDeleteFrameOperation(undoOperation);
            Assert.AreSame(authoringCanvas, manager.GetTimelineCanvas(0, 1));
            Assert.IsTrue(manager.GetFrameFilled(0, 1));

            IDisposable saveLease = manager.RetainTimelineDrawingsForSave();
            AnimationUI_Manager.DeleteFrameOperation disposeOperation =
                manager.RemoveKeyFrame(0, 1);
            Assert.IsTrue(disposeOperation.Succeeded);
            manager.DiscardDeleteFrameOperationUndoState(disposeOperation);
            yield return null;
            Assert.IsFalse(authoringCanvas == null,
                "An in-progress save must retain a displaced drawing Canvas");

            WidgetManager.m_Instance.UnregisterGrabWidget(widgetObject);
            UnityEngine.Object.Destroy(widgetObject);
            saveLease.Dispose();
            yield return null;
            Assert.IsTrue(authoringCanvas == null,
                "The Canvas should be destroyed after its final owner releases it");
            Debug.Log($"{kLogPrefix} test=canvasLifetime state=passed");
        }
    }
}
