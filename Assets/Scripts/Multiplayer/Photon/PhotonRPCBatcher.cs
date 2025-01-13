// Copyright 2023 The Open Brush Authors
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

#if MP_PHOTON

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;


// A static class for batching Photon RPC calls using async/await.
public static class PhotonRPCBatcher
{

    private static int delayBetweenBatchesMs = 100;
    private static ConcurrentQueue<Action> rpcQueue = new ConcurrentQueue<Action>();
    private static CancellationTokenSource cts = new CancellationTokenSource();
    private static bool isRunning = false;

    // Enqueues an RPC action to be sent.
    public static void EnqueueRPC(Action rpcAction)
    {
        rpcQueue.Enqueue(rpcAction);

        // If not running yet, start the processing loop
        if (!isRunning)
        {
            isRunning = true;
            StartProcessingLoop();
        }
    }

    // Starts the asynchronous loop that processes the RPC queue.
    private static async void StartProcessingLoop()
    {

        while (!cts.Token.IsCancellationRequested)
        {
            if (rpcQueue.TryDequeue(out Action rpcAction)) rpcAction?.Invoke();
            else
            {
                isRunning = false;
                break;
            }

            await Task.Delay(delayBetweenBatchesMs, cts.Token);
        }

        isRunning = false;
    }


    // Stops the processing loop (if needed).
    public static void Stop()
    {
        cts.Cancel();
        cts = new CancellationTokenSource();
    }

}
#endif