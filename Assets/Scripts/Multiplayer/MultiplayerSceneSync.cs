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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TiltBrush;
using TMPro;
using UnityEngine;


namespace OpenBrush.Multiplayer
{
    public class MultiplayerSceneSync : MonoBehaviour
    {
        public static MultiplayerSceneSync m_Instance;
        public Action<byte[]> onLargeDataReceived;
        [HideInInspector] public int batchSize = 10;
        [HideInInspector] public float delayBetweenBatches = 0.05f;
        public SyncType m_SyncType = SyncType.Strokes;
        [HideInInspector] public int numberOfCommandsExpected = 0;
        [HideInInspector] public int numberOfCommandsSent = 0;

        private bool _isWaiting = false;
        private bool _isSendingCommandHistory = false;


        void Awake()
        {
            m_Instance = this;
        }

        void Start()
        {
            onLargeDataReceived += OnLargeDataReceived;
        }

        private void Update()
        {
            ProcessQueue();
        }

        void OnDestroy()
        {
            onLargeDataReceived -= OnLargeDataReceived;
        }

        public void StartSyncronizationForUser(int id)
        {

            switch (m_SyncType)
            {
                case SyncType.Strokes:
                    SendStrokesToPlayer(id);
                    break;
                case SyncType.Commands:
                    StartCoroutine(SendCommandHistory(id));
                    break;
            }

        }

        #region Syncronization Logic Strokes
        async void SendStrokesToPlayer(int id)
        {
            LinkedList<Stroke> strokes = SketchMemoryScript.m_Instance.GetMemoryList;

            if (strokes.Count == 0) return;

            SendCurrentTargetEnvironmentCommand();
            StartSyncProgressDisplayForSrokes(id, strokes);
            const int chunkSize = 5;
            List<Stroke> strokeList = strokes.ToList();

            int counter = 0;
            for (int i = 0; i < strokeList.Count; i += chunkSize)
            {
                var chunk = strokeList.Skip(i).Take(chunkSize).ToList();
                byte[] strokesData = await MultiplayerStrokeSerialization.SerializeAndCompressMemoryListAsync(chunk);
                MultiplayerManager.m_Instance.SendLargeDataToPlayer(id, strokesData);
                counter += chunk.Count;
                //Debug.Log($"Sent {strokesData.Length} bytes of serialized stroke data (batch {(i / chunkSize) + 1}) to player {id}.");
            }
        }

        async void DeserializeReceivedStrokes(byte[] largeData)
        {

            // Decompress and deserialize strokes asynchronously
            List<Stroke> strokes = await MultiplayerStrokeSerialization.DecompressAndDeserializeMemoryListAsync(largeData);

            Debug.Log($"Successfully deserialized {strokes.Count} strokes.");

            // Handle the strokes (e.g., add them to the scene or memory)
            foreach (var stroke in strokes)
            {
                BrushStrokeCommand c = new BrushStrokeCommand(stroke);
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                SketchMemoryScript.m_Instance.PerformAndRecordNetworkCommand(c, true);
            }

        }

        void OnLargeDataReceived(byte[] largeData)
        {
            //Debug.Log($"[Multiplayer Scene Sync]Successfully received {largeData.Length} bytes from the autosave.");

            DeserializeReceivedStrokes(largeData);
        }

        #endregion

        #region Syncronization Logic Commands
        public void SendCurrentTargetEnvironmentCommand()
        {
            TiltBrush.Environment targetEnvironment = SceneSettings.m_Instance.GetDesiredPreset();

            if (targetEnvironment != null)
            {
                SwitchEnvironmentCommand command = new SwitchEnvironmentCommand(targetEnvironment);
                MultiplayerManager.m_Instance.OnCommandPerformed(command);
            }
        }

        public IEnumerator SendCommandHistory(int id)
        {
            if (_isWaiting) yield break;

            if (_isSendingCommandHistory)
            {
                _isWaiting = true;
                while (_isSendingCommandHistory)
                {
                    yield return null;
                }
                _isWaiting = false;
            }

            _isSendingCommandHistory = true;


            List<Stroke> strokesWithoutCommand = SketchMemoryScript.m_Instance.GetStrokesWithoutCommand();
            IEnumerable<BaseCommand> commands = SketchMemoryScript.m_Instance.GetAllOperations();

            int firstCommandTimestamp = commands.Any() ? commands.First().NetworkTimestamp ?? int.MaxValue : int.MaxValue;

            CreateBrushStrokeCommands(strokesWithoutCommand, firstCommandTimestamp); // this add the strokes without commands to the IEnumerable<BaseCommand> commands

            if (commands.Count() == 0) yield break;

            SendCurrentTargetEnvironmentCommand();

            StartSyncProgressDisplayForCommands(id, commands.ToList());

            foreach (BaseCommand command in commands) MultiplayerManager.m_Instance.OnCommandPerformed(command);

            _isSendingCommandHistory = false;

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

        public async void StartSyncProgressDisplayForSrokes(int TargetPlayerId, LinkedList<Stroke> strokes)
        {
            StartSynchHistory(TargetPlayerId);

            int sentStrokes = 0;

            foreach (var stroke in strokes)
            {

                while (await MultiplayerManager.m_Instance.CheckStrokeReception(stroke, TargetPlayerId))
                {
                    await Task.Delay(200);
                }

                sentStrokes++;
                SynchHistoryPercentage(TargetPlayerId, strokes.Count, sentStrokes);
            }

            SynchHistoryComplete(TargetPlayerId);
        }

        public async void StartSyncProgressDisplayForCommands(int TargetPlayerId, List<BaseCommand> commands)
        {
            StartSynchHistory(TargetPlayerId);

            int sentStrokes = 0;
            foreach (var command in commands)
            {
                while (await MultiplayerManager.m_Instance.CheckCommandReception(command, TargetPlayerId))
                {
                    await Task.Delay(200);
                }
                sentStrokes++;
                SynchHistoryPercentage(TargetPlayerId, commands.Count, sentStrokes);
            }

            SynchHistoryComplete(TargetPlayerId);
        }

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

        private readonly object infoCardLock = new object();
        private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private InfoCardAnimation infoCard;

        private void EnqueueMessage(string message)
        {
            messageQueue.Enqueue(message);
        }

        private void ProcessQueue() //once per frame
        {
            if (messageQueue.TryDequeue(out string message))
            {
                if (infoCard == null)
                {
                    DisplaySynchInfo(message);
                }
                else
                {
                    UpdateInfoCard(message);
                }
            }
        }

        private void DisplaySynchInfo(string text)
        {
            if (infoCard == null)
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    text,
                    fPopScalar: 1.0f
                );
                infoCard = RetrieveInfoCard();
            }
            else
            {
                UpdateInfoCard(text);
            }
        }

        private void UpdateInfoCard(string text)
        {
            infoCard.GetComponentInChildren<TextMeshPro>().text = text;
            infoCard.UpdateHoldingDuration(5f);
        }

        private InfoCardAnimation RetrieveInfoCard()
        {
            InfoCardAnimation[] allInfoCards = FindObjectsOfType<InfoCardAnimation>();
            foreach (var card in allInfoCards)
            {
                TextMeshPro textComponent = card.GetComponentInChildren<TextMeshPro>();
                if (textComponent != null && textComponent.text.Contains("Sync"))
                {
                    return card;
                }
            }
            return null;
        }

        public void StartSynchInfo()
        {
            EnqueueMessage("Sync Started!");
        }
        public void SynchInfoPercentageUpdate()
        {
            int percentage = (int)((float)SketchMemoryScript.AllStrokesCount() / numberOfCommandsExpected * 100);
            EnqueueMessage($"Sync {percentage}%");
        }

        public void HideSynchInfo()
        {
            EnqueueMessage("Sync Ended!");
        }

        #endregion

    }

    public enum SyncType
    {
        Strokes,
        Commands
    }
}