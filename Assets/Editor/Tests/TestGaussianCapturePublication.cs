using NUnit.Framework;

namespace TiltBrush
{
    public class TestGaussianCapturePublication
    {
        [TestCase(true, false, true, true)]
        [TestCase(false, false, true, false)]
        [TestCase(true, true, true, false)]
        [TestCase(true, false, false, false)]
        public void OnlyCompletedSharedStorageCapturesArePublished(
            bool completed, bool canceled, bool sharedStorage, bool expected)
        {
            Assert.AreEqual(expected, CameraCaptureRuntime.ShouldPublishGaussianCapture(
                completed, canceled, sharedStorage));
        }
    }
}
