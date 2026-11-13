// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using UnityEngine;

namespace TiltBrush
{
    public class ModelPositionSlider : BaseSlider
    {
        private ModelWidget m_ModelWidget;
        private double? m_PendingTime;

        public ModelWidget ModelWidget
        {
            get => m_ModelWidget;
            set
            {
                if (m_ModelWidget == value) return;
                m_ModelWidget = value;
                m_PendingTime = null;
            }
        }

        private ModelPlaybackController Playback => m_ModelWidget != null ? m_ModelWidget.Playback : null;
        public double TargetTime => m_PendingTime ?? Playback?.TargetTime ?? 0;

        public override void UpdateValue(float value)
        {
            var playback = Playback;
            if (playback == null || !playback.Initialized || playback.Length <= 0) return;
            RequestSeek(Mathf.Clamp01(value) * playback.Length);
        }

        public void RequestSeek(double time)
        {
            var playback = Playback;
            if (playback == null || !playback.Initialized || double.IsNaN(time) || double.IsInfinity(time)) return;
            // Native seeks can span frames. Keep the latest target until the previous edit completes.
            m_PendingTime = Math.Max(0, Math.Min(time, playback.Length));
            ApplyPendingSeek();
        }

        private void ApplyPendingSeek()
        {
            var playback = Playback;
            if (!m_PendingTime.HasValue || playback == null || !playback.Initialized || playback.IsRestoring) return;
            double time = m_PendingTime.Value;
            m_PendingTime = null;
            playback.Seek(Math.Min(time, playback.Length));
        }

        private void Update()
        {
            var playback = Playback;
            bool available = m_ModelWidget != null && m_ModelWidget.gameObject.activeInHierarchy &&
                playback.Initialized && playback.Length > 0;
            if (m_IsAvailable != available) SetAvailable(available);
            if (available)
            {
                ApplyPendingSeek();
                m_CurrentValue = Mathf.Clamp01((float)(TargetTime / playback.Length));
            }
            else m_CurrentValue = 0;
            SetSliderPositionToReflectValue();
        }

        private void OnDisable() => m_PendingTime = null;
    }
}
