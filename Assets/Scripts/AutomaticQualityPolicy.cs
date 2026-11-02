// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;

namespace TiltBrush
{
    // Keep recovery slower than degradation, and stop retrying a failed promotion rapidly.
    internal sealed class AutomaticQualityPolicy
    {
        public double SettlingSeconds { get; set; } = 1;
        public double RecoverySeconds { get; set; } = 10;
        public double RetrySeconds { get; set; } = 30;

        private double m_SettleUntil;
        private double m_HighFpsSince = double.NaN;
        private int m_LowFrames;
        private int m_HighFrames;
        private int m_LastPromotedLevel = -1;
        private double m_LastPromotionTime;
        private double m_NextPromotionTime;
        private double m_RetryDelay;

        public void ResetMeasurement(double now)
        {
            // FPS uses a one-second window: wait until samples from the old level expire.
            m_SettleUntil = now + Math.Max(1, SettlingSeconds);
            m_HighFpsSince = double.NaN;
            m_LowFrames = m_HighFrames = 0;
        }

        public void Reset(double now)
        {
            ResetMeasurement(now);
            m_LastPromotedLevel = -1;
            m_NextPromotionTime = now;
            m_RetryDelay = 0;
        }

        public int Evaluate(double now, int level, int maxLevel, int fps,
            float lowerFps, float higherFps, int lowerFrames, int higherFrames)
        {
            if (now < m_SettleUntil) return level;

            m_LowFrames = fps <= lowerFps ? m_LowFrames + 1 : 0;
            if (fps >= higherFps)
            {
                ++m_HighFrames;
                if (double.IsNaN(m_HighFpsSince)) m_HighFpsSince = now;
            }
            else
            {
                m_HighFrames = 0;
                m_HighFpsSince = double.NaN;
            }

            if (m_LowFrames >= lowerFrames && level > 0)
            {
                if (level == m_LastPromotedLevel &&
                    now - m_LastPromotionTime <= Math.Max(1, SettlingSeconds) + RecoverySeconds)
                {
                    double initialDelay = Math.Max(1, RetrySeconds);
                    m_RetryDelay = m_RetryDelay == 0 ? initialDelay :
                        Math.Min(Math.Max(300, initialDelay), m_RetryDelay * 2);
                    m_NextPromotionTime = now + m_RetryDelay;
                }
                m_LastPromotedLevel = -1;
                ResetMeasurement(now);
                return level - 1;
            }

            if (m_HighFrames >= higherFrames && !double.IsNaN(m_HighFpsSince) &&
                now - m_HighFpsSince >= Math.Max(0, RecoverySeconds) &&
                now >= m_NextPromotionTime && level < maxLevel)
            {
                m_LastPromotedLevel = level + 1;
                m_LastPromotionTime = now;
                ResetMeasurement(now);
                return level + 1;
            }

            return level;
        }
    }
}
