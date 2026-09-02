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
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiltBrush
{
    public class TestHttpApiCommandsPlayMode
    {
        private const string kSceneName = "Main";
        private const string kApiBaseUrl = "http://localhost:40074";
        private const float kEpsilon = 0.01f;
        private static bool s_Ready;

        private static Type GetTypeOrFail(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            Assert.NotNull(type, $"Type not found: {typeName}");
            return type;
        }

        private static object GetStaticProperty(Type type, string name)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(prop, $"Static property not found: {type.FullName}.{name}");
            return prop.GetValue(null);
        }

        private static object GetStaticField(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, $"Static field not found: {type.FullName}.{name}");
            return field.GetValue(null);
        }

        private static object GetInstanceProperty(object instance, string name)
        {
            var prop = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(prop, $"Property not found: {instance.GetType().FullName}.{name}");
            return prop.GetValue(instance);
        }

        private static object GetInstanceField(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"Field not found: {instance.GetType().FullName}.{name}");
            return field.GetValue(instance);
        }

        private static T GetStructField<T>(object boxedStruct, string name)
        {
            var field = boxedStruct.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"Struct field not found: {boxedStruct.GetType().FullName}.{name}");
            return (T)field.GetValue(boxedStruct);
        }

        private static bool IsVr()
        {
            var appType = GetTypeOrFail("TiltBrush.App");
            var vrSdk = GetStaticProperty(appType, "VrSdk");
            if (vrSdk == null)
            {
                return false;
            }
            var method = vrSdk.GetType().GetMethod("GetHmdDof", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                return false;
            }
            var dof = method.Invoke(vrSdk, null);
            return !string.Equals(dof?.ToString(), "None", StringComparison.OrdinalIgnoreCase);
        }

        private static Vector3 GetBrushPosition()
        {
            var apiType = GetTypeOrFail("TiltBrush.ApiManager");
            var api = GetStaticProperty(apiType, "Instance");
            return (Vector3)GetInstanceField(api, "BrushPosition");
        }

        private static Quaternion GetBrushRotation()
        {
            var apiType = GetTypeOrFail("TiltBrush.ApiManager");
            var api = GetStaticProperty(apiType, "Instance");
            return (Quaternion)GetInstanceField(api, "BrushRotation");
        }

        private static int GetStrokeCount()
        {
            var sketchType = GetTypeOrFail("TiltBrush.SketchMemoryScript");
            var sketch = GetStaticField(sketchType, "m_Instance");
            return (int)GetInstanceProperty(sketch, "StrokeCount");
        }

        private static object GetSceneScript()
        {
            var appType = GetTypeOrFail("TiltBrush.App");
            return GetStaticProperty(appType, "Scene");
        }

        private static object GetScenePose()
        {
            return GetInstanceProperty(GetSceneScript(), "Pose");
        }

        private static Vector3 GetSceneTranslation()
        {
            return GetStructField<Vector3>(GetScenePose(), "translation");
        }

        private static Quaternion GetSceneRotation()
        {
            return GetStructField<Quaternion>(GetScenePose(), "rotation");
        }

        private static float GetSceneScale()
        {
            return GetStructField<float>(GetScenePose(), "scale");
        }

        private static Vector3 GetPointerPosition()
        {
            var pointerType = GetTypeOrFail("TiltBrush.PointerManager");
            var pointerManager = GetStaticField(pointerType, "m_Instance");
            var mainPointer = GetInstanceProperty(pointerManager, "MainPointer");
            var component = mainPointer as Component;
            Assert.NotNull(component, "MainPointer is not a Component");
            return component.transform.position;
        }

        private static GameObject GetDropCam()
        {
            var sketchControls = GameObject.Find("SketchControls");
            Assert.NotNull(sketchControls, "SketchControls not found");
            var dropCamTransform = sketchControls.transform.Find("DropCam");
            Assert.NotNull(dropCamTransform, "DropCam not found under SketchControls");
            return dropCamTransform.gameObject;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForServerReady(float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            string url = $"{kApiBaseUrl}/help/commands?raw";
            while (true)
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success &&
                        !string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        yield break;
                    }
                }

                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                {
                    Assert.Fail("Timed out waiting for HTTP API server. Ensure the Main scene is running.");
                }
                yield return null;
            }
        }

        private static IEnumerator WaitForRuntimeReady(float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                var apiType = GetTypeOrFail("TiltBrush.ApiManager");
                var sketchType = GetTypeOrFail("TiltBrush.SketchMemoryScript");
                var controlsType = GetTypeOrFail("TiltBrush.SketchControlsScript");
                var appType = GetTypeOrFail("TiltBrush.App");
                var pointerType = GetTypeOrFail("TiltBrush.PointerManager");
                var widgetType = GetTypeOrFail("TiltBrush.WidgetManager");

                var api = GetStaticProperty(apiType, "Instance");
                var sketch = GetStaticField(sketchType, "m_Instance");
                var controls = GetStaticField(controlsType, "m_Instance");
                var scene = GetStaticProperty(appType, "Scene");
                var pointer = GetStaticField(pointerType, "m_Instance");
                var widget = GetStaticField(widgetType, "m_Instance");

                if (api != null &&
                    sketch != null &&
                    controls != null &&
                    scene != null &&
                    pointer != null &&
                    widget != null)
                {
                    yield break;
                }

                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                {
                    Assert.Fail("Timed out waiting for runtime singletons. Ensure the Main scene is running.");
                }
                yield return null;
            }
        }

        private static IEnumerator WaitForApiQueueEmpty(float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            var apiType = GetTypeOrFail("TiltBrush.ApiManager");
            var queueField = apiType.GetField("m_RequestedCommandQueue",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(queueField, "ApiManager queue field not found");
            while (true)
            {
                var api = GetStaticProperty(apiType, "Instance");
                var queue = (Queue)queueField.GetValue(api);
                if (queue.Count == 0)
                {
                    yield break;
                }
                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                {
                    Assert.Fail("Timed out waiting for API command queue to drain.");
                }
                yield return null;
            }
        }

        private static IEnumerator SendCommand(string command, string parameters = null)
        {
            string url = $"{kApiBaseUrl}/api/v1?{command}";
            if (!string.IsNullOrEmpty(parameters))
            {
                url += "=" + UnityWebRequest.EscapeURL(parameters);
            }

            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                Assert.AreEqual(UnityWebRequest.Result.Success, request.result,
                    $"HTTP command failed: {command} ({request.error})");
            }
            yield return WaitForApiQueueEmpty(2f);
        }

        private static IEnumerator EnsureReady()
        {
            if (s_Ready)
            {
                yield break;
            }

            LogAssert.ignoreFailingMessages = true;
            if (SceneManager.GetActiveScene().name != kSceneName)
            {
                SceneManager.LoadScene(kSceneName);
                yield return null;
            }

            yield return WaitForServerReady(30f);
            yield return WaitForRuntimeReady(30f);
            s_Ready = true;
        }

        private static void AssertVector3Approx(Vector3 expected, Vector3 actual, float eps = kEpsilon)
        {
            Assert.LessOrEqual(Vector3.Distance(expected, actual), eps,
                $"Expected {expected}, got {actual}");
        }

        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, float maxAngle = 0.5f)
        {
            float angle = Quaternion.Angle(expected, actual);
            Assert.LessOrEqual(angle, maxAngle, $"Expected rotation within {maxAngle} deg, got {angle} deg");
        }

        [UnityTest] public IEnumerator Cmd_DebugBrush() { yield return EnsureReady(); yield return SendCommand("debug.brush"); }
        [UnityTest] public IEnumerator Cmd_StrokesDebug() { yield return EnsureReady(); yield return SendCommand("strokes.debug"); }

        [UnityTest]
        public IEnumerator Cmd_BrushMoveTo()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.move.to", "1,2,3");
            AssertVector3Approx(new Vector3(1, 2, 3), GetBrushPosition());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushMoveBy()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.home.reset");
            var origin = GetBrushPosition();
            yield return SendCommand("brush.move.by", "1,0,0");
            AssertVector3Approx(origin + new Vector3(1, 0, 0), GetBrushPosition());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushMoveForward()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.home.reset");
            var origin = GetBrushPosition();
            var originRot = GetBrushRotation();
            yield return SendCommand("brush.move", "1");
            AssertVector3Approx(origin + (originRot * Vector3.forward), GetBrushPosition());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushHomeSetReset()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.move.to", "2,2,2");
            yield return SendCommand("brush.home.set");
            yield return SendCommand("brush.move.by", "1,0,0");
            yield return SendCommand("brush.home.reset");
            AssertVector3Approx(new Vector3(2, 2, 2), GetBrushPosition());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushTransformStack()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.transform.push");
            var pushed = GetBrushPosition();
            yield return SendCommand("brush.move.by", "1,0,0");
            yield return SendCommand("brush.transform.pop");
            AssertVector3Approx(pushed, GetBrushPosition());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushTurnY()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.home.reset");
            var originRot = GetBrushRotation();
            yield return SendCommand("brush.turn.y", "45");
            AssertQuaternionApprox(originRot * Quaternion.AngleAxis(45, Vector3.up), GetBrushRotation());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushTurnX()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.home.reset");
            var originRot = GetBrushRotation();
            yield return SendCommand("brush.turn.x", "30");
            AssertQuaternionApprox(originRot * Quaternion.AngleAxis(30, Vector3.left), GetBrushRotation());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushTurnZ()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.home.reset");
            var originRot = GetBrushRotation();
            yield return SendCommand("brush.turn.z", "15");
            AssertQuaternionApprox(originRot * Quaternion.AngleAxis(15, Vector3.forward), GetBrushRotation());
        }

        [UnityTest]
        public IEnumerator Cmd_BrushLookDirections()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.look.forwards");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.forward), 0.99f);
            yield return SendCommand("brush.look.up");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.up), 0.99f);
            yield return SendCommand("brush.look.down");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.down), 0.99f);
            yield return SendCommand("brush.look.left");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.left), 0.99f);
            yield return SendCommand("brush.look.right");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.right), 0.99f);
            yield return SendCommand("brush.look.backwards");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.back), 0.99f);
        }

        [UnityTest]
        public IEnumerator Cmd_BrushLookAt()
        {
            yield return EnsureReady();
            yield return SendCommand("brush.move.to", "1,1,1");
            yield return SendCommand("brush.look.at", "2,1,1");
            Assert.Greater(Vector3.Dot(GetBrushRotation() * Vector3.forward, Vector3.right), 0.99f);
        }

        [UnityTest]
        public IEnumerator Cmd_BrushMoveToHand()
        {
            yield return EnsureReady();
            var pointerPos = GetPointerPosition();
            yield return SendCommand("brush.move.to.hand", "r");
            AssertVector3Approx(pointerPos, GetBrushPosition(), 0.1f);
        }

        [UnityTest]
        public IEnumerator Cmd_BrushDraw()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("brush.draw", "1");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_BrushNewStroke()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("brush.new.stroke");
            yield return SendCommand("brush.draw", "1");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawPath()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.path", "[0,0,0],[1,0,0],[1,1,0],[0,1,0]");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawStroke()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.stroke", "[0,0,0,0,180,90,.75],[1,0,0,0,180,90,.75],[1,1,0,0,180,90,.75],[0,1,0,0,180,90,.75]");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawPolygon()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.polygon", "5,2.5,45");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawText()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.text", "hello");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawPaths()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.paths", "[[0,0,0],[1,0,0],[1,1,0]],[[0,0,-1],[-1,0,-1],[-1,1,-1]]");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 2);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawSvgPath()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.svg.path", "M 0 0 L 10 0 L 10 10 Z");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_DrawSvg()
        {
            yield return EnsureReady();
            int strokeCount = GetStrokeCount();
            yield return SendCommand("draw.svg", "<svg xmlns='http://www.w3.org/2000/svg'><path d='M 0 0 L 10 0 L 10 10 Z'/></svg>");
            Assert.GreaterOrEqual(GetStrokeCount(), strokeCount + 1);
        }

        [UnityTest]
        public IEnumerator Cmd_UserMoveTo()
        {
            yield return EnsureReady();
            yield return SendCommand("user.move.to", "1,1,1");
            AssertVector3Approx(new Vector3(-1, -1, -1), GetSceneTranslation());
        }

        [UnityTest]
        public IEnumerator Cmd_UserMoveBy()
        {
            yield return EnsureReady();
            yield return SendCommand("user.move.to", "1,1,1");
            yield return SendCommand("user.move.by", "1,0,0");
            AssertVector3Approx(new Vector3(-2, -1, -1), GetSceneTranslation());
        }

        [UnityTest]
        public IEnumerator Cmd_UserTurnY()
        {
            yield return EnsureReady();
            yield return SendCommand("user.turn.y", "45");
            AssertQuaternionApprox(Quaternion.Euler(0, -45, 0), GetSceneRotation());
        }

        [UnityTest]
        public IEnumerator Cmd_UserDirection()
        {
            yield return EnsureReady();
            if (IsVr())
            {
                Assert.Ignore("user.direction is monoscopic-only");
            }
            yield return SendCommand("user.direction", "45,45,0");
            AssertQuaternionApprox(Quaternion.Euler(45, 45, 0), GetSceneRotation());
        }

        [UnityTest]
        public IEnumerator Cmd_UserLookAt()
        {
            yield return EnsureReady();
            yield return SendCommand("user.move.to", "1,1,1");
            yield return SendCommand("user.look.at", "2,1,1");
            var userPos = -GetSceneTranslation();
            var lookDir = (new Vector3(2, 1, 1) - userPos).normalized;
            if (IsVr())
            {
                var flat = new Vector3(lookDir.x, 0, lookDir.z).normalized;
                Assert.Greater(Vector3.Dot(GetSceneRotation() * Vector3.forward, flat), 0.99f);
            }
            else
            {
                Assert.Greater(Vector3.Dot(GetSceneRotation() * Vector3.forward, lookDir), 0.99f);
            }
        }

        [UnityTest]
        public IEnumerator Cmd_SceneScaleTo()
        {
            yield return EnsureReady();
            yield return SendCommand("scene.scale.to", "0.5");
            Assert.AreEqual(0.5f, GetSceneScale(), kEpsilon);
        }

        [UnityTest]
        public IEnumerator Cmd_SceneScaleBy()
        {
            yield return EnsureReady();
            yield return SendCommand("scene.scale.to", "0.5");
            yield return SendCommand("scene.scale.by", "2");
            Assert.AreEqual(1.0f, GetSceneScale(), kEpsilon);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorOn()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            Assert.IsTrue(GetDropCam().activeSelf);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorOff()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.off");
            Assert.IsFalse(GetDropCam().activeSelf);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorToggle()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.off");
            yield return SendCommand("spectator.toggle");
            Assert.IsTrue(GetDropCam().activeSelf);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorMoveTo()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.move.to", "5,5,5");
            AssertVector3Approx(new Vector3(5, 5, 5), GetDropCam().transform.position);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorMoveBy()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.move.to", "5,5,5");
            yield return SendCommand("spectator.move.by", "1,0,-2");
            AssertVector3Approx(new Vector3(6, 5, 3), GetDropCam().transform.position);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorDirection()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.direction", "45,45,0");
            AssertQuaternionApprox(Quaternion.Euler(45, 45, 0), GetDropCam().transform.rotation);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorTurnY()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.direction", "0,0,0");
            yield return SendCommand("spectator.turn.y", "30");
            AssertQuaternionApprox(Quaternion.AngleAxis(30, Vector3.up), GetDropCam().transform.rotation);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorTurnX()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.direction", "0,0,0");
            yield return SendCommand("spectator.turn.x", "15");
            AssertQuaternionApprox(Quaternion.AngleAxis(15, Vector3.left), GetDropCam().transform.rotation);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorTurnZ()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.direction", "0,0,0");
            yield return SendCommand("spectator.turn.z", "10");
            AssertQuaternionApprox(Quaternion.AngleAxis(10, Vector3.forward), GetDropCam().transform.rotation);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorLookAt()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.look.at", "0,0,0");
            yield return WaitFrames(2);
            var dropCam = GetDropCam();
            var expectedForward = (-dropCam.transform.position).normalized;
            Assert.Greater(Vector3.Dot(dropCam.transform.forward.normalized, expectedForward), 0.99f);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorModeStationary()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.mode", "stationary");
            var dropCam = GetDropCam();
            var stationaryPos = dropCam.transform.position;
            yield return WaitFrames(2);
            AssertVector3Approx(stationaryPos, dropCam.transform.position);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorModeWobble()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.mode", "wobble");
            var dropCam = GetDropCam();
            var wobblePos = dropCam.transform.position;
            yield return WaitFrames(2);
            Assert.Greater(Vector3.Distance(wobblePos, dropCam.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorModeCircular()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.mode", "circular");
            var dropCam = GetDropCam();
            var circularPos = dropCam.transform.position;
            yield return WaitFrames(2);
            Assert.Greater(Vector3.Distance(circularPos, dropCam.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorModeSlowFollow()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            yield return SendCommand("spectator.mode", "slowFollow");
            var dropCam = GetDropCam();
            var xrRig = GameObject.Find("Viewpoint/XRRig");
            Assert.NotNull(xrRig, "XRRig not found");
            xrRig.transform.position += new Vector3(1, 0, 0);
            yield return WaitFrames(2);
            Assert.Less(Vector3.Distance(dropCam.transform.position, xrRig.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHidePanels()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "panels");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("Panels")));
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHideWidgets()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "widgets");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("GrabWidgets")));
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHideStrokes()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "strokes");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("MainCanvas")));
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHideSelection()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "selection");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("SelectionCanvas")));
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHideHeadset()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "headset");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("HeadMesh")));
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHideUi()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "ui");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("UI")));
        }

        [UnityTest]
        public IEnumerator Cmd_SpectatorHideUserTools()
        {
            yield return EnsureReady();
            yield return SendCommand("spectator.on");
            var cam = GetDropCam().GetComponentInChildren<Camera>();
            Assert.NotNull(cam, "DropCam camera not found");
            yield return SendCommand("spectator.hide", "usertools");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("UserTools")));
        }
    }
}
