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
using System.Threading.Tasks;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public interface IConnectionHandler
    {
        Task<bool> Connect();
        Task<bool> JoinRoom(RoomCreateData data);
        Task<bool> LeaveRoom(bool force = false);
        Task<bool> Disconnect();
        ConnectionState State { get; }
        ConnectionUserInfo UserInfo { get; set; }
        string LastError { get; }
    }

    public interface IDataConnectionHandler : IConnectionHandler
    {

        void Update();
        int GetPlayerCount();
        int GetNetworkedTimestampMilliseconds();
        bool GetPlayerRoomOwnershipStatus(int playerId);
        void SendLargeDataToPlayer(int playerId, byte[] largeData);
        Task<bool> PerformCommand(BaseCommand command);
        Task<bool> SendCommandToPlayer(BaseCommand command, int playerId);
        Task<bool> CheckCommandReception(BaseCommand command, int playerId);
        Task<bool> CheckStrokeReception(Stroke stroke, int playerId);
        Task<bool> UndoCommand(BaseCommand command);
        Task<bool> RedoCommand(BaseCommand command);
        Task<bool> RpcSyncToSharedAnchor(string uuid);
        Task<bool> RpcStartSyncHistory(int id);
        Task<bool> RpcSyncHistoryPercentage(int id, int exp, int snt);
        Task<bool> RpcHistorySyncComplete(int id);

        event Action Disconnected;

    }

    public interface IVoiceConnectionHandler : IConnectionHandler
    {
        void Update();
        bool StartSpeaking();
        bool StopSpeaking();
        public bool isTransmitting { get; }

    }

    public enum ConnectionState
    {
        INITIALIZING = 0,
        INITIALIZED = 1,
        DISCONNECTED = 2,
        DISCONNECTING = 3,
        CONNECTING = 4,
        AUTHENTICATING = 5,
        IN_LOBBY = 6,
        JOINING_ROOM = 7,
        IN_ROOM = 8,
        RECONNECTING = 9,
        ERROR = 10,
        LEAVING_ROOM = 11
    }

    public interface ITransientData<T>
    {
        int PlayerId { get; set; }
        bool IsSpawned { get; }
        void TransmitData(T data);
        T ReceiveData();
    }
}
