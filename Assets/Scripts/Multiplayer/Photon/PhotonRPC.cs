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
using Photon.Voice.Unity;

namespace OpenBrush.Multiplayer
{
    public class PhotonRPC : SimulationBehaviour
    {
        private const string k_LiveStrokeCapabilityCommand =
            "OpenBrush.Multiplayer.LiveStrokeCapability";
        private static Dictionary<Guid, Stroke> m_inProgressStrokes;
        private static List<PendingCommand> m_pendingCommands;
        private static Dictionary<Guid, TaskCompletionSource<bool>> m_acknowledgments;

        private sealed class IncomingLiveStrokePreview
        {
            public Guid StreamId;
            public int SourcePlayerId;
            public Stroke Stroke;
            public StrokeTimeSessionMetadata SourceTimeSession;
            public List<PointerManager.ControlPoint> ConfirmedPoints;
            public BaseBrushScript Brush;
            public BaseBrushScript ProvisionalTailBrush;
            public bool HasProvisionalTail;
            public PointerManager.ControlPoint ProvisionalTail;
            public bool HasReceivedProvisionalSequence;
            public uint LastProvisionalSequence;
            public int RenderedConfirmedPointCount;
            public float LastUpdateTime;
            public bool VisualUpdatePending;
        }

        private static Dictionary<Guid, IncomingLiveStrokePreview> m_IncomingLiveStrokes;
        private static Dictionary<Guid, float> m_ClosedLiveStrokeIds;
        private sealed class FailedLiveStrokePreview
        {
            public int SourcePlayerId;
            public float ExpiresAt;
        }
        private static Dictionary<Guid, FailedLiveStrokePreview> m_FailedLiveStrokes;
        private const int k_MaxLiveStrokeControlPoints = 32768;
        private const float k_LiveStrokeTimeoutSeconds = 10f;
        private const float k_ClosedLiveStrokeRetentionSeconds = 30f;

        public void Awake()
        {
            m_inProgressStrokes = new();
            m_pendingCommands = new();
            m_acknowledgments = new();
            m_IncomingLiveStrokes = new();
            m_ClosedLiveStrokeIds = new();
            m_FailedLiveStrokes = new();
        }

        public void Update()
        {
            TryProcessCommands();
            ApplyPendingLiveStrokeUpdates();
            ExpireLiveStrokePreviews();
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

            PendingCommand command = m_pendingCommands.FirstOrDefault(pending =>
                pending.ParentGuid == Guid.Empty ||
                SketchMemoryScript.m_Instance.FindNetworkCommand(
                    pending.ParentGuid) != null);
            if (command.Guid == Guid.Empty)
            {
                return;
            }

            bool stillPending = CheckifChildStillPending(command);

            if (stillPending)
            {
                return;
            }

            // All children present, begin execution

            m_pendingCommands.Remove(command);

            InvokePreCommands(command);

            bool parentAlreadyProcessed =
                command.Command.ParentGuid != Guid.Empty &&
                SketchMemoryScript.m_Instance.FindNetworkCommand(
                    command.Command.ParentGuid) != null;
            if (command.Command is BrushStrokeCommand ||
                command.Command is DeleteStrokeCommand)
            {
                Debug.Log(
                    $"[LiveStrokeCommand] Execute pending " +
                    $"type={command.Command.GetType().Name} command={command.Command.Guid} " +
                    $"parent={command.Command.ParentGuid} " +
                    $"children={command.Command.ChildrenCount} " +
                    $"attachToProcessedParent={parentAlreadyProcessed}.");
            }
            SketchMemoryScript.m_Instance.PerformAndRecordNetworkCommand(
                command.Command, discard: parentAlreadyProcessed);

            TryProcessCommands();
        }

        private static void AddPendingCommand(Action preAction, Guid commandGuid, Guid parentGuid, BaseCommand command, int childCount)
        {

            PendingCommand pendingCommand = new PendingCommand(
                commandGuid, parentGuid, command, preAction, childCount);

            if (!parentGuid.Equals(default))
            {
                var pendingParent = m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);
                if (!pendingParent.Guid.Equals(default))
                {
                    pendingParent.ChildCommands.Add(pendingCommand);
                }
            }

            foreach (PendingCommand orphan in m_pendingCommands
                .Where(pending => pending.ParentGuid == commandGuid)
                .ToList())
            {
                if (orphan.Command.ParentGuid == Guid.Empty)
                {
                    orphan.Command.SetParent(command);
                }
                pendingCommand.ChildCommands.Add(orphan);
            }

            m_pendingCommands.Add(pendingCommand);
        }

        private static bool CheckifCommandGuidIsInStack(Guid commandGuid)
        {

            if (SketchMemoryScript.m_Instance.IsCommandInStack(commandGuid) ||
                m_pendingCommands.Any(pending => pending.Guid == commandGuid))
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
            if (parentGuid.Equals(default)) return null;

            PendingCommand pendingParent =
                m_pendingCommands.FirstOrDefault(x => x.Guid == parentGuid);
            if (!pendingParent.Guid.Equals(default))
            {
                Debug.Log(
                    $"[LiveStrokeCommand] Parent lookup parent={parentGuid} source=pending.");
                return pendingParent.Command;
            }

            BaseCommand processedParent =
                SketchMemoryScript.m_Instance.FindNetworkCommand(parentGuid);
            Debug.Log(
                $"[LiveStrokeCommand] Parent lookup parent={parentGuid} " +
                $"source={(processedParent == null ? "missing" : "processed")}.");
            return processedParent;
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
            var command = new BaseCommand(
                commandGuid, (int)(App.Instance.CurrentSketchTime * 1000),
                parent: parentCommand);

            AddPendingCommand(() => { }, commandGuid, parentGuid, command, childCount);
        }

        public static void Send_LiveStrokeCapability(
            NetworkRunner runner, int maxStreamedPointers)
        {
            RPC_PerformCommand(
                runner, k_LiveStrokeCapabilityCommand, string.Empty,
                new[] { maxStreamedPointers.ToString() });
        }

        public static void Send_BrushStrokeFull(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeFull(runner, strokeData, commandGuid, timestamp,
                    parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeFull(runner, strokeData, commandGuid, timestamp,
                    parentGuid, childCount, targetPlayer);
            }
        }

        private static void BrushStrokeFull(
            NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0)
        {

            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            var decode = NetworkedStroke.ToStroke(strokeData);

            CreateBrushStroke(
                decode, commandGuid, timestamp, rebaseTimestamps: true,
                parentGuid, childCount);
        }

        public static void Send_BrushStrokeFullClock(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeFullClock(
                    runner, strokeData, commandGuid, timestamp, rebaseTimestamps,
                    sourceStartUtcMs,
                    sourceStartSketchTimeMs, parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeFullClock(
                    runner, strokeData, commandGuid, timestamp, rebaseTimestamps,
                    sourceStartUtcMs,
                    sourceStartSketchTimeMs, parentGuid, childCount, targetPlayer);
            }
        }

        private static void BrushStrokeFullClock(
            NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0)
        {
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            var decode = NetworkedStroke.ToStroke(strokeData);
            var sourceTimeSession = new StrokeTimeSessionMetadata
            {
                StartUtcMs = sourceStartUtcMs,
                StartSketchTimeMs = sourceStartSketchTimeMs,
            };
            CreateBrushStroke(
                decode, commandGuid, timestamp, rebaseTimestamps,
                parentGuid, childCount, sourceTimeSession);
        }

        public static void Send_BrushStrokeFullContributor(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, Guid contributorId, string contributorNickname,
            bool hasSourceTimeSession, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeFullContributor(
                    runner, strokeData, commandGuid, timestamp, rebaseTimestamps,
                    contributorId, contributorNickname, hasSourceTimeSession,
                    sourceStartUtcMs, sourceStartSketchTimeMs, parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeFullContributor(
                    runner, strokeData, commandGuid, timestamp, rebaseTimestamps,
                    contributorId, contributorNickname, hasSourceTimeSession,
                    sourceStartUtcMs, sourceStartSketchTimeMs, parentGuid, childCount,
                    targetPlayer);
            }
        }

        private static void BrushStrokeFullContributor(
            NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, Guid contributorId, string contributorNickname,
            bool hasSourceTimeSession, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0)
        {
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            var decode = NetworkedStroke.ToStroke(strokeData);
            decode.m_MultiplayerContributorId = contributorId;
            decode.m_MultiplayerContributorNickname = contributorNickname;
            StrokeTimeSessionMetadata sourceTimeSession = hasSourceTimeSession
                ? new StrokeTimeSessionMetadata
                {
                    StartUtcMs = sourceStartUtcMs,
                    StartSketchTimeMs = sourceStartSketchTimeMs,
                }
                : null;
            CreateBrushStroke(
                decode, commandGuid, timestamp, rebaseTimestamps,
                parentGuid, childCount, sourceTimeSession);
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

        public static void Send_BrushStrokeBeginContributor(
            NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength,
            Guid contributorId, string contributorNickname,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeBeginContributor(
                    runner, id, strokeData, finalLength, contributorId,
                    contributorNickname);
            }
            else
            {
                RPC_BrushStrokeBeginContributor(
                    runner, id, strokeData, finalLength, contributorId,
                    contributorNickname, targetPlayer);
            }
        }

        private static void BrushStrokeBegin(
            Guid id, NetworkedStroke strokeData, int finalLength,
            Guid contributorId = default, string contributorNickname = null)
        {
            var decode = NetworkedStroke.ToStroke(strokeData);

            decode.m_Type = Stroke.Type.NotCreated;
            decode.m_MultiplayerContributorId = contributorId;
            decode.m_MultiplayerContributorNickname = contributorNickname;
            MultiplayerManager.m_Instance.PlaceStrokeOnContributorLayer(decode);

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

        public static void Send_BrushStrokeComplete(
            NetworkRunner runner, Guid id, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeComplete(runner, id, commandGuid, timestamp,
                    parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeComplete(runner, id, commandGuid, timestamp,
                    parentGuid, childCount, targetPlayer);
            }
        }

        private static void BrushStrokeComplete(
            Guid id, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0)
        {

            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            if (!m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("shouldn't be here!");
                return;
            }

            var stroke = m_inProgressStrokes[id];

            CreateBrushStroke(
                stroke, commandGuid, timestamp, rebaseTimestamps: true,
                parentGuid, childCount);

            m_inProgressStrokes.Remove(id);
        }

        public static void Send_BrushStrokeCompleteClock(
            NetworkRunner runner, Guid id, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            if (targetPlayer == default)
            {
                RPC_BrushStrokeCompleteClock(
                    runner, id, commandGuid, timestamp, rebaseTimestamps, sourceStartUtcMs,
                    sourceStartSketchTimeMs, parentGuid, childCount);
            }
            else
            {
                RPC_BrushStrokeCompleteClock(
                    runner, id, commandGuid, timestamp, rebaseTimestamps, sourceStartUtcMs,
                    sourceStartSketchTimeMs, parentGuid, childCount, targetPlayer);
            }
        }

        private static void BrushStrokeCompleteClock(
            Guid id, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0)
        {
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            if (!m_inProgressStrokes.ContainsKey(id))
            {
                Debug.LogError("[MultiplayerStrokeClock] Missing chunked stroke at completion.");
                return;
            }

            var stroke = m_inProgressStrokes[id];
            var sourceTimeSession = new StrokeTimeSessionMetadata
            {
                StartUtcMs = sourceStartUtcMs,
                StartSketchTimeMs = sourceStartSketchTimeMs,
            };
            CreateBrushStroke(
                stroke, commandGuid, timestamp, rebaseTimestamps,
                parentGuid, childCount, sourceTimeSession);
            m_inProgressStrokes.Remove(id);
        }

        private static bool CreateBrushStroke(
            Stroke stroke, Guid commandGuid, int timestamp, bool rebaseTimestamps,
            Guid parentGuid = default, int childCount = 0,
            StrokeTimeSessionMetadata sourceTimeSession = null,
            BaseBrushScript completedPreviewBrush = null,
            bool requireSourceTimeConversion = false)
        {
            bool preserveSourceTimeSession = sourceTimeSession != null &&
                !rebaseTimestamps && !requireSourceTimeConversion;
            bool convertSourceTimeSession = sourceTimeSession != null &&
                !preserveSourceTimeSession;
            bool recordStrokeTimeSession = rebaseTimestamps || requireSourceTimeConversion;
            if (convertSourceTimeSession)
            {
                if (!RewriteStrokeTimestampsFromSourceSession(stroke, sourceTimeSession))
                {
                    if (requireSourceTimeConversion)
                    {
                        return false;
                    }
                    Debug.LogWarning(
                        $"[MultiplayerStrokeClock] Could not map stroke {stroke.m_Guid} " +
                        "from its source clock; using legacy receipt-time rebasing.");
                    if (!RebaseStrokeTimestampsToReceiver(stroke))
                    {
                        return false;
                    }
                }
            }
            else if (rebaseTimestamps && !RebaseStrokeTimestampsToReceiver(stroke))
            {
                return false;
            }

            Action preAction = () =>
            {
                stroke.m_Type = Stroke.Type.NotCreated;
                MultiplayerManager.m_Instance.PlaceStrokeOnContributorLayer(stroke);
                var canvas = stroke.m_IntendedCanvas ?? App.Scene.MainCanvas;
                if (completedPreviewBrush != null)
                {
                    FinalizeLiveStrokePreviewBrush(stroke, completedPreviewBrush);
                }
                else
                {
                    stroke.Recreate(null, canvas);
                }
                if (recordStrokeTimeSession)
                {
                    SketchMemoryScript.m_Instance.RecordStrokeInCurrentTimeSession(stroke);
                }
                else if (preserveSourceTimeSession)
                {
                    SketchMemoryScript.m_Instance.RestoreTargetedStrokeTimeSession(
                        sourceTimeSession, stroke);
                }
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
            };

            var parentCommand = FindParentCommand(parentGuid);

            var command = new BrushStrokeCommand(stroke, commandGuid, timestamp, parent: parentCommand);

            AddPendingCommand(preAction, commandGuid, parentGuid, command, childCount);
            return true;
        }

        private static void FinalizeLiveStrokePreviewBrush(
            Stroke stroke, BaseBrushScript brush)
        {
            if (App.Config.m_UseBatchedBrushes && brush.m_bCanBatch)
            {
                BatchSubset subset = brush.FinalizeBatchedBrush();
                stroke.m_Type = Stroke.Type.BatchedBrushStroke;
                stroke.m_IntendedCanvas = null;
                stroke.m_Object = null;
                stroke.m_BatchSubset = subset;
                subset.m_Stroke = stroke;
                subset.m_ParentBatch.FlushMeshUpdates();
                brush.DestroyMesh();
                UnityEngine.Object.Destroy(brush.gameObject);
            }
            else
            {
                brush.FinalizeSolitaryBrush();
                stroke.m_Type = Stroke.Type.BrushStroke;
                stroke.m_IntendedCanvas = null;
                stroke.m_BatchSubset = null;
                stroke.m_Object = brush.gameObject;
                brush.Stroke = stroke;
            }
        }

        private static bool CanAcceptLiveStrokeFrom(int sourcePlayerId)
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            return multiplayer != null &&
                multiplayer.State == ConnectionState.IN_ROOM &&
                multiplayer.IsLiveStrokeRoomStateReady &&
                multiplayer.IsLiveStrokeStreamingEnabled &&
                multiplayer.IsPlayerLiveStrokeCompatible(sourcePlayerId);
        }

        private static void LiveStrokeStart(
            Guid streamId, NetworkedLiveStrokeStart strokeData, Guid contributorId,
            string contributorNickname, long sourceStartUtcMs,
            uint sourceStartSketchTimeMs, int sourcePlayerId)
        {
            if (streamId == Guid.Empty)
            {
                return;
            }
            if (!CanAcceptLiveStrokeFrom(sourcePlayerId))
            {
                TrackFailedLiveStrokeStart(streamId, sourcePlayerId);
                return;
            }
            if (m_IncomingLiveStrokes.ContainsKey(streamId) ||
                m_ClosedLiveStrokeIds.ContainsKey(streamId) ||
                m_FailedLiveStrokes.ContainsKey(streamId))
            {
                return;
            }

            int sourcePreviewCount = m_IncomingLiveStrokes.Values.Count(
                preview => preview.SourcePlayerId == sourcePlayerId);
            int capacity = MultiplayerManager.m_Instance.MaxStreamedPointers;
            if (sourcePreviewCount >= capacity)
            {
                TrackFailedLiveStrokeStart(streamId, sourcePlayerId);
                Debug.LogWarning(
                    $"[LiveStrokeCapacity] Declined stream {streamId} from player " +
                    $"{sourcePlayerId}; active={sourcePreviewCount}, capacity={capacity}.");
                return;
            }

            Stroke stroke = NetworkedLiveStrokeStart.ToStroke(strokeData);
            if (stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length != 1 ||
                BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid) == null)
            {
                TrackFailedLiveStrokeStart(streamId, sourcePlayerId);
                return;
            }

            stroke.m_MultiplayerContributorId = contributorId;
            stroke.m_MultiplayerContributorNickname = contributorNickname;
            MultiplayerManager.m_Instance.PlaceStrokeOnContributorLayer(stroke);
            var preview = new IncomingLiveStrokePreview
            {
                StreamId = streamId,
                SourcePlayerId = sourcePlayerId,
                Stroke = stroke,
                SourceTimeSession = new StrokeTimeSessionMetadata
                {
                    StartUtcMs = sourceStartUtcMs,
                    StartSketchTimeMs = sourceStartSketchTimeMs,
                },
                ConfirmedPoints = new List<PointerManager.ControlPoint>
                {
                    stroke.m_ControlPoints[0]
                },
                LastUpdateTime = Time.realtimeSinceStartup,
            };

            if (!UpdateLiveStrokePreview(preview))
            {
                DestroyLiveStrokePreview(preview);
                TrackFailedLiveStrokeStart(streamId, sourcePlayerId);
                return;
            }
            m_IncomingLiveStrokes[streamId] = preview;
        }

        private static void TrackFailedLiveStrokeStart(
            Guid streamId, int sourcePlayerId)
        {
            m_FailedLiveStrokes[streamId] = new FailedLiveStrokePreview
            {
                SourcePlayerId = sourcePlayerId,
                ExpiresAt = Time.realtimeSinceStartup +
                    k_ClosedLiveStrokeRetentionSeconds,
            };
            MultiplayerManager.m_Instance?.DeclineLiveStroke(
                streamId, sourcePlayerId);
        }

        private static void LiveStrokeConfirmed(
            Guid streamId, int firstControlPointIndex,
            NetworkedControlPoint[] confirmedControlPoints,
            int sourcePlayerId)
        {
            if (!m_IncomingLiveStrokes.TryGetValue(
                    streamId, out IncomingLiveStrokePreview preview) ||
                preview.SourcePlayerId != sourcePlayerId)
            {
                return;
            }

            if (firstControlPointIndex < preview.ConfirmedPoints.Count)
            {
                return;
            }
            if (firstControlPointIndex != preview.ConfirmedPoints.Count ||
                confirmedControlPoints == null ||
                preview.ConfirmedPoints.Count + confirmedControlPoints.Length >
                    k_MaxLiveStrokeControlPoints)
            {
                FailLiveStrokePreview(
                    preview, requestRepair: false, Guid.Empty,
                    notifySender: true);
                return;
            }

            foreach (NetworkedControlPoint networkedPoint in confirmedControlPoints)
            {
                preview.ConfirmedPoints.Add(
                    NetworkedControlPoint.ToControlPoint(networkedPoint));
            }
            preview.HasProvisionalTail = false;
            preview.LastUpdateTime = Time.realtimeSinceStartup;
            preview.VisualUpdatePending = true;
        }

        private static void LiveStrokeProvisionalTail(
            Guid streamId, uint sequence, int confirmedControlPointCount,
            NetworkedControlPoint provisionalTail, int sourcePlayerId)
        {
            if (!m_IncomingLiveStrokes.TryGetValue(
                    streamId, out IncomingLiveStrokePreview preview) ||
                preview.SourcePlayerId != sourcePlayerId ||
                confirmedControlPointCount != preview.ConfirmedPoints.Count ||
                (preview.HasReceivedProvisionalSequence &&
                    sequence <= preview.LastProvisionalSequence))
            {
                return;
            }

            preview.HasReceivedProvisionalSequence = true;
            preview.LastProvisionalSequence = sequence;
            preview.HasProvisionalTail = true;
            preview.ProvisionalTail = NetworkedControlPoint.ToControlPoint(
                provisionalTail);
            preview.LastUpdateTime = Time.realtimeSinceStartup;
            preview.VisualUpdatePending = true;
        }

        private static void LiveStrokeComplete(
            Guid streamId, int finalControlPointCount,
            SketchMemoryScript.StrokeFlags strokeFlags, Guid commandGuid,
            int timestamp, Guid parentGuid, int childCount, int sourcePlayerId)
        {
            Debug.Log(
                $"[LiveStrokeCommand] Receive complete stream={streamId} " +
                $"command={commandGuid} parent={parentGuid} children={childCount} " +
                $"points={finalControlPointCount} source={sourcePlayerId}.");
            if (!m_IncomingLiveStrokes.TryGetValue(
                    streamId, out IncomingLiveStrokePreview preview))
            {
                if (m_FailedLiveStrokes.TryGetValue(
                        streamId, out FailedLiveStrokePreview failed) &&
                    failed.SourcePlayerId == sourcePlayerId)
                {
                    m_FailedLiveStrokes.Remove(streamId);
                    m_ClosedLiveStrokeIds[streamId] = Time.realtimeSinceStartup +
                        k_ClosedLiveStrokeRetentionSeconds;
                    MultiplayerManager.m_Instance?.RequestLiveStrokeRepair(
                        streamId, commandGuid, sourcePlayerId);
                }
                return;
            }

            if (preview.SourcePlayerId != sourcePlayerId)
            {
                return;
            }
            if (preview.VisualUpdatePending)
            {
                preview.VisualUpdatePending = false;
                if (!UpdateLiveStrokePreview(preview))
                {
                    FailLiveStrokePreview(
                        preview, requestRepair: true, commandGuid);
                    return;
                }
            }
            if (CheckifCommandGuidIsInStack(commandGuid))
            {
                FailLiveStrokePreview(preview, requestRepair: false, Guid.Empty);
                return;
            }

            if (finalControlPointCount <= 0 ||
                finalControlPointCount != preview.ConfirmedPoints.Count)
            {
                FailLiveStrokePreview(preview, requestRepair: true, commandGuid);
                return;
            }

            preview.Stroke.m_ControlPoints = preview.ConfirmedPoints.ToArray();
            preview.Stroke.m_ControlPointsToDrop = new bool[finalControlPointCount];
            preview.Stroke.m_Flags = strokeFlags;
            DestroyLiveStrokeBrush(ref preview.ProvisionalTailBrush);

            if (!CreateBrushStroke(
                preview.Stroke, commandGuid, timestamp,
                rebaseTimestamps: false, parentGuid, childCount,
                preview.SourceTimeSession, preview.Brush,
                requireSourceTimeConversion: true))
            {
                FailLiveStrokePreview(preview, requestRepair: true, commandGuid);
                return;
            }
            Debug.Log(
                $"[LiveStrokeCommand] Queued completed stroke stream={streamId} " +
                $"command={commandGuid} parent={parentGuid} children={childCount} " +
                $"seed={preview.Stroke.m_Seed}.");

            preview.Brush = null;
            m_IncomingLiveStrokes.Remove(streamId);
            m_ClosedLiveStrokeIds[streamId] = Time.realtimeSinceStartup +
                k_ClosedLiveStrokeRetentionSeconds;
        }

        private static void ApplyPendingLiveStrokeUpdates()
        {
            foreach (IncomingLiveStrokePreview preview in m_IncomingLiveStrokes.Values
                .Where(item => item.VisualUpdatePending)
                .ToList())
            {
                preview.VisualUpdatePending = false;
                if (!UpdateLiveStrokePreview(preview))
                {
                    FailLiveStrokePreview(
                        preview, requestRepair: false, Guid.Empty,
                        notifySender: true);
                }
            }
        }

        private static bool UpdateLiveStrokePreview(IncomingLiveStrokePreview preview)
        {
            if (preview.ConfirmedPoints.Count == 0 ||
                preview.RenderedConfirmedPointCount > preview.ConfirmedPoints.Count)
            {
                return false;
            }

            if (preview.Brush == null)
            {
                BrushDescriptor descriptor = BrushCatalog.m_Instance.GetBrush(
                    preview.Stroke.m_BrushGuid);
                CanvasScript canvas = preview.Stroke.m_IntendedCanvas ?? App.Scene.MainCanvas;
                if (descriptor == null || canvas == null)
                {
                    return false;
                }

                PointerManager.ControlPoint first = preview.ConfirmedPoints[0];
                preview.Brush = BaseBrushScript.Create(
                    canvas.transform,
                    TrTransform.TRS(
                        first.m_Pos, first.m_Orient, preview.Stroke.m_BrushScale),
                    descriptor, preview.Stroke.m_Color, preview.Stroke.m_BrushSize);
                preview.Brush.RandomSeed = preview.Stroke.m_Seed;
            }

            bool geometryChanged = false;
            for (int i = preview.RenderedConfirmedPointCount;
                 i < preview.ConfirmedPoints.Count; ++i)
            {
                PointerManager.ControlPoint point = preview.ConfirmedPoints[i];
                preview.Brush.UpdatePosition_LS(
                    TrTransform.TRS(
                        point.m_Pos, point.m_Orient, preview.Stroke.m_BrushScale),
                    point.m_Pressure);
                geometryChanged = true;
            }

            if (geometryChanged)
            {
                preview.Brush.ApplyChangesToVisuals();
            }

            preview.RenderedConfirmedPointCount = preview.ConfirmedPoints.Count;
            return UpdateLiveStrokeProvisionalTail(preview);
        }

        private static bool UpdateLiveStrokeProvisionalTail(
            IncomingLiveStrokePreview preview)
        {
            if (!preview.HasProvisionalTail)
            {
                DestroyLiveStrokeBrush(ref preview.ProvisionalTailBrush);
                return true;
            }

            PointerManager.ControlPoint start =
                preview.ConfirmedPoints[preview.ConfirmedPoints.Count - 1];
            TrTransform startTransform = TrTransform.TRS(
                start.m_Pos, start.m_Orient, preview.Stroke.m_BrushScale);
            if (preview.ProvisionalTailBrush == null)
            {
                BrushDescriptor descriptor = BrushCatalog.m_Instance.GetBrush(
                    preview.Stroke.m_BrushGuid);
                CanvasScript canvas = preview.Stroke.m_IntendedCanvas ?? App.Scene.MainCanvas;
                if (descriptor == null || canvas == null)
                {
                    return false;
                }

                preview.ProvisionalTailBrush = BaseBrushScript.Create(
                    canvas.transform, startTransform, descriptor,
                    preview.Stroke.m_Color, preview.Stroke.m_BrushSize);
                preview.ProvisionalTailBrush.RandomSeed = preview.Stroke.m_Seed;
                preview.ProvisionalTailBrush.SetPreviewMode();
            }

            preview.ProvisionalTailBrush.ResetBrushForPreview(startTransform);
            PointerManager.ControlPoint tail = preview.ProvisionalTail;
            preview.ProvisionalTailBrush.UpdatePosition_LS(
                TrTransform.TRS(
                    tail.m_Pos, tail.m_Orient, preview.Stroke.m_BrushScale),
                tail.m_Pressure);
            preview.ProvisionalTailBrush.ApplyChangesToVisuals();
            return true;
        }

        private static void FailLiveStrokePreview(
            IncomingLiveStrokePreview preview, bool requestRepair, Guid commandGuid,
            bool notifySender = false)
        {
            DestroyLiveStrokePreview(preview);
            m_IncomingLiveStrokes.Remove(preview.StreamId);
            if (requestRepair && commandGuid != Guid.Empty)
            {
                m_ClosedLiveStrokeIds[preview.StreamId] = Time.realtimeSinceStartup +
                    k_ClosedLiveStrokeRetentionSeconds;
                MultiplayerManager.m_Instance?.RequestLiveStrokeRepair(
                    preview.StreamId, commandGuid, preview.SourcePlayerId);
            }
            else
            {
                m_FailedLiveStrokes[preview.StreamId] = new FailedLiveStrokePreview
                {
                    SourcePlayerId = preview.SourcePlayerId,
                    ExpiresAt = Time.realtimeSinceStartup +
                        k_ClosedLiveStrokeRetentionSeconds,
                };
                if (notifySender)
                {
                    MultiplayerManager.m_Instance?.DeclineLiveStroke(
                        preview.StreamId, preview.SourcePlayerId);
                }
            }
        }

        private static void DestroyLiveStrokePreview(IncomingLiveStrokePreview preview)
        {
            if (preview == null)
            {
                return;
            }
            DestroyLiveStrokeBrush(ref preview.ProvisionalTailBrush);
            DestroyLiveStrokeBrush(ref preview.Brush);
        }

        private static void DestroyLiveStrokeBrush(ref BaseBrushScript brush)
        {
            if (brush == null)
            {
                return;
            }
            brush.DestroyMesh();
            UnityEngine.Object.Destroy(brush.gameObject);
            brush = null;
        }

        private static void ExpireLiveStrokePreviews()
        {
            float now = Time.realtimeSinceStartup;
            foreach (IncomingLiveStrokePreview preview in m_IncomingLiveStrokes.Values
                .Where(item => now - item.LastUpdateTime >= k_LiveStrokeTimeoutSeconds)
                .ToList())
            {
                FailLiveStrokePreview(
                    preview, requestRepair: false, Guid.Empty,
                    notifySender: true);
            }
            foreach (Guid streamId in m_ClosedLiveStrokeIds
                .Where(pair => pair.Value <= now)
                .Select(pair => pair.Key)
                .ToList())
            {
                m_ClosedLiveStrokeIds.Remove(streamId);
            }
            foreach (Guid streamId in m_FailedLiveStrokes
                .Where(pair => pair.Value.ExpiresAt <= now)
                .Select(pair => pair.Key)
                .ToList())
            {
                m_FailedLiveStrokes.Remove(streamId);
            }
        }

        public static void RemoveLiveStrokePreviewsForPlayer(int playerId)
        {
            if (m_IncomingLiveStrokes == null)
            {
                return;
            }
            foreach (IncomingLiveStrokePreview preview in m_IncomingLiveStrokes.Values
                .Where(item => item.SourcePlayerId == playerId)
                .ToList())
            {
                FailLiveStrokePreview(preview, requestRepair: false, Guid.Empty);
            }
            foreach (Guid streamId in m_FailedLiveStrokes
                .Where(pair => pair.Value.SourcePlayerId == playerId)
                .Select(pair => pair.Key)
                .ToList())
            {
                m_FailedLiveStrokes.Remove(streamId);
            }
        }

        private static bool RewriteStrokeTimestampsFromSourceSession(
            Stroke stroke, StrokeTimeSessionMetadata sourceTimeSession)
        {
            if (stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length == 0)
            {
                return false;
            }

            // Map from the sender's wall clock to a fresh snapshot of the receiver's
            // suspension-corrected sketch clock. An active stroke-session anchor may belong
            // to loaded history or predate a multiplayer clock synchronization.
            long targetStartUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double targetSketchTimeMsDouble = App.Instance.CurrentSketchTime * 1000.0;
            if (targetSketchTimeMsDouble < uint.MinValue ||
                targetSketchTimeMsDouble > uint.MaxValue)
            {
                return false;
            }
            uint targetStartSketchTimeMs = (uint)targetSketchTimeMsDouble;

            try
            {
                DateTimeOffset.FromUnixTimeMilliseconds(sourceTimeSession.StartUtcMs);
                DateTimeOffset.FromUnixTimeMilliseconds(targetStartUtcMs);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            foreach (var controlPoint in stroke.m_ControlPoints)
            {
                if (!SketchMemoryScript.TryConvertStrokeTimestampToSession(
                    controlPoint.m_TimestampMs,
                    sourceTimeSession.StartUtcMs,
                    sourceTimeSession.StartSketchTimeMs,
                    targetStartUtcMs,
                    targetStartSketchTimeMs,
                    out _))
                {
                    return false;
                }
            }

            for (int i = 0; i < stroke.m_ControlPoints.Length; ++i)
            {
                var controlPoint = stroke.m_ControlPoints[i];
                SketchMemoryScript.TryConvertStrokeTimestampToSession(
                    controlPoint.m_TimestampMs,
                    sourceTimeSession.StartUtcMs,
                    sourceTimeSession.StartSketchTimeMs,
                    targetStartUtcMs,
                    targetStartSketchTimeMs,
                    out controlPoint.m_TimestampMs);
                stroke.m_ControlPoints[i] = controlPoint;
            }

            return true;
        }

        private static bool RebaseStrokeTimestampsToReceiver(Stroke stroke)
        {
            if (stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length == 0)
            {
                return false;
            }

            long receiverTailMs = (long)(App.Instance.CurrentSketchTime * 1000);
            long requestedOffsetMs = receiverTailMs - stroke.TailTimestampMs;
            uint minimumTimestampMs = uint.MaxValue;
            uint maximumTimestampMs = uint.MinValue;
            foreach (PointerManager.ControlPoint controlPoint in stroke.m_ControlPoints)
            {
                minimumTimestampMs = Math.Min(
                    minimumTimestampMs, controlPoint.m_TimestampMs);
                maximumTimestampMs = Math.Max(
                    maximumTimestampMs, controlPoint.m_TimestampMs);
            }

            long minimumOffsetMs = -(long)minimumTimestampMs;
            long maximumOffsetMs = uint.MaxValue - (long)maximumTimestampMs;
            long offsetMs = Math.Max(
                minimumOffsetMs, Math.Min(requestedOffsetMs, maximumOffsetMs));
            if (offsetMs != requestedOffsetMs)
            {
                Debug.LogWarning(
                    $"[MultiplayerStrokeTime] Could not align stroke {stroke.m_Guid} " +
                    $"to receiver time without overflow; using offset {offsetMs} ms " +
                    $"instead of {requestedOffsetMs} ms.");
            }

            for (int i = 0; i < stroke.m_ControlPoints.Length; ++i)
            {
                var controlPoint = stroke.m_ControlPoints[i];
                controlPoint.m_TimestampMs = (uint)(controlPoint.m_TimestampMs + offsetMs);
                stroke.m_ControlPoints[i] = controlPoint;
            }
            return true;
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
            Debug.Log(
                $"[LiveStrokeCommand] Receive delete command={commandGuid} " +
                $"parent={parentGuid} children={childCount} seed={seed}.");
            if (CheckifCommandGuidIsInStack(commandGuid)) return;

            // TODO : implment GUID for strokesdata.
            // The range of int is large (-2,147,483,648 to 2,147,483,647), but collisions are still possible.
            Stroke foundStroke = SketchMemoryScript.m_Instance.GetMemoryList
                .FirstOrDefault(stroke => stroke.m_Seed == seed);
            string strokeSource = foundStroke == null ? "missing" : "memory";
            if (foundStroke == null)
            {
                // A delete can be a sibling of a brush command under the same compound
                // command. In that case the target exists in the pending command graph but
                // has not yet been added to memory.
                foundStroke = m_pendingCommands
                    .Select(pending => pending.Command)
                    .OfType<BrushStrokeCommand>()
                    .Select(command => command.m_Stroke)
                    .FirstOrDefault(stroke => stroke.m_Seed == seed);
                if (foundStroke != null)
                {
                    strokeSource = "pending";
                }
            }

            if (foundStroke != null)
            {
                Debug.Log(
                    $"[LiveStrokeCommand] Queue delete command={commandGuid} " +
                    $"parent={parentGuid} seed={seed} strokeSource={strokeSource}.");
                var parentCommand = FindParentCommand(parentGuid);
                var command = new DeleteStrokeCommand(foundStroke, commandGuid, timestamp, parent: parentCommand);

                AddPendingCommand(() => { }, commandGuid, parentGuid, command, childCount);
            }
            else
            {
                // Remote deletes are idempotent. The stroke may already have been removed or
                // may never have been synchronized to this client. Keep a no-op command in the
                // received command tree so its parent does not wait forever for this child.
                Debug.LogWarning(
                    $"[LiveStrokeCommand] Queue no-op delete command={commandGuid} " +
                    $"parent={parentGuid} seed={seed} strokeSource=missing.");
                var parentCommand = FindParentCommand(parentGuid);
                var placeholder = new BaseCommand(
                    commandGuid, timestamp, parent: parentCommand);
                AddPendingCommand(
                    () => { }, commandGuid, parentGuid, placeholder, childCount);
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

        private static void TransferRoomOwnership(NetworkPlayerSettings[] playerSettings,NetworkRoomSettings roomData)
        {
            if (!MultiplayerManager.m_Instance) return;

            // Convert NetworkPlayerSettings[] to RemotePlayerSettings[]
            RemotePlayerSettings[] remoteSettings = new RemotePlayerSettings[playerSettings.Length];
            for (int i = 0; i < playerSettings.Length; i++)
            {
                remoteSettings[i] = new RemotePlayerSettings(
                    playerSettings[i].m_PlayerId,
                    playerSettings[i].m_IsMutedForAll,
                    playerSettings[i].m_IsViewOnly
                );
            }

            RoomCreateData currentRoomData = new RoomCreateData();

            currentRoomData.maxPlayers = roomData.m_MaxPlayers;
            currentRoomData.silentRoom = roomData.m_IsSilentRoom;
            currentRoomData.viewOnlyRoom = roomData.m_IsViewOnlyRoom;
            
            MultiplayerManager.m_Instance.RoomOwnershipReceived(remoteSettings, currentRoomData);
        }

        private static void SetViewOnly(bool isEnabled)
        {
            MultiplayerManager.m_Instance.IsViewOnly = isEnabled;
        }

        private static void SetRoomVoiceEnabled(bool enabled, PlayerRef source)
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            int sourcePlayerId = source.RawEncoded;
            if (multiplayer == null || !multiplayer.IsPlayerRoomOwner(sourcePlayerId))
            {
                Debug.LogWarning($"[RoomVoiceOwnerValidation] Rejected voice state from player {sourcePlayerId}; sender is not the room owner.");
                return;
            }

            multiplayer.ApplyRoomVoiceEnabled(enabled);
        }

        private static void ReceiveManualColocationReference(
            NetworkManualColocationReference networkReference,
            PlayerRef source,
            bool requireCreatorMatch)
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            if (multiplayer == null)
            {
                return;
            }

            ManualColocationReference reference = networkReference.ToReference();
            int sourcePlayerId = source.RawEncoded;
            bool senderIsOwner = multiplayer.IsPlayerRoomOwner(sourcePlayerId);
            if (!senderIsOwner ||
                (requireCreatorMatch &&
                 reference.CreatorPlayerId != sourcePlayerId))
            {
                string reason = senderIsOwner
                    ? $"creator is player {reference.CreatorPlayerId}"
                    : "sender is not the room owner";
                Debug.LogWarning(
                    $"[ManualColocationAuth] Rejected reference revision {reference.Revision} from player {sourcePlayerId}: {reason}.");
                return;
            }

            multiplayer.ReceiveManualColocationReference(reference);
        }

        #region RPCS
        [Rpc(InvokeLocal = false)]
        public static void RPC_ManualColocationReference(
            NetworkRunner runner,
            NetworkManualColocationReference reference,
            RpcInfo info = default)
        {
            ReceiveManualColocationReference(
                reference, info.Source, requireCreatorMatch: true);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_ManualColocationReference(
            NetworkRunner runner,
            NetworkManualColocationReference reference,
            [RpcTarget] PlayerRef targetPlayer,
            RpcInfo info = default)
        {
            ReceiveManualColocationReference(
                reference, info.Source, requireCreatorMatch: false);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_SyncSketchTime(
            NetworkRunner runner, uint sourceSketchTimeMs,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            if (multiplayer == null ||
                !multiplayer.IsPlayerRoomOwner(info.Source.RawEncoded))
            {
                Debug.LogWarning(
                    $"[MultiplayerStrokeTime] Rejected sketch clock sync from player " +
                    $"{info.Source.RawEncoded}; sender is not the room owner.");
                return;
            }

            multiplayer.ApplySketchTimeSync(sourceSketchTimeMs);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_SyncToSharedAnchor(NetworkRunner runner, string uuid)
        {
#if OCULUS_SUPPORTED
            OculusMRController.m_Instance.RemoteSyncToAnchor(uuid);
#endif // OCULUS_SUPPORTED
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_PerformCommand(
            NetworkRunner runner, string commandName, string guid, string[] data,
            RpcInfo info = default)
        {
            Debug.Log($"Command recieved: {commandName}");

            if (commandName == k_LiveStrokeCapabilityCommand)
            {
                if (info.Source != PlayerRef.None &&
                    data != null && data.Length == 1 &&
                    int.TryParse(data[0], out int maxStreamedPointers))
                {
                    MultiplayerManager.m_Instance?.ReceiveLiveStrokeCapability(
                        info.Source.RawEncoded, maxStreamedPointers);
                }
                return;
            }

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
        private static void RPC_BrushStrokeFull(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeFull(
                strokeData, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFullClock(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeFullClock(
                strokeData, commandGuid, timestamp, rebaseTimestamps,
                sourceStartUtcMs, sourceStartSketchTimeMs, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFullContributor(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, Guid contributorId, string contributorNickname,
            bool hasSourceTimeSession, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeFullContributor(
                strokeData, commandGuid, timestamp, rebaseTimestamps, contributorId,
                contributorNickname, hasSourceTimeSession, sourceStartUtcMs,
                sourceStartSketchTimeMs, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFullContributor(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, Guid contributorId, string contributorNickname,
            bool hasSourceTimeSession, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeFullContributor(
                strokeData, commandGuid, timestamp, rebaseTimestamps, contributorId,
                contributorNickname, hasSourceTimeSession, sourceStartUtcMs,
                sourceStartSketchTimeMs, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFullClock(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeFullClock(
                strokeData, commandGuid, timestamp, rebaseTimestamps,
                sourceStartUtcMs, sourceStartSketchTimeMs, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeFull(
            NetworkRunner runner, NetworkedStroke strokeData, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeFull(
                strokeData, commandGuid, timestamp, parentGuid, childCount);
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
        private static void RPC_BrushStrokeBeginContributor(
            NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength,
            Guid contributorId, string contributorNickname,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeBegin(
                id, strokeData, finalLength, contributorId, contributorNickname);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeBeginContributor(
            NetworkRunner runner, Guid id, NetworkedStroke strokeData, int finalLength,
            Guid contributorId, string contributorNickname)
        {
            BrushStrokeBegin(
                id, strokeData, finalLength, contributorId, contributorNickname);
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
        private static void RPC_BrushStrokeComplete(
            NetworkRunner runner, Guid id, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeComplete(
                id, commandGuid, timestamp, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeCompleteClock(
            NetworkRunner runner, Guid id, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0,
            [RpcTarget] PlayerRef targetPlayer = default)
        {
            BrushStrokeCompleteClock(
                id, commandGuid, timestamp, rebaseTimestamps, sourceStartUtcMs,
                sourceStartSketchTimeMs, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeCompleteClock(
            NetworkRunner runner, Guid id, Guid commandGuid, int timestamp,
            bool rebaseTimestamps, long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeCompleteClock(
                id, commandGuid, timestamp, rebaseTimestamps, sourceStartUtcMs,
                sourceStartSketchTimeMs, parentGuid, childCount);
        }

        [Rpc(InvokeLocal = false)]
        private static void RPC_BrushStrokeComplete(
            NetworkRunner runner, Guid id, Guid commandGuid, int timestamp,
            Guid parentGuid = default, int childCount = 0)
        {
            BrushStrokeComplete(
                id, commandGuid, timestamp, parentGuid, childCount);
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

        [Rpc(InvokeLocal = false)]
        public static void RPC_TransferRoomOwnership
            (NetworkRunner runner,
             [RpcTarget] PlayerRef targetPlayer,
             NetworkPlayerSettings[] playerSettings,
             NetworkRoomSettings roomData)
        {
            TransferRoomOwnership(playerSettings,roomData);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_SetUserViewOnlyMode(NetworkRunner runner, bool value, [RpcTarget] PlayerRef targetPlayer)
        {
            SetViewOnly(value);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeRoomState(
            NetworkRunner runner, bool enabled,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            MultiplayerManager.m_Instance?.ApplyLiveStrokeRoomState(
                enabled, info.Source.RawEncoded);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeStart(
            NetworkRunner runner, Guid streamId, NetworkedLiveStrokeStart strokeData,
            Guid contributorId, string contributorNickname,
            long sourceStartUtcMs, uint sourceStartSketchTimeMs,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            LiveStrokeStart(
                streamId, strokeData, contributorId, contributorNickname,
                sourceStartUtcMs, sourceStartSketchTimeMs,
                info.Source.RawEncoded);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeConfirmed(
            NetworkRunner runner, Guid streamId, int firstControlPointIndex,
            NetworkedControlPoint[] confirmedControlPoints,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            LiveStrokeConfirmed(
                streamId, firstControlPointIndex, confirmedControlPoints,
                info.Source.RawEncoded);
        }

        [Rpc(
            InvokeLocal = false, Channel = RpcChannel.Unreliable,
            TickAligned = false)]
        public static void RPC_LiveStrokeProvisionalTail(
            NetworkRunner runner, Guid streamId, uint sequence,
            int confirmedControlPointCount, NetworkedControlPoint provisionalTail,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            LiveStrokeProvisionalTail(
                streamId, sequence, confirmedControlPointCount,
                provisionalTail, info.Source.RawEncoded);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeComplete(
            NetworkRunner runner, Guid streamId, int finalControlPointCount,
            uint strokeFlags, Guid commandGuid,
            int timestamp, Guid parentGuid, int childCount,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            LiveStrokeComplete(
                streamId, finalControlPointCount,
                (SketchMemoryScript.StrokeFlags)strokeFlags, commandGuid,
                timestamp, parentGuid, childCount, info.Source.RawEncoded);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeCancel(
            NetworkRunner runner, Guid streamId,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            if (m_IncomingLiveStrokes.TryGetValue(
                    streamId, out IncomingLiveStrokePreview preview) &&
                preview.SourcePlayerId == info.Source.RawEncoded)
            {
                FailLiveStrokePreview(
                    preview, requestRepair: false, Guid.Empty);
            }
            m_FailedLiveStrokes.Remove(streamId);
            m_ClosedLiveStrokeIds[streamId] = Time.realtimeSinceStartup +
                k_ClosedLiveStrokeRetentionSeconds;
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeDeclined(
            NetworkRunner runner, Guid streamId,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            MultiplayerManager.m_Instance?.ReceiveLiveStrokeDeclined(
                streamId, info.Source.RawEncoded);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_LiveStrokeRepairRequest(
            NetworkRunner runner, Guid streamId, Guid commandGuid,
            [RpcTarget] PlayerRef targetPlayer, RpcInfo info = default)
        {
            MultiplayerManager.m_Instance?.SendLiveStrokeRepair(
                streamId, commandGuid, info.Source.RawEncoded);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_SetRoomVoiceEnabled(
            NetworkRunner runner, bool enabled, [RpcTarget] PlayerRef targetPlayer,
            RpcInfo info = default)
        {
            SetRoomVoiceEnabled(enabled, info.Source);
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_DisconnectRemoteUser(NetworkRunner runner,[RpcTarget] PlayerRef targetPlayer)
        {
            MultiplayerManager.m_Instance.Disconnect();
        }

        [Rpc(InvokeLocal = false)]
        public static void RPC_MutePlayer(NetworkRunner runner, bool mute, int playerId)
        {
            if (MultiplayerAudioSourcesManager.m_Instance != null)
            {
                MultiplayerAudioSourcesManager.m_Instance.SetMuteForPlayer(playerId, mute);
            }
        }

        #endregion
    }
}

#endif // FUSION_WEAVER
