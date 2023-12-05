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
using System.Numerics;
using System.Threading.Tasks;
using TiltBrush;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    public interface IConnectionHandler
    {
        Task<bool> Connect();

        bool IsConnected();
        Task<bool> Disconnect(bool force = false);

        void Update();

        Task<bool> PerformCommand(BaseCommand command);
        Task<bool> UndoCommand(BaseCommand command);
        Task<bool> RedoCommand(BaseCommand command);
        Task<bool> RpcSyncToSharedAnchor(string uuid);

        //ITransientData<PlayerRigData> SpawnPlayer();
    }

    public interface ITransientData<T>
    {
        void TransmitData(T data);
        T RecieveData();
    }
}
