using UnityEngine;
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

        [Rpc(InvokeLocal = false)]
        public static void RPC_PerformCommand(NetworkRunner runner, string commandName, string guid, string[] data)
        {
            Debug.Log($"Command recieved: {commandName}");
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

                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new BrushStrokeCommand(decode), propegate: false);
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
    }
}
