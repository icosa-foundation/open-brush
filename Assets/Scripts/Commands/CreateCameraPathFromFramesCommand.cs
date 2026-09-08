// Copyright 2024 The Open Brush Authors
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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class CreateCameraPathFromFramesCommand : BaseCommand
    {
        private List<FlyPathRecorder.RecordedFrame> m_Frames;
        private CanvasScript m_Canvas;
        private CameraPathWidget m_Widget;
        private int m_TiltMeterCost;

        public CameraPathWidget Widget => m_Widget;

        public CreateCameraPathFromFramesCommand(List<FlyPathRecorder.RecordedFrame> frames)
        {
            m_Frames = new List<FlyPathRecorder.RecordedFrame>(frames);
            m_Canvas = App.ActiveCanvas;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnUndo()
        {
            m_Widget.Hide();
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: false);
            WidgetManager.m_Instance.ValidateCurrentCameraPath();
            App.Switchboard.TriggerCameraPathDeleted();
        }

        protected override void OnRedo()
        {
            if (m_Widget != null)
            {
                m_Widget.gameObject.SetActive(true);
                m_Widget.RestoreFromToss();
            }
            else
            {
                m_Widget = Object.Instantiate(WidgetManager.m_Instance.CameraPathWidgetPrefab);
                m_Widget.transform.parent = m_Canvas.transform;
                m_Widget.transform.localPosition = Vector3.zero;
                m_Widget.transform.localRotation = Quaternion.identity;
                m_Widget.transform.localScale = Vector3.one;
                m_Widget.Show(true);
                App.Switchboard.TriggerCameraPathCreated();
                WidgetManager.m_Instance.CameraPathsVisible = true;

                BuildPath();

                TrTransform xfSpawn = TrTransform.identity;
                m_Widget.InitIntroAnim(
                    xfSpawn, xfSpawn, false, Quaternion.identity, forceTransform: true);
                m_TiltMeterCost = m_Widget.GetTiltMeterCost();
            }

            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: true);
        }

        protected override void OnDispose()
        {
            if (m_Widget != null && m_Widget.gameObject)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(m_Widget.gameObject);
                Object.Destroy(m_Widget.gameObject);
            }
        }

        private void BuildPath()
        {
            CreatePositionKnots();
            CreateRotationKnots();
            CreateSpeedKnots();
            m_Widget.Path.RefreshEntirePath();
        }

        private void CreatePositionKnots()
        {
            // Frames are recorded in canvas space; CreatePositionKnot places knots in
            // world/room space, so convert through the canvas pose (as the Lua API does).
            TrTransform canvasPose = m_Canvas.Pose;
            for (int i = 0; i < m_Frames.Count; i++)
            {
                FlyPathRecorder.RecordedFrame frame = m_Frames[i];
                CameraPathPositionKnot posKnot =
                    m_Widget.Path.CreatePositionKnot(canvasPose.MultiplyPoint(frame.position));
                posKnot.transform.rotation =
                    Quaternion.LookRotation(canvasPose.rotation * GetDirectionToNext(i), Vector3.up);

                if (i < m_Frames.Count - 1)
                {
                    // TangentMagnitude is stored in canvas-space units, so use the
                    // untransformed frame positions here.
                    float distance = Vector3.Distance(frame.position, m_Frames[i + 1].position);
                    posKnot.TangentMagnitude = distance * 0.3f;
                }
                else
                {
                    posKnot.TangentMagnitude = 1.0f;
                }

                m_Widget.Path.InsertPositionKnot(posKnot, m_Widget.Path.PositionKnots.Count);
            }
        }

        private void CreateRotationKnots()
        {
            int rotationKnotInterval = Mathf.Max(1, m_Frames.Count / 10);
            Quaternion canvasRotation = m_Canvas.Pose.rotation;

            for (int i = 0; i < m_Frames.Count; i += rotationKnotInterval)
            {
                FlyPathRecorder.RecordedFrame frame = m_Frames[i];
                PathT pathT = new PathT(i);
                CameraPathRotationKnot rotKnot =
                    m_Widget.Path.CreateRotationKnot(pathT, canvasRotation * frame.rotation);
                m_Widget.Path.AddRotationKnot(rotKnot, pathT);
            }
        }

        private void CreateSpeedKnots()
        {
            int speedKnotInterval = Mathf.Max(1, m_Frames.Count / 8);

            for (int i = 0; i < m_Frames.Count; i += speedKnotInterval)
            {
                FlyPathRecorder.RecordedFrame frame = m_Frames[i];
                PathT pathT = new PathT(i);
                CameraPathSpeedKnot speedKnot = m_Widget.Path.CreateSpeedKnot(pathT);
                speedKnot.SetCameraSpeed(frame.speed * 0.5f);
                m_Widget.Path.AddSpeedKnot(speedKnot, pathT);
            }
        }

        private Vector3 GetDirectionToNext(int currentIndex)
        {
            const float kMinDirectionLengthSq = 0.000001f;
            Vector3 direction;

            if (currentIndex >= m_Frames.Count - 1)
            {
                if (currentIndex > 0)
                {
                    direction = m_Frames[currentIndex].position -
                        m_Frames[currentIndex - 1].position;
                    return direction.sqrMagnitude > kMinDirectionLengthSq
                        ? direction.normalized
                        : m_Frames[currentIndex].rotation * Vector3.forward;
                }

                return m_Frames[currentIndex].rotation * Vector3.forward;
            }

            direction = m_Frames[currentIndex + 1].position - m_Frames[currentIndex].position;
            return direction.sqrMagnitude > kMinDirectionLengthSq
                ? direction.normalized
                : m_Frames[currentIndex].rotation * Vector3.forward;
        }
    }
}
