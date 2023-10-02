

using System;
using System.Numerics;
using System.Threading.Tasks;

namespace OpenBrush.Multiplayer
{
    public interface IConnectionHandler
    {
        Task<bool> Connect();
        Task<bool> Disconnect(bool force = false);

        //ITransientData<PlayerRigData> SpawnPlayer();
    }

    public interface ITransientData<T>
    {
        void TransmitData(T data);
        T RecieveData();
    }
}
