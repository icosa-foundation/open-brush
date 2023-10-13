using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TiltBrush;
using System.Linq;

namespace OpenBrush.Multiplayer
{
    public class PhotonPlayerRig : NetworkBehaviour, ITransientData<PlayerRigData>
    {
        // Only used for transferring data - don't actually use these transforms without offsetting
        public NetworkTransform m_PlayArea;
        public NetworkTransform m_PlayerHead;
        public NetworkTransform m_Left;
        public NetworkTransform m_Right;
        public NetworkTransform m_Tool;



        [Networked] private Color brushColor { get; set; }
        [Networked] private float brushSize { get; set; }
        [Networked] private NetworkString<_64> brushGuid { get; set; }
        [Networked] public ulong oculusPlayerId { get; set; }

        PointerScript transientPointer;
        // The offset transforms.
        [SerializeField] private Transform headTransform;
        private PlayerRigData transmitData;

        private Dictionary <Guid, Stroke> m_inProgressStrokes;
        private List<PendingCommand> m_pendingCommands;

        public void TransmitData(PlayerRigData data)
        {
            transmitData = data;
            oculusPlayerId = data.ExtraData.OculusPlayerId;

            brushColor = data.BrushData.Color;
            brushSize = data.BrushData.Size;
            brushGuid = data.BrushData.Guid;
        }

        public PlayerRigData RecieveData()
        {
            var data = new PlayerRigData
            {
                HeadPosition = m_PlayerHead.InterpolationTarget.position,
                HeadRotation = m_PlayerHead.InterpolationTarget.rotation,
                ExtraData = new ExtraData
                {
                    OculusPlayerId = this.oculusPlayerId
                }
            };
            return data;
        }

        public override void Spawned()
        {
            base.Spawned();

            brushGuid = BrushCatalog.m_Instance.DefaultBrush.m_Guid.ToString();

            if(!Object.HasStateAuthority)
            {
                transientPointer = PointerManager.m_Instance.CreateRemotePointer();
                transientPointer.SetBrush(BrushCatalog.m_Instance.DefaultBrush);
                transientPointer.SetColor(App.BrushColor.CurrentColor);

                m_inProgressStrokes = new Dictionary<Guid, Stroke>();
                m_pendingCommands = new List<PendingCommand>();
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            if(Object.HasStateAuthority)
            {
                m_PlayerHead.transform.position = transmitData.HeadPosition;
                m_PlayerHead.transform.rotation = transmitData.HeadRotation;

                m_Tool.transform.position = transmitData.ToolPosition;
                m_Tool.transform.rotation = transmitData.ToolRotation;
            }

            else
            {
                TryProcessCommands();


                var toolTR = TrTransform.TR(m_Tool.InterpolationTarget.position, m_Tool.InterpolationTarget.rotation);
                App.Scene.AsScene[transientPointer.transform] = toolTR;

                transientPointer.SetColor(brushColor);
                if(brushGuid.ToString() != string.Empty)
                {
                    transientPointer.SetBrush(BrushCatalog.m_Instance.GetBrush(new System.Guid(brushGuid.ToString())));
                }
                transientPointer.BrushSize01 = brushSize;
            }

            var remoteTR = TrTransform.TR(m_PlayerHead.InterpolationTarget.position, m_PlayerHead.InterpolationTarget.rotation);
            App.Scene.AsScene[headTransform] = remoteTR;
        }

        private bool CheckifChildStillPending(PendingCommand pending)
        {   
            Debug.Log(pending.Command.GetType());
            if (pending.TotalExpectedChildren == pending.Command.ChildCount)
            {
                bool moreChildrenToAssign = false;

                foreach (var childCommand in pending.Command.m_Children)
                {
                    // has a child present in the pending queue, check them too
                    var childPending = m_pendingCommands.FirstOrDefault(x => x.Guid == childCommand.m_Guid);

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
            Debug.Log($"Queue length: {m_pendingCommands.Count}");

            var command = m_pendingCommands[0];

            bool stillPending = CheckifChildStillPending(command);

            if (stillPending)
            {
                return;
            }

            // All children present, begin execution
            
            m_pendingCommands.RemoveAt(0);

            InvokePreCommands(command);


            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command.Command, propegate: false);

            TryProcessCommands();
        }

        private void AddPendingCommand(Action preAction, Guid commandGuid, Guid parentGuid, BaseCommand command, int childCount)
        {
            Debug.Log($"{command.GetType()}, {parentGuid}");
            PendingCommand pendingCommand = new PendingCommand(commandGuid, command, preAction, childCount);

            if (!parentGuid.Equals(default))
            {
                var pendingParent = m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);
                pendingParent.ChildCommands.Add(pendingCommand);
            }

            m_pendingCommands.Add(pendingCommand);
        }

        private BaseCommand FindParentCommand(Guid parentGuid)
        {
            PendingCommand pendingParent = m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);

            if (!parentGuid.Equals(default))
            {
                return pendingParent.Command;
            }
            return null;
        }

        public void CreateBrushStroke(Stroke stroke, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
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

#region RPCs
        [Rpc(InvokeLocal = false)]
        public void RPC_BaseCommand(Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            Debug.Log($"Base command child count: {childCount}");
            var parentCommand = FindParentCommand(parentGuid);
            var command = new BaseCommand(parent: parentCommand);

            AddPendingCommand(() => {}, commandGuid, parentGuid, command, childCount);
        }

        [Rpc(InvokeLocal = false)]
        public void RPC_BrushStrokeFull(NetworkedStroke strokeData, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            var decode = NetworkedStroke.ToStroke(strokeData);

            CreateBrushStroke(decode, commandGuid, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        public void RPC_BrushStrokeBegin(Guid id, NetworkedStroke strokeData, int finalLength)
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
        public void RPC_BrushStrokeContinue(Guid id, int offset, NetworkedControlPoint[] controlPoints, bool[] dropPoints)
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
        public void RPC_BrushStrokeComplete(Guid id, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
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
        public void RPC_DeleteStroke(int seed, Guid commandGuid, Guid parentGuid = default, int childCount = 0)
        {
            Debug.Log(seed);
            var foundStroke = SketchMemoryScript.m_Instance.GetMemoryList.Where(x => x.m_Seed == seed).First();

            if (foundStroke != null)
            {
                Debug.Log($"Found seed: {foundStroke.m_Seed}");

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
