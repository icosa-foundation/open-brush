// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class ProceduralArmTests
    {
        private static ProceduralArm Arm(float side, out Pose grip)
        {
            grip = new Pose(new Vector3(side * 0.006f, -0.014f, 0.008f),
                Quaternion.Euler(1.6f, -side * 6.65f, side * 2.57f));
            return new ProceduralArm(new Vector3(side * 0.18f, 1.45f, -0.19f),
                new Vector3(side * 0.6f, -1, -0.15f), 0.30f, 0.30f, grip, Vector3.up);
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void RelaxedGripPreservesContactWithNeutralWrist(float side)
        {
            var arm = Arm(side, out Pose grip);
            var contact = new Vector3(side * 0.12f, 1.30f, 0.26f);
            var anchor = new Vector3(0, -0.023f, -0.038f);
            Pose result = arm.RelaxedGripPose(contact, anchor);
            var solved = arm.Solve(result.position, result.rotation);

            Assert.Less(Vector3.Distance(contact, result.position + result.rotation * anchor), 0.00001f);
            Assert.Less(Quaternion.Angle(solved.UpperArm * solved.Forearm, result.rotation * grip.rotation), 0.1f);
            Assert.Less(Vector3.Distance(arm.Evaluate(solved).position, result.position), 0.00001f);
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void TaskConstraintsPreserveContactOrientationAndPointingRay(float side)
        {
            var arm = Arm(side, out _);
            var local = new Pose(new Vector3(0, -0.02f, 0.06f), Quaternion.Euler(84.43f, 0, 0));
            var contact = new Vector3(side * 0.12f, 1.30f, 0.26f);
            Pose relaxed = arm.RelaxedGripPose(contact, local.position);
            Quaternion required = Quaternion.AngleAxis(8, Vector3.up) * relaxed.rotation * local.rotation;
            Pose plane = arm.ContactPose(new Pose(contact, required), local);

            Assert.Less(Quaternion.Angle(plane.rotation * local.rotation, required), 0.01f);
            Assert.Less(Vector3.Distance(plane.position + plane.rotation * local.position, contact), 0.00001f);

            Vector3 direction = required * Vector3.forward;
            Pose ray = arm.ContactPose(new Pose(contact, Quaternion.LookRotation(direction, Vector3.up)), local, true);
            Assert.Less(Vector3.Angle(ray.rotation * local.rotation * Vector3.forward, direction), 0.01f);
            Assert.Less(Vector3.Distance(ray.position + ray.rotation * local.position, contact), 0.00001f);
        }

        [Test]
        public void RelaxedGripRejectsUnreachableContact()
        {
            var arm = Arm(1, out _);
            Assert.Throws<InvalidOperationException>(() =>
                arm.RelaxedGripPose(new Vector3(2, 1.3f, 2), new Vector3(0, -0.023f, -0.038f)));
        }
    }
}
