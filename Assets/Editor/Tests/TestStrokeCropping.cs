using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestStrokeCropping
    {
        private static StrokeCropVolume Volume(int shape) => new(
            (StrokeCropVolume.Shape)shape, TrTransform.identity, Vector3.one);

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void OutsideEndpointsCrossVolume(int shape)
        {
            Assert.IsTrue(Volume(shape).ClipSegment(new Vector3(-2, 0, 0),
                new Vector3(2, 0, 0), out float enter, out float exit));
            Assert.That(enter, Is.EqualTo(0.25f).Within(0.00001));
            Assert.That(exit, Is.EqualTo(0.75f).Within(0.00001));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SegmentEntirelyOutsideIsRejected(int shape)
        {
            Assert.IsFalse(Volume(shape).ClipSegment(new Vector3(2, 0, 0),
                new Vector3(3, 0, 0), out _, out _));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void StationaryPointInsideIsKept(int shape)
        {
            Assert.IsTrue(Volume(shape).ClipSegment(Vector3.zero, Vector3.zero,
                out float enter, out float exit));
            Assert.AreEqual(0, enter);
            Assert.AreEqual(1, exit);
        }

        [Test]
        public void CapsuleClipsBothCaps()
        {
            Assert.IsTrue(Volume(2).ClipSegment(new Vector3(0, -2, 0),
                new Vector3(0, 2, 0), out float enter, out float exit));
            Assert.AreEqual(0.25f, enter);
            Assert.AreEqual(0.75f, exit);
            Assert.IsFalse(Volume(2).ClipSegment(new Vector3(2, -2, 0),
                new Vector3(2, 2, 0), out _, out _));
        }

        [Test]
        public void CapsuleRejectsBoxCorner()
        {
            Assert.IsTrue(Volume(1).Contains(new Vector3(0.9f, 0, 0.9f)));
            Assert.IsFalse(Volume(2).Contains(new Vector3(0.9f, 0, 0.9f)));
        }

        [Test]
        public void CapsuleIncludesRoundedEndsAndBody()
        {
            var volume = new StrokeCropVolume(StrokeCropVolume.Shape.Capsule,
                TrTransform.identity, new Vector3(1, 3, 1));
            Assert.IsTrue(volume.ClipSegment(new Vector3(0, -4, 0), new Vector3(0, 4, 0),
                out float enter, out float exit));
            Assert.AreEqual(0.125f, enter);
            Assert.AreEqual(0.875f, exit);
            Assert.IsTrue(volume.Contains(new Vector3(0.5f, 2.5f, 0)));
            Assert.IsFalse(volume.Contains(new Vector3(1, 2.5f, 0)));
        }

        [Test]
        public void EllipsoidUsesEachAxisSize()
        {
            var volume = new StrokeCropVolume(StrokeCropVolume.Shape.Ellipsoid,
                TrTransform.identity, new Vector3(1, 2, 3));
            Assert.IsTrue(volume.ClipSegment(new Vector3(0, 0, -6), new Vector3(0, 0, 6),
                out float enter, out float exit));
            Assert.AreEqual(0.25f, enter);
            Assert.AreEqual(0.75f, exit);
            Assert.IsTrue(volume.Contains(new Vector3(0, 1.5f, 0)));
            Assert.IsFalse(volume.Contains(new Vector3(1.5f, 0, 0)));
        }

        [Test]
        public void PlaneKeepsNormalSideAndBoundary()
        {
            var volume = Volume(4);
            Assert.IsTrue(volume.ClipSegment(Vector3.down, Vector3.up, out float enter, out float exit));
            Assert.AreEqual(0.5f, enter);
            Assert.AreEqual(1, exit);
            Assert.IsTrue(volume.ClipSegment(Vector3.up, Vector3.down, out enter, out exit));
            Assert.AreEqual(0, enter);
            Assert.AreEqual(0.5f, exit);
            Assert.IsFalse(volume.ClipSegment(Vector3.down, new Vector3(1, -1, 0), out _, out _));
            Assert.IsTrue(volume.ClipSegment(Vector3.zero, Vector3.right, out _, out _));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void LeavingAndReenteringSplitsStroke(int shape)
        {
            var segments = StrokeCropping.ClipStrokeToVolume(new[] {
                Point(Vector3.zero, 0), Point(new Vector3(2, 0, 0), 100),
                Point(Vector3.zero, 200)
            }, Volume(shape), TrTransform.identity);
            Assert.AreEqual(2, segments.Count);
            Assert.AreEqual(new Vector3(1, 0, 0), segments[0][1].m_Pos);
            Assert.AreEqual(50, segments[0][1].m_TimestampMs);
            Assert.AreEqual(150, segments[1][0].m_TimestampMs);
        }

        [Test]
        public void RotatedTranslatedScaledCanvasClipsInVolumeSpace()
        {
            var toVolume = TrTransform.TRS(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0), 2);
            var fromVolume = toVolume.inverse;
            var segments = StrokeCropping.ClipStrokeToVolume(new[] {
                Point(fromVolume * new Vector3(-2, 0, 0), 0),
                Point(fromVolume * new Vector3(2, 0, 0), 100)
            }, Volume(1), toVolume);
            Assert.AreEqual(1, segments.Count);
            Assert.That(Vector3.Distance(toVolume * segments[0][0].m_Pos, Vector3.left), Is.LessThan(0.00001));
            Assert.That(Vector3.Distance(toVolume * segments[0][1].m_Pos, Vector3.right), Is.LessThan(0.00001));
        }

        private static PointerManager.ControlPoint Point(Vector3 p, uint time) => new()
        {
            m_Pos = p, m_Orient = Quaternion.identity, m_Pressure = 1, m_TimestampMs = time
        };
    }
}
