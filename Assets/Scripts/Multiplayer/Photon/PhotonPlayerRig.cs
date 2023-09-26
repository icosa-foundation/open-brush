using Fusion;

namespace OpenBrush.Multiplayer
{
    public class PhotonPlayerRig : NetworkBehaviour, ITransientData<PlayerRigData>
    {
        public NetworkTransform m_PlayArea;
        public NetworkTransform m_PlayerHead;
        public NetworkTransform m_Left;
        public NetworkTransform m_Right;

        private PlayerRigData dataHolder;
        private PlayerRigData recievedData;

        // Start is called before the first frame update
        void Start()
        {
            
        }

        // Update is called once per frame

        void Update()
        {
            
        }

        public void TransmitData(PlayerRigData data)
        {
            dataHolder = data;
        }

        public PlayerRigData RecieveData()
        {
            return recievedData;
        }

        public override void Render()
        {
            base.Render();

            if (Object.HasStateAuthority)
            {
                m_PlayerHead.InterpolationTarget.position = dataHolder.HeadPosition;
                m_PlayerHead.InterpolationTarget.rotation = dataHolder.HeadRotation;
            }
            else
            {
                recievedData = new PlayerRigData();
                recievedData.HeadPosition = m_PlayerHead.InterpolationTarget.position;
                recievedData.HeadRotation = m_PlayerHead.InterpolationTarget.rotation;
            }
        }
    }
}

