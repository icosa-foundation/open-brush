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


#if FUSION_WEAVER

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public class PhotonRPC : SimulationBehaviour
    {
        private static Dictionary<Guid, Stroke> m_inProgressStrokes;
        private static List<PendingCommand> m_pendingCommands;

        public void Awake()
        {
            m_inProgressStrokes = new();
            m_pendingCommands = new();
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


            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command.Command, invoke: false);

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

        private static BaseCommand FindParentCommand(Guid parentGuid)
        {
            PendingCommand pendingParent = m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);

            if (!parentGuid.Equals(default))
            {
                return pendingParent.Command;
            }
            return null;
        }

        public static void CreateBrushStroke(Stroke stroke, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            Action preAction = () =>
            {
                stroke.m_Type = Stroke.Type.NotCreated;
                stroke.m_IntendedCanvas = App.Scene.MainCanvas;
                stroke.Recreate(null, App.Scene.MainCanvas);
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
            };

            var parentCommand = FindParentCommand(parentGuid);

            var command = new BrushStrokeCommand(stroke, parent: parentCommand);

            AddPendingCommand(preAction, commandGuid, parentGuid, command, childCount);
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
        public static void RPC_BaseCommand(NetworkRunner runner, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            Debug.Log($"Base command child count: {childCount}");
            var parentCommand = FindParentCommand(parentGuid);
            var command = new BaseCommand(parent: parentCommand);

            AddPendingCommand(() => {}, commandGuid, parentGuid, command, childCount);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_BrushStrokeFull(NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            var decode = NetworkedStroke.ToStroke(strokeData);

            CreateBrushStroke(decode, commandGuid, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_BrushStrokeBegin(NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength)
        {
            var decode = NetworkedStroke.ToStroke(strokeData);

            decode.m_Type = Stroke.Type.NotCreated;
            decode.m_IntendedCanvas = App.Scene.MainCanvas;
            
            Array.Resize(ref decode.m_ControlPoints, finalLength);
            Array.Resize(ref decode.m_ControlPointsToDrop, finalLength);

            if(m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("Shouldn't be here!");
                return;
            }

            m_inProgressStrokes[id] = decode;
        }
        
        [Rpc(InvokeLocal = false)]
        public static void RPC_BrushStrokeContinue(NetworkRunner runner, Guid id, int offset, NetworkedControlPoint[] controlPoints, bool[] dropPoints)
        {
            if(!m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("shouldn't be here!");
                return;
            }

            var stroke = m_inProgressStrokes[id];
            
            for(int i = 0; i < controlPoints.Length; ++i)
            {
                stroke.m_ControlPoints[offset + i] = NetworkedControlPoint.ToControlPoint(controlPoints[i]);
                stroke.m_ControlPointsToDrop[offset + i] = dropPoints[i];
            }
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_BrushStrokeComplete(NetworkRunner runner, Guid id, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            if(!m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("shouldn't be here!");
                return;
            }

            var stroke = m_inProgressStrokes[id];

            CreateBrushStroke(stroke, commandGuid, parentGuid, childCount);

            m_inProgressStrokes.Remove(id);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_DeleteStroke(NetworkRunner runner, int seed, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            var foundStroke = SketchMemoryScript.m_Instance.GetMemoryList.Where(x => x.m_Seed == seed).First();

            if (foundStroke != null)
            {
                var parentCommand = FindParentCommand(parentGuid);
                var command = new DeleteStrokeCommand(foundStroke, parent: parentCommand);

                AddPendingCommand(() => {}, commandGuid, parentGuid, command, childCount);
            }
            else
            {
                Debug.LogError($"couldn't find stroke with seed: {seed}");
            }
        }
#endregion
    }
}

#endif // FUSION_WEAVER
