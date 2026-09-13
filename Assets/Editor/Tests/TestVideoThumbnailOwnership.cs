// Copyright 2026 The Open Brush Authors
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Video;

namespace TiltBrush
{
    public class TestVideoThumbnailOwnership
    {
        [Test]
        public void ReleasingCatalogThumbnailDoesNotDisposePlaybackControllers()
        {
            var owner = new GameObject("VideoThumbnailOwnershipTest");
            var video = new ReferenceVideo("fixture.mp4", "fixture", null, "fixture.mp4");
            var texture = new Texture2D(1, 1);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var player = owner.AddComponent<VideoPlayer>();
                typeof(ReferenceVideo).GetField("m_VideoPlayer", flags).SetValue(video, player);
                var controllers = (HashSet<ReferenceVideo.Controller>)typeof(ReferenceVideo)
                    .GetField("m_Controllers", flags).GetValue(video);
                controllers.Add(new ReferenceVideo.Controller(video));
                typeof(ReferenceVideo).GetField("<Thumbnail>k__BackingField", flags).SetValue(video, texture);
                video.ReleaseThumbnail();
                Assert.IsNull(video.Thumbnail);
                Assert.IsTrue(video.HasInstances);
                Assert.AreSame(player, typeof(ReferenceVideo).GetField("m_VideoPlayer", flags).GetValue(video));
                video.ReleaseThumbnail();
                Assert.IsTrue(video.HasInstances);
            }
            finally
            {
                video.Dispose();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
