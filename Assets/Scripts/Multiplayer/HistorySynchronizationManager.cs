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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiltBrush;
using TMPro;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    public class HistorySynchronizationManager : MonoBehaviour
    {
        public static HistorySynchronizationManager m_Instance;
        public int batchSize = 60;
        public float delayBetweenBatches = 0.05f;
        [HideInInspector] public int numberOfCommandsExpected = 0;
        [HideInInspector] public int numberOfCommandsSent = 0;

        private bool _isWaiting = false;
        private bool _isSendingCommandHistory = false;
        private InfoCardAnimation infoCard;
        private HashSet<int> currentlyProcessingUsers = new HashSet<int>();
        private Dictionary<int, Queue<HistorySynchronizationCommand>> userCommandQueues = new Dictionary<int, Queue<HistorySynchronizationCommand>>();
        private readonly SemaphoreSlim _networkLock = new SemaphoreSlim(1, 1);

        void Awake()
        {
            m_Instance = this;
        }

        public async void StartSyncronizationForUser(int playerId)
        {

            if (!userCommandQueues.ContainsKey(playerId))
                userCommandQueues[playerId] = new Queue<HistorySynchronizationCommand>();

            PrepareHistory(playerId);

            if (!currentlyProcessingUsers.Contains(playerId))
                await ProcessQueueForUser(playerId);
        }

        #region Syncronization Logic
        public void SendCurrentTargetEnvironmentCommand()
        {
            TiltBrush.Environment targetEnvironment = SceneSettings.m_Instance.GetDesiredPreset();

            if (targetEnvironment != null)
            {
                SwitchEnvironmentCommand command = new SwitchEnvironmentCommand(targetEnvironment);
                MultiplayerManager.m_Instance.OnCommandPerformed(command);
            }
        }

        public void PrepareHistory(int playerId)
        {

            List<Stroke> strokesWithoutCommand = SketchMemoryScript.m_Instance.GetStrokesWithoutCommand();
            IEnumerable<BaseCommand> commands = SketchMemoryScript.m_Instance.GetAllOperations();

            int firstCommandTimestamp = commands.Any() ? commands.First().NetworkTimestamp ?? int.MaxValue : int.MaxValue;

            CreateBrushStrokeCommands(strokesWithoutCommand, firstCommandTimestamp);

            foreach (BaseCommand command in commands)
            {
                HistorySynchronizationCommand c = new HistorySynchronizationCommand(command, playerId);
                userCommandQueues[playerId].Enqueue(c);
            }

        }

        private async Task ProcessQueueForUser(int playerId)
        {
            currentlyProcessingUsers.Add(playerId);

            StartSynchHistory(playerId);

            if (!userCommandQueues.TryGetValue(playerId, out var commandQueue))
            {
                Debug.LogWarning($"No queue found for user {playerId}. Exiting.");
                CleanupUserQueue(playerId);
                return;
            }

            int totalCommands = commandQueue.Count;
            int processedCommands = 0;

            while (commandQueue.Count > 0)
            {
                var command = commandQueue.Peek();
                Debug.Log($"Processing command for User {playerId}.");

                if (!MultiplayerManager.m_Instance.IsRemotePlayerStillConnected(playerId))
                {
                    Debug.LogWarning($"User {playerId} is no longer connected. Clearing their queue.");
                    CleanupUserQueue(playerId);
                    break;
                }

                await _networkLock.WaitAsync();
                try
                {
                    bool success = await command.Process();

                    if (success)
                    {
                        Debug.Log($"Command successfully acknowledged for User {playerId}.");
                        commandQueue.Dequeue();
                        processedCommands++;
                        SynchHistoryPercentage(playerId, totalCommands, processedCommands);
                    }
                    else
                    {
                        Debug.LogError($"Command failed after retries {command.Attempts} for User {playerId}.");
                        CleanupUserQueue(playerId);
                        break;
                    }
                }
                finally
                {
                    _networkLock.Release();
                }

            }

            Debug.Log($"Finished processing the queue for User {playerId}.");
            SynchHistoryComplete(playerId);
            currentlyProcessingUsers.Remove(playerId);
            userCommandQueues.Remove(playerId);
        }

        private void CleanupUserQueue(int playerId)
        {
            currentlyProcessingUsers.Remove(playerId);
            userCommandQueues.Remove(playerId);
            Debug.Log($"Cleaned up resources for User {playerId}.");
        }

        private void CreateBrushStrokeCommands(List<Stroke> strokes, int LastTimestamp)
        {
            if (strokes == null || strokes.Count == 0) return;

            strokes = strokes.OrderBy(s => s.HeadTimestampMs).ToList();

            uint earliestStrokeTimestampMs = strokes.First().HeadTimestampMs;
            uint latestStrokeTimestampMs = strokes.Last().TailTimestampMs;
            uint totalStrokeTimeMs = latestStrokeTimestampMs - earliestStrokeTimestampMs;

            if (totalStrokeTimeMs == 0) totalStrokeTimeMs = 1;

            foreach (var stroke in strokes)
            {
                uint strokeTimeMs = stroke.HeadTimestampMs - earliestStrokeTimestampMs;
                long numerator = (long)strokeTimeMs * (LastTimestamp - 1);
                int timestamp = (int)(numerator / totalStrokeTimeMs);

                if (timestamp >= LastTimestamp)
                {
                    timestamp = LastTimestamp - 1;
                }

                BrushStrokeCommand command = new BrushStrokeCommand(stroke, Guid.NewGuid(), timestamp);
                SketchMemoryScript.m_Instance.AddCommandToNetworkStack(command);
            }
        }

        #endregion

        #region Remote infoCard commands
        private void StartSynchHistory(int id)
        {

            MultiplayerManager.m_Instance.StartSynchHistory(id);
        }

        private void SynchHistoryPercentage(int id, int expected, int sent)
        {
            MultiplayerManager.m_Instance.SynchHistoryPercentage(id, expected, sent);
        }

        private void SynchHistoryComplete(int id)
        {
            MultiplayerManager.m_Instance.SynchHistoryComplete(id);
        }

        #endregion

        #region Local infoCard commands
        public void DisplaySynchInfo()
        {
            OutputWindowScript.m_Instance.CreateInfoCardAtController(
                InputManager.ControllerName.Brush,
                "Synch Started!",
                fPopScalar: 1.0f
            );
            RetrieveInfoCard();
        }

        public InfoCardAnimation RetrieveInfoCard()
        {
            InfoCardAnimation[] allInfoCards = UnityEngine.Object.FindObjectsOfType<InfoCardAnimation>();

            foreach (var card in allInfoCards)
            {
                TextMeshPro textComponent = card.GetComponentInChildren<TextMeshPro>();
                if (textComponent != null && textComponent.text.Contains("Synch"))
                {
                    infoCard = card;
                    return card;
                }
            }

            return null;
        }

        public void SynchInfoPercentageUpdate()
        {
            int percentage = (int)((float)SketchMemoryScript.AllStrokesCount() / numberOfCommandsExpected * 100);
            string text = $"Synch {percentage}%";

            if (infoCard == null) infoCard = RetrieveInfoCard();
            if (infoCard == null) DisplaySynchInfo();

            infoCard.GetComponentInChildren<TextMeshPro>().text = text;
            infoCard.UpdateHoldingDuration(5f);
        }

        public void HideSynchInfo()
        {
            if (infoCard == null) infoCard = RetrieveInfoCard();
            if (infoCard == null) DisplaySynchInfo();

            infoCard.GetComponentInChildren<TextMeshPro>().text = "Synch Ended!";
            infoCard.UpdateHoldingDuration(3.0f);
        }

        #endregion

    }

    public class HistorySynchronizationCommand
    {
        public BaseCommand Command { get; private set; }
        public int TargetPlayerId { get; private set; }
        public bool IsAcknowledged { get; private set; }
        public float LastSentTime { get; private set; }
        public int Attempts { get; private set; }

        private const int MaxRetries = 5;
        private float RetryDelay = 0.1f;

        public HistorySynchronizationCommand(BaseCommand command, int targetPlayerId)
        {
            Command = command;
            IsAcknowledged = false;
            LastSentTime = Time.time;
            Attempts = 0;
            TargetPlayerId = targetPlayerId;
        }

        public async Task<bool> Process()
        {

            while (Attempts < MaxRetries)
            {
                Attempts++;
                LastSentTime = Time.time;

                MultiplayerManager.m_Instance.SendCommandToPlayer(Command, TargetPlayerId);

                bool isAcknowledged = await MultiplayerManager.m_Instance.CheckCommandReception(Command, TargetPlayerId);

                if (isAcknowledged)
                {
                    IsAcknowledged = true;
                    return true;
                }

                Debug.Log($"Attempt {Attempts} failed for command. Retrying in {RetryDelay} seconds...");
                await Task.Delay(TimeSpan.FromSeconds(RetryDelay));
                RetryDelay *= 2;
            }

            Debug.LogError($"Command acknowledgment failed after {MaxRetries} attempts.");
            return false;
        }
    }

}
