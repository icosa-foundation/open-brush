// Copyright 2026 The Open Brush Authors

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiltBrush
{
    internal class TestModelImport
    {
        [Test]
        public async Task ParameterlessLoadSharesPendingMaterialization()
        {
            var materializeEntered = NewSource<bool>();
            var releaseMaterialize = NewSource<string>();
            var model = new Model(
                "pending/model.obj",
                "pending-model",
                () =>
                {
                    materializeEntered.TrySetResult(true);
                    return releaseMaterialize.Task.GetAwaiter().GetResult();
                });

            Task first = null;
            try
            {
                first = model.LoadModelAsync();
                Assert.IsTrue(await CompletesWithin(materializeEntered.Task, 30000));

                Task second = model.LoadModelAsync();
                Assert.AreSame(first, second);
                Assert.IsFalse(first.IsCompleted);

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("SAF_MATERIALIZE Could not materialize pending/model.obj"));
                releaseMaterialize.SetResult(null);
                Assert.IsTrue(await CompletesWithin(first, 30000));
            }
            finally
            {
                releaseMaterialize.TrySetResult(null);
                if (first != null)
                {
                    await CompletesWithin(first, 30000);
                }
            }
        }

        private static TaskCompletionSource<T> NewSource<T>()
        {
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task<bool> CompletesWithin(Task task, int timeoutMilliseconds)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeoutMilliseconds));
            return completed == task;
        }

    }
}
