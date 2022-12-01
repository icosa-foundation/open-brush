using UnityEngine;

namespace IsoMesh
{
    [System.Serializable]
    public class MainSettings
    {
        [SerializeField]
        private bool m_autoUpdate = true;
        public bool AutoUpdate
        {
            get => m_autoUpdate;
            set => m_autoUpdate = value;
        }

        [SerializeField]
        private OutputMode m_outputMode = OutputMode.Procedural;
        public OutputMode OutputMode => m_outputMode;

        [SerializeField]
        private bool m_isAsynchronous = false;
        public bool IsAsynchronous => m_isAsynchronous;

        [SerializeField]
        private Material m_proceduralMaterial;
        public Material ProceduralMaterial => m_proceduralMaterial;

        public void CopySettings(MainSettings source)
        {
            m_autoUpdate = source.m_autoUpdate;
            m_outputMode = source.m_outputMode;
            m_isAsynchronous = source.m_isAsynchronous;
            m_proceduralMaterial = source.m_proceduralMaterial;
        }
    }
}