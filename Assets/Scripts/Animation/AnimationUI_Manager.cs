// Copyright 2023 The Open Brush Authors
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
using System;
using TMPro;
using System.Linq;
using UnityEditor;

namespace TiltBrush.FrameAnimation
{
    public class AnimationUI_Manager : MonoBehaviour
    {
        int m_Fps = 8;
        float m_FrameOn;
        long m_Start;
        long m_Current;
        long m_Time;
        float m_FrameOffset = 1.22f;
        int m_TrackScrollOffset;
        bool m_Playing;
        bool m_Scrolling;
        int m_PreviousShowingFrame = -1;
        int m_PreviousCanvasBatches;
        CanvasScript m_LastCanvas;
        GameObject m_AnimationPathCanvas;
        bool m_AnimationMode = true;
        float m_SliderFrameSize = 0.12f; // Visual size of frame on timeline
        float m_TimelineOffset;

        public List<Track> Timeline;
        public GameObject timelineRef;
        public GameObject timelineNotchPrefab;
        public GameObject timelineFramePrefab;
        public GameObject timelineField;
        public GameObject textRef;
        public GameObject deleteFrameButton;
        public GameObject frameButtonPrefab;
        public List<GameObject> timelineNotches;
        public List<GameObject> timelineFrameObjects;

        int FrameOn => Math.Clamp((int)m_FrameOn, 0, GetTimelineLength() - 1);

        public struct Frame
        {
            public bool Visible;
            public bool Deleted;
            public bool FrameExists;
            public CanvasScript Canvas;
            public CameraPathWidget AnimatedPath;
        }

        public struct DeletedFrame
        {
            public Frame Frame;
            public int Length;
            public (int, int) Location;
        }

        public struct Track
        {
            public List<Frame> Frames;
            public bool Visible;
            public bool Deleted;
        }

        public Frame NewFrame(CanvasScript canvas)
        {
            Frame thisframeLayer;
            thisframeLayer.Canvas = canvas;
            thisframeLayer.Visible = App.Scene.IsLayerVisible(canvas);
            thisframeLayer.Deleted = false;
            thisframeLayer.FrameExists = true;
            thisframeLayer.AnimatedPath = null;
            return thisframeLayer;
        }

        Track NewTrack()
        {
            Track thisFrame;
            thisFrame.Frames = new List<Frame>();
            thisFrame.Visible = true;
            thisFrame.Deleted = false;
            return thisFrame;
        }

        void Awake()
        {
            App.Scene.animationUI_manager = this;
        }

        public void StartTimeline()
        {
            Timeline = new List<Track>();
            Track mainTrack = NewTrack();
            Frame originFrame = NewFrame(App.Scene.m_MainCanvas);
            mainTrack.Frames.Add(originFrame);
            Timeline.Add(mainTrack);
            App.Scene.animationUI_manager = this;
            FocusFrame(0);
            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();
            ResetTimeline();

            if (m_AnimationPathCanvas == null)
            {
                m_AnimationPathCanvas = new GameObject("AnimationPaths");
                m_AnimationPathCanvas.transform.parent = App.Scene.gameObject.transform;
                m_AnimationPathCanvas.AddComponent<CanvasScript>();
            }
        }

        private void HideFrame(int hidingFrame, int frameOn)
        {
            foreach (Track track in Timeline)
            {
                if (hidingFrame >= track.Frames.Count) { continue; }
                if (frameOn < track.Frames.Count && track.Frames[hidingFrame].Canvas.Equals(track.Frames[frameOn].Canvas)) continue;

                Frame thisFrame = track.Frames[hidingFrame];

                App.Scene.HideCanvas(thisFrame.Canvas);
                thisFrame.Visible = false;
                track.Frames[hidingFrame] = thisFrame;
            }
        }

        private void ShowFrame(int frameIndex)
        {
            if (m_PreviousShowingFrame == frameIndex) return;

            for (int i = 0; i < Timeline.Count; i++)
            {
                if (frameIndex >= Timeline[i].Frames.Count) { continue; }
                Frame thisFrame = Timeline[i].Frames[frameIndex];
                if (Timeline[i].Visible && !thisFrame.Deleted)
                {
                    App.Scene.ShowCanvas(thisFrame.Canvas);
                }
                else
                {
                    App.Scene.HideCanvas(thisFrame.Canvas);
                    thisFrame.Visible = false;
                }
                Timeline[i].Frames[frameIndex] = thisFrame;
            }
            m_PreviousShowingFrame = frameIndex;
        }

        public bool GetFrameFilled(int track, int frame)
        {
            return Timeline[track].Frames[frame].Canvas.gameObject.transform.childCount > 0;
        }

        public bool GetFrameFilled(CanvasScript canvas)
        {
            (int, int) loc = GetCanvasLocation(canvas);
            return Timeline[loc.Item1].Frames[loc.Item2].Canvas.gameObject.transform.childCount > 0;
        }

        public void AddAnimationPath(CameraPathWidget pathwidget, int trackNum, int frameNum)
        {
            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(m_AnimationPathCanvas.transform);

            pathwidget.SetPathAnimation(true);

            (int, int) Loc = (trackNum, frameNum);
            pathwidget.Path.timelineLocation = Loc;

            CanvasScript origCanvas = Timeline[Loc.Item1].Frames[Loc.Item2].Canvas;

            if (Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath != null)
            {
                WidgetManager.m_Instance.DeleteCameraPath(Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath);
            }

            int i = 0;

            while (
                Loc.Item2 + i < Timeline[Loc.Item1].Frames.Count &&
                Timeline[Loc.Item1].Frames[Loc.Item2 + i].Canvas.Equals(origCanvas)
            ) { i++; }

            for (int c = 0; c < i; c++)
            {
                Frame changingFrame = Timeline[Loc.Item1].Frames[Loc.Item2 + c];
                changingFrame.AnimatedPath = pathwidget;
                Timeline[Loc.Item1].Frames[Loc.Item2 + c] = changingFrame;
            }
        }

        public void AddAnimationPath(CameraPathWidget pathwidget)
        {
            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(m_AnimationPathCanvas.transform);
            pathwidget.SetPathAnimation(true);
            (int, int) Loc = GetCanvasLocation(App.Scene.ActiveCanvas);
            pathwidget.Path.timelineLocation = Loc;

            if (!GetFrameFilled(Loc.Item1, Loc.Item2))
            {
                TiltBrush.WidgetManager.m_Instance.UnregisterGrabWidget(pathwidget.gameObject);
                Destroy(pathwidget);
                return;
            }

            CanvasScript origCanvas = Timeline[Loc.Item1].Frames[Loc.Item2].Canvas;

            if (Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath != null)
            {
                WidgetManager.m_Instance.DeleteCameraPath(Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath);
            }

            int i = 0;

            List<Frame> framesChanging = new List<Frame>();
            while (
                Loc.Item2 + i < Timeline[Loc.Item1].Frames.Count &&
                Timeline[Loc.Item1].Frames[Loc.Item2 + i].Canvas.Equals(origCanvas)
            ) { i++; }

            for (int c = 0; c < i; c++)
            {
                Frame changingFrame = Timeline[Loc.Item1].Frames[Loc.Item2 + c];
                changingFrame.AnimatedPath = pathwidget;
                Timeline[Loc.Item1].Frames[Loc.Item2 + c] = changingFrame;
            }

            ResetTimeline();
        }

        public void AddLayerRefresh(CanvasScript canvasAdding)
        {
            Track addingTrack = NewTrack();

            for (int i = 0; i < GetTimelineLength(); i++)
            {
                Frame addingFrame;
                addingFrame = NewFrame(i == FrameOn ? canvasAdding : App.Scene.AddCanvas());
                addingTrack.Frames.Add(addingFrame);
            }
            Timeline.Add(addingTrack);
            ResetTimeline();
        }

        public (int, int) GetCanvasLocation(CanvasScript canvas)
        {
            for (int trackNum = 0; trackNum < Timeline.Count; trackNum++)
            {
                for (int frameNum = 0; frameNum < Timeline[trackNum].Frames.Count; frameNum++)
                {
                    if (canvas.Equals(Timeline[trackNum].Frames[frameNum].Canvas))
                    {
                        return (trackNum, frameNum);
                    }
                }
            }
            return (-1, -1);
        }

        public CanvasScript GetTimelineCanvas(int trackIndex, int frameIndex)
        {
            if (trackIndex < Timeline.Count)
            {
                if (frameIndex < Timeline[trackIndex].Frames.Count)
                {
                    return Timeline[trackIndex].Frames[frameIndex].Canvas;
                }
            }
            return App.Scene.MainCanvas;
        }

        public List<List<CanvasScript>> GetTrackCanvases()
        {
            var timelineCavses = new List<List<CanvasScript>>();
            for (int l = 0; l < GetTimelineLength(); l++)
            {
                var canvasFrames = new List<CanvasScript>();

                for (int i = 0; i < Timeline.Count; i++)
                {
                    if (l >= Timeline[i].Frames.Count) { continue; }
                    canvasFrames.Add(Timeline[i].Frames[l].Canvas);
                }
                timelineCavses.Add(canvasFrames);
            }
            return timelineCavses;
        }

        public void UpdateLayerVisibilityRefresh(CanvasScript canvas)
        {
            bool visible = canvas.gameObject.activeSelf;
            (int, int) canvasIndex = GetCanvasLocation(canvas);

            if (canvasIndex.Item2 != -1)
            {
                Track thisTrack = Timeline[canvasIndex.Item1];
                thisTrack.Visible = visible;

                for (int i = 0; i < thisTrack.Frames.Count; i++)
                {
                    Frame changingFrame = thisTrack.Frames[i];
                    changingFrame.Visible = visible;
                    thisTrack.Frames[i] = changingFrame;
                }
                Timeline[canvasIndex.Item1] = thisTrack;
            }
        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas)
        {
            (int, int) canvasIndex = GetCanvasLocation(canvas);
            if (canvasIndex.Item2 != -1)
            {
                Track thisTrack = Timeline[canvasIndex.Item1];
                thisTrack.Deleted = true;
                Timeline[canvasIndex.Item1] = thisTrack;
            }
            ResetTimeline();
        }

        public int GetTimelineLength()
        {
            int maxLength = 0;

            for (int t = 0; t < Timeline.Count; t++)
            {
                if (Timeline[t].Frames.Count > maxLength)
                {
                    maxLength = Timeline[t].Frames.Count;
                }
            }
            return maxLength;
        }

        public void ResetTimeline()
        {
            if (timelineNotches != null)
            {
                foreach (var notch in timelineNotches)
                {
                    Destroy(notch);
                }
            }
            if (timelineFrameObjects != null)
            {
                foreach (var frame in timelineFrameObjects)
                {
                    Destroy(frame);
                }
            }
            foreach (Transform thisObj in timelineField.transform)
            {
                Destroy(thisObj.gameObject);
            }

            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();

            int timelineLength = GetTimelineLength();
            for (int f = 0; f < timelineLength; f++)
            {
                GameObject newNotch = Instantiate(timelineNotchPrefab);
                newNotch.transform.FindChild("Num").GetComponent<TextMeshPro>().text = "" + f;
                newNotch.transform.SetParent(timelineRef.transform);
                newNotch.SetActive(false);
                timelineNotches.Add(newNotch);
                GameObject newFrame = Instantiate(timelineFramePrefab, timelineField.transform, false);
                timelineFrameObjects.Add(newFrame);
                newFrame.name = "FrameContainer_" + f.ToString();

                GameObject frameWrapper = newFrame.transform.GetChild(0).gameObject;

                int numDeleted = 0;

                for (int i = frameWrapper.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(frameWrapper.transform.GetChild(i).gameObject);
                }

                for (int i = 0; i < Timeline.Count; i++)
                {
                    numDeleted += Timeline[i].Deleted ? 1 : 0;
                    int trackOn = i - numDeleted;
                    if (trackOn < Timeline.Count && !Timeline[i].Deleted)
                    {
                        var newButton = Instantiate(frameButtonPrefab, frameWrapper.transform, false);
                        var frameButton = newButton.transform.GetChild(0);

                        frameButton.GetComponent<MeshRenderer>().enabled = false;
                        frameButton.localPosition = new Vector3(0.00538007962f, 0.449999988f - m_FrameOffset * trackOn, 0);
                        frameButton.gameObject.SetActive(true);

                        frameButton.gameObject.GetComponent<FrameButton>().SetButtonCoordinate(i, f);

                        // Hide all ui indicators first
                        for (int o = 0; o < frameButton.GetChildCount(); o++)
                        {
                            frameButton.GetChild(o).gameObject.SetActive(false);

                            if (Timeline[i].Frames[f].AnimatedPath != null && frameButton.GetChild(o).gameObject.GetComponent<SpriteRenderer>() != null)
                            {
                                frameButton.GetChild(o).gameObject.GetComponent<SpriteRenderer>().color = new Color(92f / 255f, 52f / 255f, 237f / 255f);
                                Timeline[i].Frames[f].AnimatedPath.gameObject.SetActive(Timeline[i].Frames[f].Canvas.Equals(App.Scene.ActiveCanvas));
                            }
                        }

                        bool filled = GetFrameFilled(i, f);
                        bool backwardsConnect;
                        bool forwardConnect;

                        backwardsConnect = (f > 0 && Timeline[i].Frames[f].Canvas.Equals(Timeline[i].Frames[f - 1].Canvas));
                        forwardConnect = (f < Timeline[i].Frames.Count - 1 && Timeline[i].Frames[f].Canvas.Equals(Timeline[i].Frames[f + 1].Canvas));
                        frameButton.GetChild(Convert.ToInt32(filled)).gameObject.SetActive(true);

                        int backBox = 6;
                        frameButton.GetChild(backBox).gameObject.SetActive(true);

                        // Set behind colours depending whether frame is active
                        Color backColor;
                        if (filled)
                        {
                            if (Timeline[i].Frames[f].Canvas.Equals(App.Scene.ActiveCanvas))
                            {
                                backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                            }
                            else
                            {
                                backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f);
                            }
                        }
                        else
                        {
                            (int, int) index = GetCanvasLocation(App.Scene.ActiveCanvas);
                            if (index.Item1 == i && f == FrameOn)
                            {
                                backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                            }
                            else
                            {
                                backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f);
                            }
                        }

                        frameButton.GetChild(backBox).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox + 1).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox + 2).gameObject.GetComponent<SpriteRenderer>().color = backColor;

                        if (backwardsConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 2).gameObject.SetActive(true);
                            frameButton.GetChild(backBox + 1).gameObject.SetActive(true);
                        }

                        if (forwardConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 4).gameObject.SetActive(true);
                            frameButton.GetChild(backBox + 2).gameObject.SetActive(true);
                        }
                    }
                }
            }

            UpdateTimelineSlider();
            UpdateTimelineNob();
            UpdateTrackScroll();
            UpdateUI();
            App.Scene.TriggerLayersUpdate();
        }

        public void UpdateTrackScroll(int scrollOffset, float scrollHeight)
        {
            m_TrackScrollOffset = scrollOffset;
            for (int i = 0; i < timelineFrameObjects.Count; i++)
            {
                GameObject frameWrapper = timelineFrameObjects[i].transform.GetChild(0).gameObject;

                for (int c = 0; c < frameWrapper.transform.GetChildCount(); c++)
                {
                    GameObject frameObject = frameWrapper.transform.GetChild(c).gameObject;
                    Vector3 thisPos = frameObject.transform.localPosition;
                    thisPos.y = -scrollOffset * m_FrameOffset;
                    frameObject.transform.localPosition = thisPos;

                    int thisFrameOffset = c + scrollOffset;
                    if (thisFrameOffset >= 7 || thisFrameOffset < 0)
                    {
                        frameObject.SetActive(false);
                    }
                    else
                    {
                        frameObject.SetActive(true);
                    }
                }
            }
        }

        public void UpdateTrackScroll()
        {
            int scrollOffsetLocal = m_TrackScrollOffset;
            for (int i = 0; i < timelineFrameObjects.Count; i++)
            {
                GameObject frameWrapper = timelineFrameObjects[i].transform.GetChild(0).gameObject;

#if UNITY_EDITOR
                EditorGUIUtility.PingObject(frameWrapper);
#endif
                for (int c = 0; c < frameWrapper.transform.GetChildCount(); c++)
                {
                    GameObject frameObject = frameWrapper.transform.GetChild(c).gameObject;

                    Vector3 thisPos = frameObject.transform.localPosition;
                    thisPos.y = -scrollOffsetLocal * m_FrameOffset;
                    frameObject.transform.localPosition = thisPos;

                    int thisFrameOffset = c + scrollOffsetLocal;

                    if (thisFrameOffset >= 7 || thisFrameOffset < 0)
                    {
                        frameObject.SetActive(false);
                    }
                    else
                    {
                        frameObject.SetActive(true);
                    }
                }
            }
        }

        public void UpdateTimelineSlider()
        {
            float meshLength = timelineRef.GetComponent<TimelineSlider>().m_MeshScale.x;
            float startX = -meshLength / 2f - m_TimelineOffset * meshLength;

            int timelineLength = GetTimelineLength();
            for (int f = 0; f < timelineLength; f++)
            {
                float thisOffset = ((float)(f)) * m_SliderFrameSize * meshLength;

                float notchOffset = startX + ((float)(f)) * m_SliderFrameSize * meshLength;
                if (timelineNotches.ElementAtOrDefault(f) != null)
                {
                    GameObject notch = timelineNotches[f];
                    notch.transform.localPosition = new Vector3(notchOffset, 0, 0);
                    notch.transform.localRotation = Quaternion.identity;
                    notch.SetActive(notchOffset >= -meshLength * 0.5 && notchOffset <= meshLength * 0.5);
                }

                if (timelineFrameObjects.ElementAtOrDefault(f) != null)
                {
                    Vector3 newPosition = timelineFrameObjects[f].transform.localPosition;
                    float width = timelineFrameObjects[f].transform.GetChild(0).localScale.x;
                    newPosition.x = thisOffset - m_TimelineOffset * meshLength - width * 0.5f;
                    timelineFrameObjects[f].transform.localPosition = new Vector3(newPosition.x, 0, 0);
                    timelineFrameObjects[f].transform.localRotation = Quaternion.identity;
                    timelineFrameObjects[f].SetActive(newPosition.x >= -0.1 && newPosition.x <= meshLength - width);
                }
            }
        }

        public void SelectTimelineFrame(int trackNum, int frameNum)
        {
            App.Scene.ActiveCanvas = Timeline[trackNum].Frames[frameNum].Canvas;
            m_FrameOn = Math.Clamp((int)frameNum, 0, Timeline[trackNum].Frames.Count - 1);
            FocusFrame(frameNum);
            ResetTimeline();
            UpdateTimelineNob();
        }

        public void UpdateTimelineNob()
        {
            float newVal = (float)(m_FrameOn - 0.01) * m_SliderFrameSize - m_TimelineOffset;

            if (newVal >= 0.9f)
            {
                m_TimelineOffset += newVal - 0.9f;
            }

            if (newVal <= 0.1f)
            {
                m_TimelineOffset += newVal - 0.1f;
            }

            float max = m_SliderFrameSize * (float)GetTimelineLength() - 1;
            m_TimelineOffset = Math.Clamp(m_TimelineOffset, 0, max < 0 ? 0 : max);

            float clampedval = (float)newVal;
            clampedval = Math.Clamp(clampedval, 0, 1);
            timelineRef.GetComponent<TimelineSlider>().SetSliderValue(clampedval);
        }

        public void updateFrameInfo()
        {
            textRef.GetComponent<TextMeshPro>().text = (m_FrameOn.ToString("0.00")) + ":" + GetTimelineLength();
        }

        public void UpdateUI(bool timelineInput = false)
        {
            updateFrameInfo();
            UpdateTimelineSlider();
            if (!timelineInput) UpdateTimelineNob();

            deleteFrameButton.GetComponent<RemoveKeyFrameButton>().SetButtonAvailable(
                App.Scene.ActiveCanvas != null && App.Scene.ActiveCanvas != Timeline[0].Frames[0].Canvas &&
                GetFrameFilled(App.Scene.ActiveCanvas)
            );
        }

        public void focusFrameNum(int frameNum)
        {
            FocusFrame(frameNum);
        }

        private void FocusFrame(int FrameIndex, bool timelineInput = false)
        {
            for (int i = 0; i < GetTimelineLength(); i++)
            {
                if (i == FrameIndex)
                {
                    continue;
                }
                HideFrame(i, FrameIndex);
            }

            App.Scene.m_LayerCanvases = new List<CanvasScript>();
            for (int i = 0; i < Timeline.Count; i++)
            {
                if (FrameIndex >= Timeline[i].Frames.Count) continue;

                if (i == 0)
                {
                    App.Scene.m_MainCanvas = Timeline[i].Frames[FrameIndex].Canvas;
                    continue;
                }
                App.Scene.m_LayerCanvases.Add(Timeline[i].Frames[FrameIndex].Canvas);
            }

            (int, int) previousActiveCanvas = GetCanvasLocation(App.Scene.ActiveCanvas);
            if (previousActiveCanvas.Item1 != -1 && FrameIndex < Timeline[previousActiveCanvas.Item1].Frames.Count)
            {
                App.Scene.ActiveCanvas = Timeline[previousActiveCanvas.Item1].Frames[FrameIndex].Canvas;
            }

            ShowFrame(FrameIndex);
            UpdateUI(timelineInput);
            App.Scene.TriggerLayersUpdate();
        }

        public DeletedFrame RemoveKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            DeletedFrame deletedFrame;

            deletedFrame.Frame = Timeline[index.Item1].Frames[index.Item2];
            deletedFrame.Frame.Canvas = Timeline[index.Item1].Frames[index.Item2].Canvas;
            deletedFrame.Length = GetFrameLength(index.Item1, index.Item2);
            deletedFrame.Location = (index.Item1, index.Item2);

            App.Scene.HideCanvas(Timeline[index.Item1].Frames[index.Item2].Canvas);
            CanvasScript replacingCanvas = App.Scene.AddCanvas();
            for (int l = index.Item2; l < nextIndex.Item2; l++)
            {
                Frame removingFrame = NewFrame(replacingCanvas);
                Timeline[index.Item1].Frames[l] = removingFrame;
            }

            FillandCleanTimeline();
            SelectTimelineFrame(index.Item1, Math.Clamp(index.Item2, 0, GetTimelineLength() - 1));
            ResetTimeline();
            return deletedFrame;
        }

        public (int, int) GetFollowingFrameIndex(int trackNum, int frameNum)
        {
            int frameAt = frameNum;
            while (frameAt < Timeline[trackNum].Frames.Count)
            {
                if (!Timeline[trackNum].Frames[frameAt].Canvas.Equals(Timeline[trackNum].Frames[frameNum].Canvas))
                {
                    return (trackNum, frameAt);
                }
                frameAt++;
            }
            return (trackNum, frameAt);
        }

        public int GetTimelineMaxCanvas()
        {
            int maxLength = 0;

            for (int t = 0; t < Timeline.Count; t++)
            {
                for (int f = 0; f < Timeline[t].Frames.Count; f++)
                {
                    if (f > maxLength && GetFrameFilled(t, f))
                    {
                        maxLength = f;
                    }
                }
            }
            return maxLength;
        }

        public void CleanTimeline()
        {
            int maxTimeline = GetTimelineMaxCanvas();
            var newTimeline = new List<Track>();

            for (int t = 0; t < Timeline.Count; t++)
            {
                Track addingTrack = NewTrack();
                addingTrack.Deleted = Timeline[t].Deleted;
                newTimeline.Add(addingTrack);
                for (int f = 0; f < Timeline[t].Frames.Count; f++)
                {
                    if (f <= maxTimeline)
                    {
                        newTimeline[t].Frames.Add(Timeline[t].Frames[f]);
                    }
                }
            }
            Timeline = newTimeline;
        }

        public void FillTimeline()
        {
            int maxTimeline = GetTimelineLength();
            var newTimeline = new List<Track>();

            for (int t = 0; t < Timeline.Count; t++)
            {
                Track addingTrack = NewTrack();
                addingTrack.Deleted = Timeline[t].Deleted;
                newTimeline.Add(addingTrack);
                int f;
                for (f = 0; f < Timeline[t].Frames.Count; f++)
                {
                    newTimeline[t].Frames.Add(Timeline[t].Frames[f]);
                }

                if (f < maxTimeline)
                {
                    while (f < maxTimeline)
                    {
                        Frame addingFrame = NewFrame(App.Scene.AddCanvas());
                        newTimeline[t].Frames.Add(addingFrame);
                        f++;
                    }
                }
            }
            Timeline = newTimeline;
        }

        // Make sure there aren't too many or too few empty frames
        public void FillandCleanTimeline()
        {
            FillTimeline();
            CleanTimeline();
        }

        public (int, int) MoveKeyFrame(bool moveRight, int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            (int, int ) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);
            bool failure = false;

            if (moveRight)
            {
                if (nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count)
                {
                    Frame emptyFrame = NewFrame(App.Scene.AddCanvas());
                    Frame movedFrame = Timeline[index.Item1].Frames[index.Item2];
                    Timeline[index.Item1].Frames[index.Item2] = emptyFrame;
                    Timeline[nextIndex.Item1].Frames.Insert(Timeline[nextIndex.Item1].Frames.Count, movedFrame);
                }
                else if (!GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
                {
                    Frame tempFrame = Timeline[nextIndex.Item1].Frames[nextIndex.Item2];
                    Timeline[nextIndex.Item1].Frames[nextIndex.Item2] = Timeline[index.Item1].Frames[index.Item2];
                    Timeline[index.Item1].Frames[index.Item2] = tempFrame;
                }
                else
                {
                    failure = true;
                }
            }
            else
            {
                if (index.Item2 > 0 && !GetFrameFilled(index.Item1, index.Item2 - 1))
                {
                    int frameLength = GetFrameLength(index.Item1, index.Item2);
                    Frame tempFrame = Timeline[index.Item1].Frames[index.Item2 - 1];
                    Timeline[index.Item1].Frames[index.Item2 - 1] = Timeline[index.Item1].Frames[index.Item2 + frameLength - 1];
                    Timeline[index.Item1].Frames[index.Item2 + frameLength - 1] = tempFrame;
                }
                else
                {
                    failure = true;
                }
            }
            if (failure) return (-1, -1);
            FillandCleanTimeline();

            if (moveRight)
            {
                SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);
                return (index.Item1, index.Item2 + 1);
            }
            SelectTimelineFrame(index.Item1, index.Item2 - 1);
            return (index.Item1, index.Item2 - 1);
        }

        // For loading the scene
        // TODO Hidden by overloads
        public void AddKeyFrame(int trackNum)
        {
            (int, int) index = (trackNum, Timeline[trackNum].Frames.Count - 1);
            (int, int ) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            if (nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count)
            {
                Frame addingFrame = NewFrame(App.Scene.AddCanvas());
                Timeline[nextIndex.Item1].Frames.Insert(Timeline[nextIndex.Item1].Frames.Count, addingFrame);
                nextIndex.Item2 = Timeline[nextIndex.Item1].Frames.Count - 1;
            }
            else if (GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
            {
                Frame addingFrame = NewFrame(App.Scene.AddCanvas());
                Timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);
            }
        }

        public (int, int) AddKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            (int, int ) insertingAt;
            (int, int ) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            if (nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count)
            {
                Frame addingFrame = NewFrame(App.Scene.AddCanvas());
                Timeline[nextIndex.Item1].Frames.Insert(Timeline[nextIndex.Item1].Frames.Count, addingFrame);
                nextIndex.Item2 = Timeline[nextIndex.Item1].Frames.Count - 1;
                insertingAt = (nextIndex.Item1, Timeline[nextIndex.Item1].Frames.Count - 1);
            }
            else if (GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
            {
                Frame addingFrame = NewFrame(App.Scene.AddCanvas());
                Timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);
                insertingAt = nextIndex;
            }
            else
            {
                insertingAt = nextIndex;
            }

            FillTimeline();
            SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);
            return insertingAt;
        }

        // TODO this is hidden by overload
        public void ExtendKeyFrame(int trackNum)
        {
            (int, int) index = (trackNum, Timeline[trackNum].Frames.Count - 1);
            Frame addingFrame = NewFrame(Timeline[index.Item1].Frames[index.Item2].Canvas);
            addingFrame.Deleted = Timeline[index.Item1].Frames[index.Item2].Deleted;
            addingFrame.AnimatedPath = Timeline[index.Item1].Frames[index.Item2].AnimatedPath;
            Timeline[index.Item1].Frames.Insert(index.Item2 + 1, addingFrame);
        }

        public (int, int) ExtendKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            if (!GetFrameFilled(index.Item1, index.Item2))
            {
                return (-1, -1);
            }

            int frameLength = GetFrameLength(index.Item1, index.Item2);

            if (index.Item2 + frameLength >= Timeline[index.Item1].Frames.Count ||
                GetFrameFilled(index.Item1, index.Item2 + frameLength))
            {
                for (int l = 0; l < Timeline.Count; l++)
                {
                    if (l == index.Item1)
                    {
                        Frame addingFrame = NewFrame(Timeline[l].Frames[index.Item2].Canvas);
                        addingFrame.Deleted = Timeline[l].Frames[index.Item2].Deleted;
                        addingFrame.AnimatedPath = Timeline[l].Frames[index.Item2].AnimatedPath;

                        Timeline[l].Frames.Insert(index.Item2 + 1, addingFrame);
                    }
                    else
                    {
                        Frame addingFrame = NewFrame(App.Scene.AddCanvas());
                        Timeline[l].Frames.Insert(Timeline[l].Frames.Count, addingFrame);
                    }
                }
            }
            else
            {
                Frame addingFrame = NewFrame(Timeline[index.Item1].Frames[index.Item2].Canvas);
                addingFrame.Deleted = Timeline[index.Item1].Frames[index.Item2].Deleted;
                addingFrame.AnimatedPath = Timeline[index.Item1].Frames[index.Item2].AnimatedPath;
                Timeline[index.Item1].Frames[index.Item2 + frameLength] = addingFrame;
            }

            m_FrameOn++;
            FocusFrame((int)m_FrameOn);
            ResetTimeline();
            return index;
        }

        public (int, int) ReduceKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            int frameLength = GetFrameLength(index.Item1, index.Item2);
            if (frameLength > 1)
            {
                Frame emptyFrame = NewFrame(App.Scene.AddCanvas());
                Timeline[index.Item1].Frames[index.Item2 + frameLength - 1] = emptyFrame;

                m_FrameOn--;
                FocusFrame(FrameOn);
                ResetTimeline();
            }
            FillandCleanTimeline();
            return index;
        }

        public (int, int) splitKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);

            CanvasScript newCanvas = App.Scene.AddCanvas();
            CanvasScript oldCanvas = App.Scene.ActiveCanvas;

            int frameLegnth = GetFrameLength(index.Item1, index.Item2);


            int splittingIndex = FrameOn;
            if (splittingIndex < index.Item2 || splittingIndex > index.Item2 + frameLegnth - 1) return (-1, -1);

            List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                .Where(x => x.Canvas == oldCanvas).ToList();

            List<Stroke> newStrokes = oldStrokes.Select(stroke =>
                SketchMemoryScript.m_Instance.DuplicateStroke(stroke, App.Scene.SelectionCanvas, null))
                .ToList();

            foreach (var stroke in newStrokes)
            {
                switch (stroke.m_Type)
                {
                    case Stroke.Type.BrushStroke:
                        {
                            BaseBrushScript brushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
                            if (brushScript)
                            {
                                brushScript.HideBrush(false);
                            }
                        }
                        break;
                    case Stroke.Type.BatchedBrushStroke:
                        {
                            stroke.m_BatchSubset.m_ParentBatch.EnableSubset(stroke.m_BatchSubset);
                        }
                        break;
                    default:
                        Debug.LogError("Unexpected: redo NotCreated duplicate stroke");
                        break;
                }
                TiltMeterScript.m_Instance.AdjustMeter(stroke, up: true);

                stroke.SetParentKeepWorldPosition(newCanvas);
            }

            for (int f = splittingIndex; f < index.Item2 + frameLegnth; f++)
            {
                Frame addingFrame = NewFrame(newCanvas);
                Timeline[index.Item1].Frames[f] = addingFrame;
            }

            SelectTimelineFrame(index.Item1, splittingIndex);
            ResetTimeline();
            return (index.Item1, splittingIndex);
        }

        public (int, int) duplicateKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            CanvasScript newCanvas = App.Scene.AddCanvas();
            CanvasScript oldCanvas = App.Scene.ActiveCanvas;

            int frameLegnth = GetFrameLength(index.Item1, index.Item2);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);
            List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                .Where(x => x.Canvas == oldCanvas).ToList();

            List<Stroke> newStrokes = oldStrokes
                .Select(stroke => SketchMemoryScript.m_Instance.DuplicateStroke(
                    stroke, App.Scene.SelectionCanvas, null)).ToList();

            foreach (var stroke in newStrokes)
            {
                switch (stroke.m_Type)
                {
                    case Stroke.Type.BrushStroke:
                        BaseBrushScript brushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
                        if (brushScript)
                        {
                            brushScript.HideBrush(false);
                        }
                        break;
                    case Stroke.Type.BatchedBrushStroke:
                        stroke.m_BatchSubset.m_ParentBatch.EnableSubset(stroke.m_BatchSubset);
                        break;
                    default:
                        Debug.LogError("Unexpected: redo NotCreated duplicate stroke");
                        break;
                }
                TiltMeterScript.m_Instance.AdjustMeter(stroke, up: true);
                stroke.SetParentKeepWorldPosition(newCanvas);
            }

            for (int f = 0; f < frameLegnth; f++)
            {
                if (nextIndex.Item2 + f < Timeline[nextIndex.Item1].Frames.Count &&
                    !GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
                {
                    Destroy(Timeline[nextIndex.Item1].Frames[nextIndex.Item2 + f].Canvas);
                    Frame addingFrame = NewFrame(newCanvas);

                    Timeline[nextIndex.Item1].Frames[nextIndex.Item2 + f] = addingFrame;
                }
                else
                {
                    Frame addingFrame = NewFrame(newCanvas);
                    Timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2 + f, addingFrame);
                }
            }

            FillTimeline();
            SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);
            ResetTimeline();
            return nextIndex;
        }

        public void TimelineSlideDown(bool down)
        {
            m_Scrolling = down;
        }

        public void TimelineSlide(float Value)
        {
            gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().OnDisable();
            m_FrameOn = ((float)(Value + m_TimelineOffset) / m_SliderFrameSize);

            int timelineLength = GetTimelineLength();
            m_FrameOn = m_FrameOn >= timelineLength ? timelineLength : m_FrameOn;
            m_FrameOn = m_FrameOn < 0 ? 0 : m_FrameOn;

            FocusFrame(FrameOn, true);
            UpdateLayerTransforms();

            // Scrolling the timeline
            if (Value < 0.1f)
            {
                m_TimelineOffset -= 0.05f;
            }
            if (Value > 0.9f)
            {
                m_TimelineOffset += 0.05f;
            }

            float max = m_SliderFrameSize * (float)timelineLength - 1;
            m_TimelineOffset = Math.Clamp(m_TimelineOffset, 0, max < 0 ? 0 : max);
            gameObject.GetComponent<TiltBrush.Layers.LayerUI_Manager>().OnEnable();
        }

        public void StartAnimation()
        {
            m_Start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            m_Playing = true;
        }

        public void StopAnimation()
        {
            m_Playing = false;
        }

        public bool GetChanging()
        {
            return m_Playing || m_Scrolling;
        }

        public void ToggleAnimation()
        {
            if (m_Playing) { StopAnimation(); }
            else StartAnimation();
        }

        public int GetFrameLength(int trackOn, int frameOn)
        {
            CanvasScript canvasOn = Timeline[trackOn].Frames[frameOn].Canvas;
            (int, int) coord = GetCanvasLocation(canvasOn);

            int frameLength = 0;
            while (
                coord.Item2 + frameLength < Timeline[coord.Item1].Frames.Count &&
                Timeline[coord.Item1].Frames[coord.Item2 + frameLength].Canvas.Equals(canvasOn)
            ) { frameLength++; }
            return frameLength;
        }

        public float GetSmoothAnimationTime(Track trackOn)
        {
            CanvasScript canvasAnimating = trackOn.Frames[FrameOn].Canvas;
            (int, int) coord = GetCanvasLocation(canvasAnimating);

            int frameLength = 0;
            while (
                coord.Item2 + frameLength < Timeline[coord.Item1].Frames.Count &&
                Timeline[coord.Item1].Frames[coord.Item2 + frameLength].Canvas.Equals(canvasAnimating)
            ) { frameLength++; }
            return (m_FrameOn - coord.Item2) / frameLength;
        }

        public void UpdateLayerTransforms()
        {
            int frameInt = FrameOn;

            // Update layer animation transforms
            if (frameInt < 0) return;
            for (int t = 0; t < Timeline.Count; t++)
            {
                if (frameInt >= Timeline[t].Frames.Count) { continue; }
                if (Timeline[t].Frames[frameInt].AnimatedPath != null)
                {
                    float canvasTime = GetSmoothAnimationTime(Timeline[t]) * (Timeline[t].Frames[frameInt].AnimatedPath.Path.NumPositionKnots - 1);
                    PathT pathTime = new TiltBrush.PathT(canvasTime);
                    PathT pathStart = new TiltBrush.PathT(0);
                    TrTransform pathPosition = TrTransform.FromTransform(Timeline[t].Frames[frameInt].AnimatedPath.gameObject.transform);
                    TrTransform posStart = App.Scene.Pose.inverse * TrTransform.TR(Timeline[t].Frames[frameInt].AnimatedPath.Path.GetPosition(pathStart), Timeline[t].Frames[frameInt].AnimatedPath.Path.GetRotation(pathStart));
                    TrTransform posNow = App.Scene.Pose.inverse * TrTransform.TR(Timeline[t].Frames[frameInt].AnimatedPath.Path.GetPosition(pathTime), Timeline[t].Frames[frameInt].AnimatedPath.Path.GetRotation(pathTime));
                    TrTransform posDifference = posNow * posStart.inverse;
                    Timeline[t].Frames[frameInt].Canvas.LocalPose = posDifference;
                    TrTransform pathPositionConstant = (pathPosition);
                    Timeline[t].Frames[frameInt].AnimatedPath.gameObject.transform.position = pathPositionConstant.translation;
                    Timeline[t].Frames[frameInt].AnimatedPath.gameObject.transform.rotation = pathPositionConstant.rotation;
                }
            }
        }

        void Update()
        {
            if (m_LastCanvas != App.Scene.ActiveCanvas)
            {
                m_PreviousCanvasBatches = 0;
            }
            m_LastCanvas = App.Scene.ActiveCanvas;

            int currentBatchPools = App.Scene.ActiveCanvas.BatchManager.GetNumBatchPools();

            if (currentBatchPools != 0 && m_PreviousCanvasBatches != currentBatchPools)
            {
                ResetTimeline();
                m_PreviousCanvasBatches = currentBatchPools;
            }

            if (m_Playing)
            {
                m_Time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                m_Current = m_Time - m_Start;
                m_FrameOn = m_Current / (1000f / m_Fps);

                m_FrameOn %= GetTimelineLength();
                FocusFrame(FrameOn);

                // Update layer animation transforms
                UpdateLayerTransforms();
            }
        }
    }
} // namespace TiltBrush.FrameAnimation
