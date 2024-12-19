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
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TiltBrush;
using static TiltBrush.SketchControlsScript;
using System.Threading.Tasks;

namespace OpenBrush.Multiplayer
{
    public class PhotonRPC : SimulationBehaviour
    {
        private static Dictionary<Guid, Stroke> m_inProgressStrokes;
        private static List<PendingCommand> m_pendingCommands;
        private static Dictionary<Guid, TaskCompletionSource<bool>> m_acknowledgments;

        public void Awake()
        {
            m_inProgressStrokes = new();
            m_pendingCommands = new();
            m_acknowledgments = new();
        }

        public void Update()
        {
            TryProcessCommands();
        }

        private bool CheckifChildStillPending(PendingCommand pending)
        {
            if (pending.TotalExpectedChildren == pending.Command.ChildrenCount)
            {
                bool moreChildrenToAssign = false;

                foreach (var childCommand in pending.Command.Children)
                {
                    // has a child present in the pending queue, check them too
                    var childPending = m_pendingCommands.FirstOrDefault(x => x.Guid == childCommand.Guid);

                    if (!childPending.Guid.Equals(default))
                    {
                        var childIsStillPending = CheckifChildStillPending(childPending);

                        if (!childIsStillPending)
                        {
                            m_pendingCommands.Remove(childPending);
                        }

                        moreChildrenToAssign |= childIsStillPending;
                    }
                }

                return moreChildrenToAssign;
            }

            else
            {
                return true;
            }
        }

        private void InvokePreCommands(PendingCommand pendingCommand)
        {
            pendingCommand.PreCommandAction.Invoke();

            foreach (var childCommand in pendingCommand.ChildCommands)
            {
                InvokePreCommands(childCommand);
            }
        }

        private void TryProcessCommands()
        {
            if (m_pendingCommands.Count == 0)
            {
                return;
            }

            var command = m_pendingCommands[0];

            bool stillPending = CheckifChildStillPending(command);

            if (stillPending)
            {
                return;
            }

            // All children present, begin execution

            m_pendingCommands.RemoveAt(0);

            InvokePreCommands(command);

            //SketchMemoryScript.m_Instance.PerformAndRecordCommand(command.Command, invoke: false);
            SketchMemoryScript.m_Instance.PerformAndRecordNetworkCommand(command.Command);

            TryProcessCommands();
        }

        private static void AddPendingCommand(Action preAction, Guid commandGuid, Guid parentGuid, BaseCommand command, int childCount)
        {

            PendingCommand pendingCommand = new PendingCommand(commandGuid, command, preAction, childCount);

            if (!parentGuid.Equals(default))
            {
                var pendingParent = m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);
                pendingParent.ChildCommands.Add(pendingCommand);
            }

            m_pendingCommands.Add(pendingCommand);
        }

        private static bool CheckifCommandGuidIsInStack(Guid commandGuid)
        {

            if (SketchMemoryScript.m_Instance.IsCommandInStack(commandGuid))
            {
                //Debug.Log($"Command with Guid {commandGuid} already in stack.");
                return true;
            }
            return false;
        }

        private static bool CheckifStrokeGuidIsInMemory(Guid strokeGuid)
        {

            if (SketchMemoryScript.m_Instance.IsStrokeInMemory(strokeGuid))
            {
                //Debug.Log($"Stroke with Guid {strokeGuid} already in memory.");
                return true;
            }
            return false;
        }

        private static BaseCommand FindParentCommand(Guid parentGuid)
        {
            PendingCommand pendingParent = m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);

            if (!parentGuid.Equals(default))
            {
                return pendingParent.Command;
            }
            return null;
        }

        public static void Send_BaseCommand(NetworkRunner runner, Guid commandGuid, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BaseCommand(runner, commandGuid, parentGuid, childCount);
            }
            else
            {
                RPC_BaseCommand(runner, commandGuid, parentGuid, childCount, targetPlayer);
            }
        }

        private static void BaseCommand(Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            Debug.Log($"Base command child count: {childCount}");
            var parentCommand = FindParentCommand(parentGuid);
            var command = new BaseCommand(parent: parentCommand);

            AddPendingCommand(() => { }, commandGuid, parentGuid, command, childCount);
        }

        public static void Send_BrushStrokeFull(NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeFull(runner, strokeData, commandGuid, timestamp, parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeFull(runner, strokeData, commandGuid, timestamp, parentGuid, childCount, targetPlayer);
            }
        }

        private static void BrushStrokeFull(NetworkedStroke strokeData, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {

            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            var decode = NetworkedStroke.ToStroke(strokeData);

            CreateBrushStroke(decode, commandGuid, timestamp, parentGuid, childCount);
        }

        public static void Send_BrushStrokeBegin(NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength, [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeBegin(runner, id, strokeData, finalLength);
            }
            else
            {
                RPC_BrushStrokeBegin(runner, id, strokeData, finalLength, targetPlayer);
            }
        }

        private static void BrushStrokeBegin(Guid id, NetworkedStroke strokeData, int finalLength)
        {
            var decode = NetworkedStroke.ToStroke(strokeData);

            decode.m_Type = Stroke.Type.NotCreated;
            decode.m_IntendedCanvas = App.Scene.MainCanvas;

            Array.Resize(ref decode.m_ControlPoints, finalLength);
            Array.Resize(ref decode.m_ControlPointsToDrop, finalLength);

            if (m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("Shouldn't be here!");
                return;
            }

            m_inProgressStrokes[id] = decode;
        }

        public static void Send_BrushStrokeContinue(NetworkRunner runner, Guid id, int offset, NetworkedControlPoint[] controlPoints, bool[] dropPoints, [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeContinue(runner, id, offset, controlPoints, dropPoints);
            }
            else
            {
                RPC_BrushStrokeContinue(runner, id, offset, controlPoints, dropPoints, targetPlayer);
            }
        }

        private static void BrushStrokeContinue(Guid id, int offset, NetworkedControlPoint[] controlPoints, bool[] dropPoints)
        {
            if (!m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("shouldn't be here!");
                return;
            }

            var stroke = m_inProgressStrokes[id];

            for (int i = 0; i < controlPoints.Length; ++i)
            {
                stroke.m_ControlPoints[offset + i] = NetworkedControlPoint.ToControlPoint(controlPoints[i]);
                stroke.m_ControlPointsToDrop[offset + i] = dropPoints[i];
            }
        }

        public static void Send_BrushStrokeComplete(NetworkRunner runner, Guid id, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeComplete(runner, id, commandGuid, timestamp, parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeComplete(runner, id, commandGuid, timestamp, parentGuid, childCount, targetPlayer);
            }
        }

        private static void BrushStrokeComplete(Guid id, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {

            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            if (!m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("shouldn't be here!");
                return;
            }

            var stroke = m_inProgressStrokes[id];

            CreateBrushStroke(stroke, commandGuid, timestamp, parentGuid, childCount);

            m_inProgressStrokes.Remove(id);
        }

        private static void CreateBrushStroke(Stroke stroke, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {

            Action preAction = () =>
            {
                stroke.m_Type = Stroke.Type.NotCreated;
                stroke.m_IntendedCanvas = App.Scene.MainCanvas;
                stroke.Recreate(null, App.Scene.MainCanvas);
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
            };

            var parentCommand = FindParentCommand(parentGuid);

            var command = new BrushStrokeCommand(stroke, commandGuid, timestamp, parent: parentCommand);

            AddPendingCommand(preAction, commandGuid, parentGuid, command, childCount);
        }

        public static void Send_DeleteStroke(NetworkRunner runner, int seed, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_DeleteStroke(runner, seed, commandGuid, timestamp, parentGuid, childCount);
            }
            else
            {
                RPC_DeleteStroke(runner, seed, commandGuid, timestamp, parentGuid, childCount, targetPlayer);
            }
        }

        private static void DeleteStroke(int seed, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            // TODO : implment GUID for strokesdata.
            // The range of int is large (-2,147,483,648 to 2,147,483,647), but collisions are still possible.
            var foundStroke = SketchMemoryScript.m_Instance.GetMemoryList.Where(x => x.m_Seed == seed).First();

            if (foundStroke != null)
            {
                var parentCommand = FindParentCommand(parentGuid);
                var command = new DeleteStrokeCommand(foundStroke, commandGuid, timestamp, parent: parentCommand);

                AddPendingCommand(() => { }, commandGuid, parentGuid, command, childCount);
            }
            else
            {
                Debug.LogError($"couldn't find stroke with seed: {seed}");
            }
        }

        public static void Send_SwitchEnvironment(NetworkRunner runner, Guid environmentGuid, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {

            if (targetPlayer == default)
            {
                RPC_SwitchEnvironment(runner, environmentGuid, commandGuid, timestamp, parentGuid, childCount);
            }
            else
            {
                RPC_SwitchEnvironment(runner, environmentGuid, commandGuid, timestamp, parentGuid, childCount, targetPlayer);
            }
        }

        private static void SwitchEnvironment(Guid environmentGuid, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            TiltBrush.Environment environment = EnvironmentCatalog.m_Instance.GetEnvironment(environmentGuid);

            if (environment != null)
            {

                var parentCommand = FindParentCommand(parentGuid);
                var command = new SwitchEnvironmentCommand(environment, commandGuid, timestamp, parent: parentCommand);

                AddPendingCommand(() => { }, commandGuid, parentGuid, command, childCount);
            }
            else
            {
                Debug.LogError($"Environment with Guid {environmentGuid} not found.");
            }
        }

        public static async Task<bool> WaitForAcknowledgment(Guid commandGuid, int timeoutMilliseconds = 1000)
        {
            var tcs = new TaskCompletionSource<bool>();
            m_acknowledgments[commandGuid] = tcs;

            var timeoutTask = Task.Delay(timeoutMilliseconds);
            var acknowledgmentTask = tcs.Task;
            var completedTask = await Task.WhenAny(acknowledgmentTask, timeoutTask);

            if (completedTask == acknowledgmentTask)
            {
                m_acknowledgments.Remove(commandGuid);
                return await acknowledgmentTask;
            }
            else
            {
                m_acknowledgments.Remove(commandGuid);
                return false;
            }
        }

        #region RPCS
        [Rpc(InvokeLocal = false)]
        public static void RPC_SyncToSharedAnchor(NetworkRunner runner, string uuid)
        {
#if OCULUS_SUPPORTED
            OculusMRController.m_Instance.RemoteSyncToAnchor(uuid);
#endif // OCULUS_SUPPORTED
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_PerformCommand(NetworkRunner runner, string commandName, string guid, string[] data)
        {
            Debug.Log($"Command recieved: {commandName}");

            if (commandName.Equals("TiltBrush.BrushStrokeCommand"))
            {
                var asString = string.Join(string.Empty, data);
                Debug.Log(asString);
                var decode = JsonUtility.FromJson<Stroke>(asString);

                // Temp
                decode.m_BrushGuid = new System.Guid(guid);

                // Can we set up these more sensibly?
                decode.m_Type = Stroke.Type.NotCreated;
                decode.m_IntendedCanvas = App.Scene.MainCanvas;

                // Setup data that couldn't be transferred
                decode.Recreate(null, App.Scene.MainCanvas);
                SketchMemoryScript.m_Instance.MemoryListAdd(decode);

                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new BrushStrokeCommand(decode), invoke: false);
            }
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_Undo(NetworkRunner runner, string commandName)
        {
            if (SketchMemoryScript.m_Instance.CanUndo())
            {
                SketchMemoryScript.m_Instance.StepBack(false);
            }
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_Redo(NetworkRunner runner, string commandName)
        {
            if (SketchMemoryScript.m_Instance.CanRedo())
            {
                SketchMemoryScript.m_Instance.StepForward(false);
            }
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BaseCommand(NetworkRunner runner, Guid commandGuid, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            BaseCommand(commandGuid, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BaseCommand(NetworkRunner runner, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            BaseCommand(commandGuid, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFull(NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeFull(strokeData, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFull(NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeFull(strokeData, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeBegin(NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength, [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeBegin(id, strokeData, finalLength);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeBegin(NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength)
        {
            BrushStrokeBegin(id, strokeData, finalLength);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeContinue(NetworkRunner runner, Guid id, int offset, NetworkedControlPoint[] controlPoints, bool[] dropPoints, [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeContinue(id, offset, controlPoints, dropPoints);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeContinue(NetworkRunner runner, Guid id, int offset, NetworkedControlPoint[] controlPoints, bool[] dropPoints)
        {
            BrushStrokeContinue(id, offset, controlPoints, dropPoints);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeComplete(NetworkRunner runner, Guid id, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeComplete(id, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeComplete(NetworkRunner runner, Guid id, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeComplete(id, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_DeleteStroke(NetworkRunner runner, int seed, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            DeleteStroke(seed, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_DeleteStroke(NetworkRunner runner, int seed, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {
            DeleteStroke(seed, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_SwitchEnvironment(NetworkRunner runner, Guid environmentGuid, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0, [RpcTarget] PlayerRef targetPlayer = default)
        {
            SwitchEnvironment(environmentGuid, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_SwitchEnvironment(NetworkRunner runner, Guid environmentGuid, Guid commandGuid, int timestamp, Guid parentGuid = default, int childCount = 0)
        {
            SwitchEnvironment(environmentGuid, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_StartHistorySync(NetworkRunner runner, [RpcTarget] PlayerRef targetPlayer)
        {
            m_Instance.IssueGlobalCommand(GlobalCommands.DisplaySynchInfo);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_HistoryPercentageUpdate(NetworkRunner runner, [RpcTarget] PlayerRef targetPlayer, int expected, int sent)
        {
            MultiplayerSceneSync.m_Instance.numberOfCommandsExpected = expected;
            MultiplayerSceneSync.m_Instance.numberOfCommandsSent = sent;
            m_Instance.IssueGlobalCommand(GlobalCommands.SynchInfoPercentageUpdate);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_HistorySyncCompleted(NetworkRunner runner, [RpcTarget] PlayerRef targetPlayer)
        {
            m_Instance.IssueGlobalCommand(GlobalCommands.HideSynchInfo);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_CheckCommand(NetworkRunner runner, Guid commandGuid, PlayerRef initiatorPlayer, [RpcTarget] PlayerRef targetPlayer)
        {
            bool isCommandInStack = CheckifCommandGuidIsInStack(commandGuid);
            RPC_Confirm(runner, commandGuid, isCommandInStack, initiatorPlayer);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_CheckStroke(NetworkRunner runner, Guid strokeGuid, PlayerRef initiatorPlayer, [RpcTarget] PlayerRef targetPlayer)
        {
            bool isCommandInStack = CheckifStrokeGuidIsInMemory(strokeGuid);
            RPC_Confirm(runner, strokeGuid, isCommandInStack, initiatorPlayer);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_Confirm(NetworkRunner runner, Guid commandGuid, bool isCommandInStack, [RpcTarget] PlayerRef targetPlayer)
        {
            if (m_acknowledgments.TryGetValue(commandGuid, out var tcs))
            {
                tcs.SetResult(isCommandInStack);
                m_acknowledgments.Remove(commandGuid);
            }
        }

        #endregion
    }
}

#endif // FUSION_WEAVER
