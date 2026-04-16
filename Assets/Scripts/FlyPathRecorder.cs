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
    /// <summary>
    /// Records camera transforms frame-by-frame during flight for later conversion to camera paths
    /// </summary>
    public class FlyPathRecorder : MonoBehaviour
    {
        [System.Serializable]
        public class RecordedFrame
        {
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
            public float speed; // Movement speed at this frame
            
            public RecordedFrame(Vector3 pos, Quaternion rot, float time, float moveSpeed)
            {
                position = pos;
                rotation = rot;
                timestamp = time;
                speed = moveSpeed;
            }
        }

        private static FlyPathRecorder m_Instance;
        public static FlyPathRecorder Instance => m_Instance;

        [Header("Recording Settings")]
        [SerializeField] private float m_MinRecordingInterval = 0.1f; // Minimum time between recorded frames
        [SerializeField] private float m_MinMovementThreshold = 0.01f; // Minimum movement to record a frame
        [SerializeField] private int m_MaxFrames = 1000; // Maximum number of frames to prevent memory issues

        private List<RecordedFrame> m_RecordedFrames;
        private bool m_IsRecording = false;
        private float m_LastRecordTime = 0f;
        private Vector3 m_LastRecordedPosition;
        private Camera m_VrCamera;
        private Vector3 m_LastFramePosition;
        
        public bool IsRecording => m_IsRecording;
        public int RecordedFrameCount => m_RecordedFrames?.Count ?? 0;
        public List<RecordedFrame> RecordedFrames => m_RecordedFrames;

        void Awake()
        {
            m_Instance = this;
            m_RecordedFrames = new List<RecordedFrame>();
        }

        void Start()
        {
            m_VrCamera = App.VrSdk.GetVrCamera();
        }

        void Update()
        {
            if (m_IsRecording && m_VrCamera != null)
            {
                RecordCurrentFrame();
            }
        }

        /// <summary>
        /// Start recording camera path frames
        /// </summary>
        public bool StartRecording()
        {
            if (m_IsRecording)
            {
                Debug.LogWarning("FlyPathRecorder: Already recording");
                return false;
            }

            if (m_VrCamera == null)
            {
                m_VrCamera = App.VrSdk.GetVrCamera();
                if (m_VrCamera == null)
                {
                    Debug.LogError("FlyPathRecorder: Could not find VR camera");
                    return false;
                }
            }

            m_RecordedFrames.Clear();
            m_IsRecording = true;
            m_LastRecordTime = Time.time;
            m_LastRecordedPosition = GetCameraWorldPosition();
            m_LastFramePosition = m_LastRecordedPosition;

            Debug.Log("FlyPathRecorder: Started recording camera path");
            
            // Record initial frame
            RecordCurrentFrame();
            
            return true;
        }

        /// <summary>
        /// Stop recording and return recorded frames
        /// </summary>
        public List<RecordedFrame> StopRecording()
        {
            if (!m_IsRecording)
            {
                Debug.LogWarning("FlyPathRecorder: Not currently recording");
                return null;
            }

            m_IsRecording = false;
            
            // Record final frame
            RecordCurrentFrame();
            
            Debug.Log($"FlyPathRecorder: Stopped recording. Captured {m_RecordedFrames.Count} frames over {GetRecordingDuration():F2} seconds");
            
            return new List<RecordedFrame>(m_RecordedFrames);
        }

        /// <summary>
        /// Clear all recorded frames
        /// </summary>
        public void ClearRecording()
        {
            m_RecordedFrames.Clear();
            Debug.Log("FlyPathRecorder: Cleared recorded frames");
        }

        /// <summary>
        /// Get the total duration of the current recording
        /// </summary>
        public float GetRecordingDuration()
        {
            if (m_RecordedFrames.Count < 2) return 0f;
            return m_RecordedFrames[m_RecordedFrames.Count - 1].timestamp - m_RecordedFrames[0].timestamp;
        }

        private void RecordCurrentFrame()
        {
            if (m_RecordedFrames.Count >= m_MaxFrames)
            {
                Debug.LogWarning($"FlyPathRecorder: Maximum frame count ({m_MaxFrames}) reached, stopping recording");
                StopRecording();
                return;
            }

            float currentTime = Time.time;
            Vector3 currentPosition = GetCameraWorldPosition();
            
            // Check minimum time interval
            if (currentTime - m_LastRecordTime < m_MinRecordingInterval)
            {
                m_LastFramePosition = currentPosition;
                return;
            }
            
            // Check minimum movement threshold (except for first and forced frames)
            float distanceMoved = Vector3.Distance(currentPosition, m_LastRecordedPosition);
            if (m_RecordedFrames.Count > 0 && distanceMoved < m_MinMovementThreshold)
            {
                m_LastFramePosition = currentPosition;
                return;
            }

            // Calculate movement speed
            float deltaTime = currentTime - m_LastRecordTime;
            float speed = deltaTime > 0f ? Vector3.Distance(currentPosition, m_LastFramePosition) / deltaTime : 0f;

            // Record the frame
            Quaternion currentRotation = GetCameraWorldRotation();
            RecordedFrame frame = new RecordedFrame(currentPosition, currentRotation, currentTime, speed);
            m_RecordedFrames.Add(frame);

            m_LastRecordTime = currentTime;
            m_LastRecordedPosition = currentPosition;
            m_LastFramePosition = currentPosition;
        }

        private Vector3 GetCameraWorldPosition()
        {
            if (m_VrCamera == null) return Vector3.zero;
            
            // Convert from camera space to scene space
            TrTransform scenePose = App.Scene.Pose;
            Vector3 cameraPos_RS = m_VrCamera.transform.position;
            return scenePose.MultiplyPoint(cameraPos_RS);
        }

        private Quaternion GetCameraWorldRotation()
        {
            if (m_VrCamera == null) return Quaternion.identity;
            
            // Convert from camera space to scene space
            TrTransform scenePose = App.Scene.Pose;
            Quaternion cameraRot_RS = m_VrCamera.transform.rotation;
            return scenePose.rotation * cameraRot_RS;
        }

        /// <summary>
        /// Get recording statistics for debugging
        /// </summary>
        public string GetRecordingStats()
        {
            if (m_RecordedFrames.Count == 0) return "No frames recorded";
            
            float duration = GetRecordingDuration();
            float avgSpeed = 0f;
            float totalDistance = 0f;
            
            for (int i = 1; i < m_RecordedFrames.Count; i++)
            {
                float dist = Vector3.Distance(m_RecordedFrames[i].position, m_RecordedFrames[i-1].position);
                totalDistance += dist;
                avgSpeed += m_RecordedFrames[i].speed;
            }
            
            if (m_RecordedFrames.Count > 1)
            {
                avgSpeed /= (m_RecordedFrames.Count - 1);
            }
            
            return $"Frames: {m_RecordedFrames.Count}, Duration: {duration:F2}s, Distance: {totalDistance:F2}m, Avg Speed: {avgSpeed:F2}m/s";
        }
    }
}