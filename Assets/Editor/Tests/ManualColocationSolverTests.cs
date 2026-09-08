// Copyright 2026 The Open Brush Authors
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

using NUnit.Framework;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    internal class ManualColocationSolverTests : MathTestUtils
    {
        private static Vector3 Meters(float x, float y = 0f, float z = 0f)
        {
            return new Vector3(x, y, z) * App.METERS_TO_UNITS;
        }

        private static ManualColocationReference Reference(
            Vector3 start_SS,
            Vector3 end_SS,
            float scale = 1f)
        {
            return new ManualColocationReference
            {
                IsValid = true,
                Start_SS = start_SS,
                End_SS = end_SS,
                SceneScale = scale,
                Revision = 1,
                CreatorPlayerId = 1,
            };
        }

        [Test]
        public void IdenticalLinesReturnIdentityPose()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Vector3.zero,
                Meters(1f));

            Assert.IsTrue(result.Success);
            AssertAlmostEqual(result.ScenePose, TrTransform.identity);
            Assert.That(result.EndpointResidualMeters, Is.LessThan(1e-5f));
        }

        [Test]
        public void TranslatedLineReturnsTranslationOnly()
        {
            Vector3 offset = Meters(2f, 0.75f, -3f);
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                offset,
                offset + Meters(1f));

            Assert.IsTrue(result.Success);
            AssertAlmostEqual(
                result.ScenePose,
                TrTransform.TRS(offset, Quaternion.identity, 1f));
        }

        [Test]
        public void RotatedLineReturnsUprightYaw()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Vector3.zero,
                Meters(0f, 0f, 1f));

            Assert.IsTrue(result.Success);
            Assert.That(Mathf.Abs(result.YawDegrees + 90f), Is.LessThan(1e-4f));
            AssertAlmostEqual(
                result.ScenePose.rotation * Vector3.up,
                Vector3.up);
            AssertAlmostEqual(
                result.ScenePose.MultiplyPoint(Meters(1f)),
                Meters(0f, 0f, 1f));
        }

        [Test]
        public void OwnerReferenceRoundTripsNonIdentityPose()
        {
            TrTransform ownerPose = TrTransform.TRS(
                Meters(3f, 0.8f, -2f),
                Quaternion.Euler(0f, 37f, 0f),
                2.5f);
            Vector3 start_RS = ownerPose.MultiplyPoint(Meters(-0.5f));
            Vector3 end_RS = ownerPose.MultiplyPoint(Meters(0.5f));

            ManualColocationValidationError error =
                ManualColocationSolver.TryCreateReference(
                    ownerPose,
                    start_RS,
                    end_RS,
                    7,
                    23,
                    out ManualColocationReference reference);
            ManualColocationSolveResult result =
                ManualColocationSolver.TrySolve(reference, start_RS, end_RS);

            Assert.AreEqual(ManualColocationValidationError.None, error);
            Assert.IsTrue(result.Success);
            AssertAlmostEqual(reference.Start_SS, Meters(-0.5f));
            AssertAlmostEqual(reference.End_SS, Meters(0.5f));
            AssertAlmostEqual(result.ScenePose, ownerPose);
        }

        [Test]
        public void ParticipantLengthMismatchDoesNotChangeOwnerScale()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f), 2f),
                Vector3.zero,
                Meters(2.25f));

            Assert.IsTrue(result.Success);
            Assert.That(result.ScenePose.scale, Is.EqualTo(2f));
            Assert.That(result.ReferenceLengthMeters, Is.EqualTo(2f));
            Assert.That(result.LocalLengthMeters, Is.EqualTo(2.25f));
            Assert.That(result.LengthMismatchMeters, Is.EqualTo(0.25f));
            Assert.That(
                result.Warnings.HasFlag(ManualColocationWarning.LengthMismatch),
                Is.True);
        }

        [Test]
        public void ReversedEndpointsProduceWarning()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Meters(1f),
                Vector3.zero);

            Assert.IsTrue(result.Success);
            Assert.That(Mathf.Abs(result.YawDegrees), Is.GreaterThan(179f));
            Assert.That(
                result.Warnings.HasFlag(
                    ManualColocationWarning.PossibleEndpointReversal),
                Is.True);
        }

        [Test]
        public void ShortLineIsRejected()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Vector3.zero,
                Meters(0.01f));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ManualColocationValidationError.LineTooShort, result.Error);
        }

        [Test]
        public void NearlyVerticalLineIsRejected()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Vector3.zero,
                Meters(0.01f, 1f));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(
                ManualColocationValidationError.InsufficientHorizontalSpan,
                result.Error);
        }

        [Test]
        public void SubHalfMeterHorizontalLineIsRejected()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Vector3.zero,
                Meters(0.4f));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(
                ManualColocationValidationError.InsufficientHorizontalSpan,
                result.Error);
        }

        [Test]
        public void NonFiniteInputIsRejected()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                new Vector3(float.NaN, 0f, 0f),
                Meters(1f));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(
                ManualColocationValidationError.NonFiniteInput,
                result.Error);
        }

        [Test]
        public void InvalidReferenceScaleIsRejected()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f), 0f),
                Vector3.zero,
                Meters(1f));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(
                ManualColocationValidationError.InvalidScale,
                result.Error);
        }

        [Test]
        public void MidpointMapsExactlyWhenLengthsDiffer()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(
                    Meters(-0.5f),
                    Meters(0.5f)),
                Meters(2f, 1f, -1f),
                Meters(3.2f, 1f, -1f));

            Assert.IsTrue(result.Success);
            Vector3 referenceMidpoint_RS = result.ScenePose.MultiplyPoint(Vector3.zero);
            Vector3 localMidpoint_RS = Meters(2.6f, 1f, -1f);
            AssertAlmostEqual(referenceMidpoint_RS, localMidpoint_RS);
        }

        [Test]
        public void HeightDifferenceProducesWarningButUprightPose()
        {
            ManualColocationSolveResult result = ManualColocationSolver.TrySolve(
                Reference(Vector3.zero, Meters(1f)),
                Vector3.zero,
                Meters(1f, 0.1f));

            Assert.IsTrue(result.Success);
            Assert.That(
                result.Warnings.HasFlag(ManualColocationWarning.HeightMismatch),
                Is.True);
            AssertAlmostEqual(
                result.ScenePose.rotation * Vector3.up,
                Vector3.up);
        }
    }
}
