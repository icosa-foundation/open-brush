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
using System.Collections.Generic;
using System.Linq;
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


        void Awake()
        {
            m_Instance = this;
        }


        public void StartSyncronizationForUser(int id)
        {

            StartSynchHistory(id);
            SendCurrentTargetEnvironmentCommand();
            StartCoroutine(SendStrokesAndCommandHistory(id));
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

        public IEnumerator SendStrokesAndCommandHistory(int id)
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

            CreateBrushStrokeCommands(strokesWithoutCommand, firstCommandTimestamp);

            int packetCounter = 0;
            int counter = 0;
            foreach (BaseCommand command in commands)
            {
                int estimatedMessages = EstimateMessagesForCommand(command);

                if (packetCounter + estimatedMessages > batchSize)
                {
                    yield return null;
                    yield return new WaitForSeconds(delayBetweenBatches);
                }

                MultiplayerManager.m_Instance.OnCommandPerformed(command);
                packetCounter += estimatedMessages;
                counter++;
                SynchHistoryPercentage(id, commands.Count(), counter);
            }

            _isSendingCommandHistory = false;

            SynchHistoryComplete(id);

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

        private int EstimateMessagesForCommand(BaseCommand command)
        {
            switch (command)
            {
                case BrushStrokeCommand strokeCommand:
                    int totalControlPoints = strokeCommand.m_Stroke.m_ControlPoints.Length;
                    return totalControlPoints <= NetworkingConstants.MaxControlPointsPerChunk
                        ? 1
                        : 2 + ((int)Math.Ceiling((double)totalControlPoints / NetworkingConstants.MaxControlPointsPerChunk) - 1);
                default:
                    return 1;
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
}