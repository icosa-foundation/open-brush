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

using System;
using UnityEngine;

namespace TiltBrush
{
    [System.Serializable]
    public class StrokeData
    {
        public enum ColorControlMode
        {
            None,      // Use m_Color for entire stroke
            Replace,   // Replace with per-point colors
            Multiply,  // Multiply m_Color with per-point colors
            Add        // Add per-point colors to m_Color
        }

        public Color m_Color;
        public Guid m_BrushGuid;
        // The room-space size of the brush when the stroke was laid down
        public float m_BrushSize;
        // The size of the pointer, relative to  when the stroke was laid down.
        // AKA, the "pointer to local" scale factor.
        // m_BrushSize * m_BrushScale = size in local/canvas space
        public float m_BrushScale;
        public PointerManager.ControlPoint[] m_ControlPoints;
        public SketchMemoryScript.StrokeFlags m_Flags;
        // Seed for deterministic pseudo-random numbers for geometry generation.
        // Not currently serialized.
        public int m_Seed;
        protected SketchGroupTag m_Group = SketchGroupTag.None;
        public SketchGroupTag Group => m_Group;
        public Guid m_Guid;

        // Optional per-control-point color data (null by default for 0 memory overhead)
        public Color32[] m_ControlPointColors;
        public ColorControlMode m_ColorMode = ColorControlMode.None;

        // Reference the BrushStrokeCommand that created this stroke with a WeakReference.
        // This allows the garbage collector to collect the BrushStrokeCommand if it's no
        // longer in use elsewhere.
        [NonSerialized] private WeakReference<BrushStrokeCommand> m_Command;
        public BrushStrokeCommand Command
        {
            get
            {
                if (m_Command != null && m_Command.TryGetTarget(out var command))
                    return command;
                return null;
            }
            set { m_Command = new WeakReference<BrushStrokeCommand>(value); }
        }


        /// This creates a copy of the given stroke.
        public StrokeData(StrokeData existing = null)
        {
            if (existing != null)
            {
                this.m_Color = existing.m_Color;
                this.m_BrushGuid = existing.m_BrushGuid;
                this.m_BrushSize = existing.m_BrushSize;
                this.m_BrushScale = existing.m_BrushScale;
                this.m_Flags = existing.m_Flags;
                this.m_Seed = existing.m_Seed;
                this.m_Group = existing.m_Group;
                this.m_ControlPoints = new PointerManager.ControlPoint[existing.m_ControlPoints.Length];
                Array.Copy(existing.m_ControlPoints, this.m_ControlPoints, this.m_ControlPoints.Length);

                // Copy per-point color data if present
                this.m_ColorMode = existing.m_ColorMode;
                if (existing.m_ControlPointColors != null)
                {
                    this.m_ControlPointColors = new Color32[existing.m_ControlPointColors.Length];
                    Array.Copy(existing.m_ControlPointColors, this.m_ControlPointColors, this.m_ControlPointColors.Length);
                }
            }
        }

        /// Get the color for a specific control point index.
        /// Returns m_Color if per-point colors are not used, or blends according to m_ColorMode.
        public Color32 GetColor(int index)
        {
            // If no per-point colors or mode is None, use base color
            if (m_ControlPointColors == null || m_ColorMode == ColorControlMode.None)
            {
                return m_Color;
            }

            // Bounds check
            if (index < 0 || index >= m_ControlPointColors.Length)
            {
                return m_Color;
            }

            Color32 controlpointColor = m_ControlPointColors[index];
            Color32 calculatedColor;

            switch (m_ColorMode)
            {
                case ColorControlMode.Replace:
                    calculatedColor = controlpointColor;
                    break;

                case ColorControlMode.Multiply:
                    // Convert to Color for accurate multiplication, then back to Color32
                    Color baseColor = m_Color;
                    Color point = controlpointColor;
                    Color result = new Color(
                        baseColor.r * point.r,
                        baseColor.g * point.g,
                        baseColor.b * point.b,
                        baseColor.a * point.a
                    );
                    calculatedColor = result;
                    break;

                case ColorControlMode.Add:
                    // Additive blending with clamping
                    calculatedColor = new Color32(
                        (byte)Mathf.Min(255, m_Color.r + controlpointColor.r),
                        (byte)Mathf.Min(255, m_Color.g + controlpointColor.g),
                        (byte)Mathf.Min(255, m_Color.b + controlpointColor.b),
                        (byte)Mathf.Min(255, m_Color.a + controlpointColor.a)
                    );
                    break;

                default:
                    calculatedColor = new Color(
                        (byte)Mathf.RoundToInt(m_Color.r * 255f),
                        (byte)Mathf.RoundToInt(m_Color.g * 255f),
                        (byte)Mathf.RoundToInt(m_Color.b * 255f),
                        (byte)Mathf.RoundToInt(m_Color.a * 255f)
                    );
                    break;
            }
            return calculatedColor;
        }
    }
} // namespace TiltBrush
