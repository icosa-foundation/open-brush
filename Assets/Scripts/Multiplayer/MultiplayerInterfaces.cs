

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
        Task<bool> Disconnect(bool force = false);

        void Update();
        
        Task<bool> PerformCommand(BaseCommand command);
        Task<bool> UndoCommand(BaseCommand command);
        Task<bool> RpcSyncToSharedAnchor(string uuid);

        //ITransientData<PlayerRigData> SpawnPlayer();
    }

    public interface ITransientData<T>
    {
        void TransmitData(T data);
        T RecieveData();
    }
}
