// Copyright 2026 The Open Brush Authors
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TiltBrush
{
    public class TestCatalogChangeQueue
    {
        [Test]
        public void PreservesMultipleChangesAndSeparatesBatches()
        {
            var queue = new CatalogChangeQueue();
            queue.Add("first"); queue.Add("second"); queue.Add("first");
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, queue.Drain());
            Assert.IsEmpty(queue.Drain());
            queue.Add("next");
            CollectionAssert.AreEqual(new[] { "next" }, queue.Drain());
            queue.Add("old-directory"); queue.Clear();
            Assert.IsEmpty(queue.Drain());
        }

        [Test]
        public async Task ConcurrentProducerAndBatchDrainDoNotLoseChanges()
        {
            var queue = new CatalogChangeQueue();
            var collected = new List<string>();
            Task producer = Task.Run(() =>
            {
                for (int i = 0; i < 2000; ++i) { queue.Add($"change-{i}"); }
            });
            while (!producer.IsCompleted)
            {
                collected.AddRange(queue.Drain());
                await Task.Yield();
            }
            await producer;
            collected.AddRange(queue.Drain());
            Assert.AreEqual(2000, collected.Count);
            Assert.AreEqual(2000, collected.Distinct().Count());
        }
    }
}
