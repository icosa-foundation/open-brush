using UnityEngine;

namespace IsoMesh
{
    [System.Serializable]
    public class AlgorithmSettings
    {
        [SerializeField]
        private float m_maxAngleTolerance = 20f;
        public float MaxAngleTolerance => m_maxAngleTolerance;

        [SerializeField]
        private float m_visualNormalSmoothing = 1e-5f;
        public float VisualNormalSmoothing => m_visualNormalSmoothing;

        [SerializeField]
        private IsosurfaceExtractionType m_isosurfaceExtractionType = IsosurfaceExtractionType.SurfaceNets;
        public IsosurfaceExtractionType IsosurfaceExtractionType => m_isosurfaceExtractionType;

        //[SerializeField]
        //private float m_constrainToCellUnits = 0f;
        //public float ConstrainToCellUnits => m_constrainToCellUnits;

        //[SerializeField]
        //private bool m_overrideQEFSettings = false;
        //public bool OverrideQEFSettings => m_overrideQEFSettings;

        //[SerializeField]
        //private int m_qefSweeps = 5;
        //public int QefSweeps => m_qefSweeps;

        //[SerializeField]
        //private float m_qefPseudoInverseThreshold = 1e-2f;
        //public float QefPseudoInverseThreshold => m_qefPseudoInverseThreshold;

        [SerializeField]
        private EdgeIntersectionType m_edgeIntersectionType = EdgeIntersectionType.Interpolation;
        public EdgeIntersectionType EdgeIntersectionType => m_edgeIntersectionType;

        [SerializeField]
        private int m_binarySearchIterations = 5;
        public int BinarySearchIterations => m_edgeIntersectionType == EdgeIntersectionType.Interpolation ? 0 : m_binarySearchIterations;

        [SerializeField]
        private bool m_applyGradientDescent = false;
        public bool ApplyGradientDescent => m_applyGradientDescent;

        [SerializeField]
        private int m_gradientDescentIterations = 10;
        public int GradientDescentIterations => m_applyGradientDescent ? m_gradientDescentIterations : 0;

        //[SerializeField]
        //private float m_nudgeVerticesToAverageNormalScalar = 0.01f;
        //public float NudgeVerticesToAverageNormalScalar => m_nudgeVerticesToAverageNormalScalar;

        //[SerializeField]
        //private float m_nudgeMaxMagnitude = 1f;
        //public float NudgeMaxMagnitude => m_nudgeMaxMagnitude;


        public void CopySettings(AlgorithmSettings source)
        {
            m_maxAngleTolerance = source.m_maxAngleTolerance;
            m_visualNormalSmoothing = source.m_visualNormalSmoothing;
            m_isosurfaceExtractionType = source.m_isosurfaceExtractionType;
            //m_constrainToCellUnits = source.m_constrainToCellUnits;
            //m_overrideQEFSettings = source.m_overrideQEFSettings;
            //m_qefSweeps = source.m_qefSweeps;
            //    m_qefPseudoInverseThreshold = source.m_qefPseudoInverseThreshold;
            m_edgeIntersectionType = source.m_edgeIntersectionType;
            m_binarySearchIterations = source.m_binarySearchIterations;
            m_applyGradientDescent = source.m_applyGradientDescent;
            m_gradientDescentIterations = source.m_gradientDescentIterations;
            //m_nudgeVerticesToAverageNormalScalar = source.m_nudgeVerticesToAverageNormalScalar;
            //m_nudgeMaxMagnitude = source.m_nudgeMaxMagnitude;
        }
    }

}