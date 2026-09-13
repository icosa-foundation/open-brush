// Copyright 2026 The Open Brush Authors

using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestModelRestoreGate
    {
        [Test]
        public async Task DelayedSuccessCallsCallbackAndReturnsTrue()
        {
            var gate = new ModelRestoreGate();
            var release = NewSource();
            int callbacks = 0;
            Task<bool> attempt = gate.RunAsync("model", async current =>
            {
                Assert.IsTrue(current());
                return await release.Task;
            }, () => ++callbacks, () => true);

            Assert.IsFalse(attempt.IsCompleted);
            release.SetResult(true);
            Assert.IsTrue(await attempt);
            Assert.AreEqual(1, callbacks);
        }

        [Test]
        public async Task FalseResultDoesNotCallCallbackAndKeyCanRetry()
        {
            var gate = new ModelRestoreGate();
            int callbacks = 0;
            Assert.IsFalse(await gate.RunAsync("model", _ => Task.FromResult(false),
                () => ++callbacks, () => true));
            Assert.AreEqual(0, callbacks);
            Assert.IsTrue(await gate.RunAsync("model", _ => Task.FromResult(true),
                () => ++callbacks, () => true));
            Assert.AreEqual(1, callbacks);
        }

        [Test]
        public async Task DuplicateKeyIsSuppressedWithinGeneration()
        {
            var gate = new ModelRestoreGate();
            var release = NewSource();
            int restores = 0;
            Task<bool> first = gate.RunAsync("model", async _ =>
            {
                ++restores;
                return await release.Task;
            }, () => { }, () => true);
            Assert.IsFalse(await gate.RunAsync("model", _ =>
                Task.FromResult(true), () => { }, () => true));
            release.SetResult(true);
            Assert.IsTrue(await first);
            Assert.AreEqual(1, restores);
        }

        [Test]
        public async Task OldGenerationCannotReleaseNewAttempt()
        {
            var gate = new ModelRestoreGate();
            var oldRelease = NewSource();
            var newRelease = NewSource();
            Task<bool> oldAttempt = gate.RunAsync("model", async _ => await oldRelease.Task,
                () => { }, () => true);
            gate.Invalidate();
            Task<bool> newAttempt = gate.RunAsync("model", async _ => await newRelease.Task,
                () => { }, () => true);
            oldRelease.SetResult(true);
            Assert.IsFalse(await oldAttempt);
            Assert.IsFalse(newAttempt.IsCompleted);
            Assert.IsFalse(await gate.RunAsync("model", _ => Task.FromResult(true),
                () => Assert.Fail("Old completion released the new attempt's key"), () => true));
            newRelease.SetResult(true);
            Assert.IsTrue(await newAttempt);
        }

        [Test]
        public async Task SourceChangePreventsRestoreAndSuccess()
        {
            var gate = new ModelRestoreGate();
            bool sourceCurrent = false;
            int restores = 0;
            Assert.IsFalse(await gate.RunAsync("model", _ =>
            {
                ++restores;
                return Task.FromResult(true);
            }, () => { }, () => sourceCurrent));
            Assert.AreEqual(0, restores);

            sourceCurrent = true;
            int callbacks = 0;
            var release = NewSource();
            Task<bool> attempt = gate.RunAsync("model", async _ => await release.Task,
                () => ++callbacks, () => sourceCurrent);
            sourceCurrent = false;
            release.SetResult(true);
            Assert.IsFalse(await attempt);
            Assert.AreEqual(0, callbacks);
        }

        [Test]
        public void ExceptionReleasesKeyForRecovery()
        {
            var gate = new ModelRestoreGate();
            Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync("model",
                _ => throw new InvalidOperationException("restore failed"), () => { }, () => true));
            Assert.DoesNotThrowAsync(async () => Assert.IsTrue(await gate.RunAsync("model",
                _ => Task.FromResult(true), () => { }, () => true)));
        }

        private static TaskCompletionSource<bool> NewSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
