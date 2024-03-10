// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;
using System.IO;

namespace TiltBrush
{
    public class SoundClipWidget : Media2dWidget
    {
        // AudioState is used to restore the state of a sound clip when loading, or when a SoundClipWidget is
        // restored from being tossed with an undo.
        private class SoundClipState
        {
            public bool Paused;
            public float Volume;
            public float? Time;
        }

        private SoundClip m_SoundClip;
        private SoundClipState m_InitialState;

        public SoundClip SoundClip
        {
            get { return m_SoundClip; }
        }

        public override float? AspectRatio => 1.0f;

        public TrTransform SaveTransform
        {
            get
            {
                TrTransform xf = TrTransform.FromLocalTransform(transform);
                xf.scale = GetSignedWidgetSize();
                return xf;
            }
        }

        public SoundClip.SoundClipController SoundClipController { get; private set; }

        public void SetSoundClip(SoundClip soundClip)
        {
            m_SoundClip = soundClip;

            var size = GetWidgetSizeRange();
            if (m_SoundClip.Aspect > 1)
            {
                m_Size = Mathf.Clamp(2 / m_SoundClip.Aspect / Coords.CanvasPose.scale, size.x, size.y);
            }
            else
            {
                m_Size = Mathf.Clamp(2 * m_SoundClip.Aspect / Coords.CanvasPose.scale, size.x, size.y);
            }

            // Create in the main canvas.
            HierarchyUtils.RecursivelySetLayer(transform, App.Scene.MainCanvas.gameObject.layer);
            HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);

            // InitSnapGhost(m_ImageQuad.transform, transform);
            Play();
        }

        protected override void OnShow()
        {
            base.OnShow();
            Play();
        }

        public override void RestoreFromToss()
        {
            base.RestoreFromToss();
            Play();
        }

        protected override void OnHide()
        {
            base.OnHide();
            // store off the sound clip state so that if the widget gets shown again it will reset to that.
            if (SoundClipController != null)
            {
                m_InitialState = new SoundClipState
                {
                    Paused = !SoundClipController.Playing,
                    Time = SoundClipController.Time,
                    Volume = SoundClipController.Volume,
                };
                SoundClipController.Dispose();
                SoundClipController = null;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SoundClipController?.Dispose();
            SoundClipController = null;
        }

        private void Play()
        {
            if (m_SoundClip == null || SoundClipController != null)
            {
                return;
            }
            // If instances of the sound clip already exist, don't override with new state.
            if (m_SoundClip.HasInstances)
            {
                m_InitialState = null;
            }
            SoundClipController = m_SoundClip.CreateController(this);

            //SoundClipController.m_SoundClipAudioSource.Play();
            SoundClipController.OnSoundClipInitialized += OnSoundClipInitialized;
        }

        private void OnSoundClipInitialized()
        {
            UpdateScale();
            if (m_InitialState != null)
            {
                SoundClipController.Volume = m_InitialState.Volume;
                if (m_InitialState.Time.HasValue)
                {
                    SoundClipController.Time = m_InitialState.Time.Value;
                }
                if (m_InitialState.Paused)
                {
                    SoundClipController.Playing = false;
                }
                m_InitialState = null;
            }
        }

        public static void FromTiltSoundClip(TiltSoundClip tiltSoundClip)
        {
            SoundClipWidget soundClipWidget = Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab);
            soundClipWidget.m_LoadingFromSketch = true;
            soundClipWidget.transform.parent = App.Instance.m_CanvasTransform;
            soundClipWidget.transform.localScale = Vector3.one;

            var soundClip = SoundClipCatalog.Instance.GetSoundClipByPersistentPath(tiltSoundClip.FilePath);
            if (soundClip == null)
            {
                soundClip = TiltBrush.SoundClip.CreateDummySoundClip();
                ControllerConsoleScript.m_Instance.AddNewLine(
                    $"Could not find sound clip {App.SoundClipLibraryPath()}\\{tiltSoundClip.FilePath}.");
            }
            soundClipWidget.SetSoundClip(soundClip);
            soundClipWidget.m_InitialState = new SoundClipState
            {
                Volume = tiltSoundClip.Volume,
                Paused = tiltSoundClip.Paused,
            };
            if (tiltSoundClip.Paused)
            {
                soundClipWidget.m_InitialState.Time = tiltSoundClip.Time;
            }
            soundClipWidget.SetSignedWidgetSize(tiltSoundClip.Transform.scale);
            soundClipWidget.Show(bShow: true, bPlayAudio: false);
            soundClipWidget.transform.localPosition = tiltSoundClip.Transform.translation;
            soundClipWidget.transform.localRotation = tiltSoundClip.Transform.rotation;
            if (tiltSoundClip.Pinned)
            {
                soundClipWidget.PinFromSave();
            }
            soundClipWidget.Group = App.GroupManager.GetGroupFromId(tiltSoundClip.GroupId);
            soundClipWidget.SetCanvas(App.Scene.GetOrCreateLayer(tiltSoundClip.LayerId));
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(soundClipWidget.GetTiltMeterCost(), up: true);
            soundClipWidget.UpdateScale();
        }

        override public GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }
        override public GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            SoundClipWidget clone = Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab) as SoundClipWidget;
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.m_LoadingFromSketch = true; // prevents intro animation
            clone.transform.parent = transform.parent;
            clone.SetSoundClip(m_SoundClip);
            clone.SetSignedWidgetSize(size);
            clone.Show(bShow: true, bPlayAudio: false);
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
            clone.CloneInitialMaterials(this);
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        public override void Activate(bool bActive)
        {
            base.Activate(bActive);
            if (bActive)
            {
                App.Switchboard.TriggerSoundClipWidgetActivated(this);
            }
        }

        protected override void UpdateScale()
        {
            transform.localScale = Vector3.one * m_Size;
        }

        public override string GetExportName()
        {
            return Path.GetFileNameWithoutExtension(m_SoundClip.AbsolutePath);
        }
    }
} // namespace TiltBrush
