using Fusion;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public class PhotonRPC : SimulationBehaviour
    {
        [Rpc(InvokeLocal = false)]
        public static void RPC_SyncToSharedAnchor(NetworkRunner runner, string uuid)
        {
            OculusMRController.m_Instance.RemoteSyncToAnchor(uuid);
        }
    }
}