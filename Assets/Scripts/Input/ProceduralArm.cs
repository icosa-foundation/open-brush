// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using UnityEngine;

namespace TiltBrush
{
    /// Invisible two-link arm. Reaches interpolate shoulder/elbow rotations, not hand positions.
    /// Drawing solves each prescribed tip position so the arm cannot silently deform the stroke.
    public sealed class ProceduralArm
    {
        public struct Pose
        {
            public Quaternion UpperArm;
            public Quaternion Forearm;
            public Quaternion Controller;
        }

        private readonly Vector3 m_Shoulder;
        private readonly Vector3 m_ElbowPole;
        private readonly float m_UpperLength;
        private readonly float m_ForearmLength;

        public ProceduralArm(Vector3 shoulder, Vector3 elbowPole, float upperLength, float forearmLength)
        {
            m_Shoulder = shoulder;
            m_ElbowPole = elbowPole;
            m_UpperLength = upperLength;
            m_ForearmLength = forearmLength;
        }

        public bool CanReach(Vector3 wrist)
        {
            float distance = Vector3.Distance(wrist, m_Shoulder);
            return distance > Mathf.Abs(m_UpperLength - m_ForearmLength) + 0.01f &&
                   distance < m_UpperLength + m_ForearmLength - 0.01f;
        }

        public Pose Solve(Vector3 wrist, Quaternion controllerRotation)
        {
            if (!CanReach(wrist))
            {
                throw new InvalidOperationException("Target exceeds the procedural arm's reach. Move the panel/stroke closer or adjust arm lengths.");
            }

            Vector3 delta = wrist - m_Shoulder;
            float distance = delta.magnitude;
            Vector3 direction = delta / distance;
            Vector3 bend = Vector3.ProjectOnPlane(m_ElbowPole, direction).normalized;
            if (bend.sqrMagnitude < 0.001f)
            {
                throw new InvalidOperationException("Arm target aligns with its elbow pole.");
            }
            float along = (distance * distance + m_UpperLength * m_UpperLength -
                m_ForearmLength * m_ForearmLength) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, m_UpperLength * m_UpperLength - along * along));
            Vector3 elbow = m_Shoulder + direction * along + bend * height;
            Vector3 up = Vector3.Cross(direction, bend);
            Quaternion upper = Quaternion.LookRotation(elbow - m_Shoulder, up);
            Quaternion forearm = Quaternion.LookRotation(wrist - elbow, up);
            return new Pose
            {
                UpperArm = upper,
                Forearm = Quaternion.Inverse(upper) * forearm,
                Controller = Quaternion.Inverse(forearm) * controllerRotation
            };
        }

        public UnityEngine.Pose Evaluate(Pose pose)
        {
            Quaternion forearm = pose.UpperArm * pose.Forearm;
            Vector3 elbow = m_Shoulder + pose.UpperArm * Vector3.forward * m_UpperLength;
            return new UnityEngine.Pose(elbow + forearm * Vector3.forward * m_ForearmLength,
                forearm * pose.Controller);
        }

        public UnityEngine.Pose Reach(Pose start, Pose end, float t)
        {
            t = MinimumJerk(t);
            return Evaluate(new Pose
            {
                UpperArm = Quaternion.Slerp(start.UpperArm, end.UpperArm, t),
                Forearm = Quaternion.Slerp(start.Forearm, end.Forearm, t),
                Controller = Quaternion.Slerp(start.Controller, end.Controller, t)
            });
        }

        public static float MinimumJerk(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (10 + t * (-15 + 6 * t));
        }

        public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1 - t;
            return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d;
        }
    }
}
