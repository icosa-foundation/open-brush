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
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace TiltBrush
{

    public class VrControllers : MonoBehaviour
    {
        [SerializeField] private ControllerBehaviorWand m_Wand;
        [SerializeField] private ControllerBehaviorBrush m_Brush;

        // Monobehaviours that must be disabled to disable pose tracking
        [SerializeField] private MonoBehaviour[] m_TrackingComponents;

        [Header("SteamVR Haptics")]
        [FormerlySerializedAs("m_HapticsScaleAdjustment")]
        [SerializeField] private float m_HapticsDurationScale = 1.0f;
        [SerializeField] private float m_HapticAmplitudeScale = 1.0f;

        [Header("Input Zones")]
        [SerializeField] private Vector2 m_TouchpadActivationRange = new Vector2(-.8f, .8f);
        [SerializeField] private float m_WandRotateJoystickPercent = 0.7f;

        // VR headsets (e.g., Rift, Vive, Wmr) use different hardware for their controllers,
        // they require a scaled duration for the haptics to be felt in users hand.
        public float HapticsDurationScale => m_HapticsDurationScale;

        public float HapticsAmplitudeScale => m_HapticAmplitudeScale;

        public BaseControllerBehavior GetBehavior(InputManager.ControllerName name)
        {
            switch (name)
            {
                case InputManager.ControllerName.Brush:
                    return m_Brush;
                case InputManager.ControllerName.Wand:
                    return m_Wand;
                default:
                    throw new System.ArgumentException($"Unknown controller behavior {name}");
            }
        }

        public BaseControllerBehavior[] GetBehaviors()
        {
            Debug.Assert((int)m_Wand.ControllerName == 0);
            Debug.Assert((int)m_Brush.ControllerName == 1);
            // The array is indexed by ControllerName, so the order here is important!
            return new BaseControllerBehavior[] { m_Wand, m_Brush };
        }

        /// Normally all controllers are assumed to be the same style.
        /// Logitech switches one of them to be a pen.
        /// This returns the style that both controllers used before one of them was switched to a pen.
        public ControllerStyle BaseControllerStyle
        {
            get
            {
                if (m_Wand.ControllerGeometry.Style != ControllerStyle.LogitechPen)
                {
                    return m_Wand.ControllerGeometry.Style;
                }
                else
                {
                    return m_Brush.ControllerGeometry.Style;
                }
            }
        }

        public Vector2 TouchpadActivationRange
        {
            get => m_TouchpadActivationRange;
        }

        public float WandRotateJoystickPercent
        {
            get => m_WandRotateJoystickPercent;
        }

        public ControllerBehaviorWand Wand
        {
            get => m_Wand;
        }

        public ControllerBehaviorBrush Brush
        {
            get => m_Brush;
        }

        public bool LogitechPenIsPresent()
        {
            return (m_Brush.ControllerGeometry.Style == ControllerStyle.LogitechPen) ||
                (m_Wand.ControllerGeometry.Style == ControllerStyle.LogitechPen);
        }

        public bool PrimaryScrollDirectionIsX(InputManager.ControllerName name)
        {
            var behavior = GetBehavior(name);
            if (behavior.ControllerGeometry.Style == ControllerStyle.LogitechPen)
            {
                return false;
            }
            return true;
        }

        /// Enable or disable tracking
        public void EnablePoseTracking(bool enabled)
        {
            foreach (var comp in m_TrackingComponents)
            {
                comp.enabled = enabled;
            }
        }

        // A procedural demonstration owns only controller tracking; head tracking is untouched.
        // Restore each component's original state rather than enabling previously disabled drivers.
        public System.IDisposable SuspendPoseTracking()
        {
            // UnityXR prefabs still list the controller behaviours in m_TrackingComponents.
            // Disabling those does not stop the separate TrackedPoseDrivers from writing
            // hardware poses during Update and before rendering. Keep behaviours active
            // and suspend the actual pose writers on both controller hierarchies.
            var components = new List<MonoBehaviour>();
            foreach (var component in m_TrackingComponents)
            {
                if (component != null && !(component is BaseControllerBehavior) && !components.Contains(component))
                {
                    components.Add(component);
                }
            }
            foreach (var behavior in GetBehaviors())
            {
                foreach (var driver in behavior.GetComponentsInChildren<UnityEngine.SpatialTracking.TrackedPoseDriver>(true))
                {
                    if (!components.Contains(driver)) { components.Add(driver); }
                }
                foreach (var driver in behavior.GetComponentsInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>(true))
                {
                    if (!components.Contains(driver)) { components.Add(driver); }
                }
            }
            return new TrackingSuspension(components.ToArray());
        }

        private sealed class TrackingSuspension : System.IDisposable
        {
            private readonly MonoBehaviour[] m_Components;
            private readonly bool[] m_Enabled;
            private bool m_Disposed;

            public TrackingSuspension(MonoBehaviour[] components)
            {
                m_Components = components;
                m_Enabled = new bool[components.Length];
                for (int i = 0; i < components.Length; ++i)
                {
                    if (components[i] == null) { continue; }
                    m_Enabled[i] = components[i].enabled;
                    components[i].enabled = false;
                }
            }

            public void Dispose()
            {
                if (m_Disposed) { return; }
                m_Disposed = true;
                for (int i = 0; i < m_Components.Length; ++i)
                {
                    if (m_Components[i] != null) { m_Components[i].enabled = m_Enabled[i]; }
                }
            }
        }
    }

} // namespace TiltBrush
