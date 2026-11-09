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
        private readonly UnityEngine.Pose m_GripLocal;
        private readonly Vector3 m_BodyUp;

        // Conservative authoring limits for a comfortable grip, not a full anatomical model.
        private const float k_MaxWristFlexion = 55;
        private const float k_MaxWristDeviation = 35;
        private const float k_MaxWristTwist = 25;
        private const float k_MaxForearmTwist = 90;

        public ProceduralArm(Vector3 shoulder, Vector3 elbowPole, float upperLength, float forearmLength,
            UnityEngine.Pose gripLocal, Vector3 bodyUp)
        {
            m_Shoulder = shoulder;
            m_ElbowPole = elbowPole;
            m_UpperLength = upperLength;
            m_ForearmLength = forearmLength;
            m_GripLocal = gripLocal;
            m_BodyUp = bodyUp.normalized;
        }

        public bool CanReach(Vector3 controllerPosition, Quaternion controllerRotation) =>
            CanReachWrist(controllerPosition + controllerRotation * m_GripLocal.position);

        private bool CanReachWrist(Vector3 wrist)
        {
            float distance = Vector3.Distance(wrist, m_Shoulder);
            return distance > Mathf.Abs(m_UpperLength - m_ForearmLength) + 0.01f &&
                   distance < m_UpperLength + m_ForearmLength - 0.01f;
        }

        public UnityEngine.Pose RelaxedGripPose(Vector3 contactPosition, Vector3 contactLocal)
        {
            Quaternion controller = RelaxedRotation(contactPosition, contactLocal);
            var result = new UnityEngine.Pose(contactPosition - controller * contactLocal, controller);
            Solve(result.position, result.rotation);
            return result;
        }

        private Quaternion RelaxedRotation(Vector3 contactPosition, Vector3 contactLocal)
        {
            Vector3 wrist = contactPosition;
            Quaternion controller = Quaternion.identity;
            // The native grab anchor and anatomical wrist are slightly offset. Resolve that
            // offset while keeping the wrist aligned with the forearm and the elbow at its pole.
            for (int iteration = 0; iteration < 4; ++iteration)
            {
                Vector3 delta = wrist - m_Shoulder;
                Vector3 direction = delta.normalized;
                // Only the iterative wrist estimate is projected into reach. The resulting
                // controller pose must still pass Solve against the exact contact position.
                float distance = Mathf.Clamp(delta.magnitude,
                    Mathf.Abs(m_UpperLength - m_ForearmLength) + 0.02f, m_UpperLength + m_ForearmLength - 0.02f);
                wrist = m_Shoulder + direction * distance;
                Vector3 bend = Vector3.ProjectOnPlane(m_ElbowPole, direction).normalized;
                if (bend.sqrMagnitude < 0.001f)
                    throw new InvalidOperationException("Natural grip aligns with the elbow pole.");
                float along = (distance * distance + m_UpperLength * m_UpperLength -
                    m_ForearmLength * m_ForearmLength) / (2 * distance);
                float height = Mathf.Sqrt(Mathf.Max(0, m_UpperLength * m_UpperLength - along * along));
                Vector3 elbow = m_Shoulder + direction * along + bend * height;
                Quaternion forearm = Quaternion.LookRotation(wrist - elbow, m_BodyUp);
                controller = forearm * Quaternion.Inverse(m_GripLocal.rotation);
                wrist = contactPosition + controller * (m_GripLocal.position - contactLocal);
            }
            return controller;
        }

        public Pose Solve(Vector3 controllerPosition, Quaternion controllerRotation)
        {
            Vector3 wrist = controllerPosition + controllerRotation * m_GripLocal.position;
            if (!CanReachWrist(wrist))
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
            Quaternion grip = controllerRotation * m_GripLocal.rotation;
            Vector4 closest = default;
            float bestScore = float.PositiveInfinity;
            float bestComfort = float.PositiveInfinity;
            Pose best = default;
            // Let the shoulder swivel the elbow around its preferred outward/downward pole,
            // instead of forcing the wrist to compensate for one fixed elbow position.
            for (int sample = 0; sample <= 8; ++sample)
            {
                float swivel = sample == 0 ? 0 : ((sample + 1) / 2) * 15 * (sample % 2 == 0 ? 1 : -1);
                Vector3 candidate = Quaternion.AngleAxis(swivel, direction) * bend;
                Vector3 elbow = m_Shoulder + direction * along + candidate * height;
                Quaternion upper = Quaternion.LookRotation(elbow - m_Shoulder, Vector3.Cross(direction, candidate));
                Quaternion neutralForearm = Quaternion.LookRotation(wrist - elbow, m_BodyUp);
                float twist = AxialTwist(Quaternion.Inverse(neutralForearm) * grip);
                Quaternion forearm = neutralForearm * Quaternion.AngleAxis(twist, Vector3.forward);
                Vector4 angles = GripAngles(forearm, controllerRotation);
                float score = ComfortScore(angles);
                float comfort = ComfortCost(angles) + 0.1f * Mathf.Pow(swivel / 60, 2);
                if (score <= 1.0002f && comfort < bestComfort)
                {
                    bestComfort = comfort;
                    best = new Pose {UpperArm = upper, Forearm = Quaternion.Inverse(upper) * forearm,
                        Controller = Quaternion.Inverse(forearm) * controllerRotation};
                }
                if (score < bestScore) { bestScore = score; closest = angles; }
            }
            if (!float.IsPositiveInfinity(bestComfort)) { return best; }
            throw GripError(closest);
        }

        public float Comfort(Pose pose) => ComfortCost(GripAngles(pose.UpperArm * pose.Forearm,
            pose.UpperArm * pose.Forearm * pose.Controller));

        public UnityEngine.Pose ContactPose(UnityEngine.Pose contact, UnityEngine.Pose local, bool freeRoll = false)
        {
            if (!freeRoll)
            {
                Quaternion rotation = contact.rotation * Quaternion.Inverse(local.rotation);
                var fixedPose = new UnityEngine.Pose(contact.position - rotation * local.position, rotation);
                Solve(fixedPose.position, fixedPose.rotation);
                return fixedPose;
            }
            Quaternion relaxed = RelaxedRotation(contact.position, local.position) * local.rotation;
            Vector3 forward = contact.rotation * Vector3.forward;
            Vector3 up = Vector3.ProjectOnPlane(relaxed * Vector3.up, forward);
            Quaternion preferred = up.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, up) : contact.rotation;
            UnityEngine.Pose best = default;
            float bestCost = float.PositiveInfinity;
            Exception failure = null;
            // Ray direction is fixed; roll is a task freedom the arm can use for comfort.
            foreach (float roll in new[] {0f, -15f, 15f, -30f, 30f, -60f, 60f})
            {
                Quaternion rotation = preferred * Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.Inverse(local.rotation);
                var candidate = new UnityEngine.Pose(contact.position - rotation * local.position, rotation);
                try
                {
                    float cost = Comfort(Solve(candidate.position, candidate.rotation)) + 0.02f * Mathf.Pow(roll / 60, 2);
                    if (cost < bestCost) { best = candidate; bestCost = cost; }
                }
                catch (InvalidOperationException error) { failure = error; }
            }
            if (float.IsPositiveInfinity(bestCost))
                throw new InvalidOperationException("Cannot point at the target with a comfortable grip.", failure);
            return best;
        }

        public UnityEngine.Pose Evaluate(Pose pose)
        {
            Quaternion forearm = pose.UpperArm * pose.Forearm;
            Vector3 elbow = m_Shoulder + pose.UpperArm * Vector3.forward * m_UpperLength;
            Quaternion controller = forearm * pose.Controller;
            ValidateGrip(forearm, controller);
            return new UnityEngine.Pose(elbow + forearm * Vector3.forward * m_ForearmLength -
                controller * m_GripLocal.position, controller);
        }

        private static float AxialTwist(Quaternion rotation) =>
            Mathf.DeltaAngle(0, 2 * Mathf.Atan2(rotation.z, rotation.w) * Mathf.Rad2Deg);

        private void ValidateGrip(Quaternion forearm, Quaternion controller)
        {
            Vector4 angles = GripAngles(forearm, controller);
            if (ComfortScore(angles) > 1.0002f) { throw GripError(angles); }
        }

        private Vector4 GripAngles(Quaternion forearm, Quaternion controller)
        {
            Quaternion wrist = Quaternion.Inverse(forearm) * controller * m_GripLocal.rotation;
            Vector3 forward = wrist * Vector3.forward;
            float flexion = Mathf.Atan2(forward.y, forward.z) * Mathf.Rad2Deg;
            float deviation = Mathf.Atan2(forward.x, Mathf.Sqrt(forward.y * forward.y + forward.z * forward.z)) * Mathf.Rad2Deg;
            Quaternion neutral = Quaternion.LookRotation(forearm * Vector3.forward, m_BodyUp);
            float forearmTwist = AxialTwist(Quaternion.Inverse(neutral) * forearm);
            return new Vector4(flexion, deviation, AxialTwist(wrist), forearmTwist);
        }

        private static float ComfortScore(Vector4 angles) => Mathf.Max(
            Mathf.Max(Mathf.Abs(angles.x) / k_MaxWristFlexion, Mathf.Abs(angles.y) / k_MaxWristDeviation),
            Mathf.Max(Mathf.Abs(angles.z) / k_MaxWristTwist, Mathf.Abs(angles.w) / k_MaxForearmTwist));

        private static float ComfortCost(Vector4 angles) =>
            Mathf.Pow(angles.x / k_MaxWristFlexion, 2) + Mathf.Pow(angles.y / k_MaxWristDeviation, 2) +
            Mathf.Pow(angles.z / k_MaxWristTwist, 2) + Mathf.Pow(angles.w / k_MaxForearmTwist, 2);

        private static InvalidOperationException GripError(Vector4 angles) => new InvalidOperationException(
            $"Controller grip exceeds arm comfort limits (wrist flexion {angles.x:F0}°, deviation {angles.y:F0}°, wrist twist {angles.z:F0}°, forearm twist {angles.w:F0}°). Reorient the target or contact pose.");

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
