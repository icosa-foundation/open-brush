using CircularBuffer;
using UnityEngine;
namespace TiltBrush
{
    public class TransformBuffers
    {
        private int m_BrushBufferSize;
        private int m_WandBufferSize;
        private int m_HeadBufferSize;

        private CircularBuffer<TrTransform> m_BrushBuffer;
        private CircularBuffer<TrTransform> m_WandBuffer;
        private CircularBuffer<TrTransform> m_HeadBuffer;

        public TrTransform CurrentBrushTr => m_BrushBuffer.IsEmpty ? TrTransform.identity : m_BrushBuffer.Front();
        public TrTransform CurrentWandTr => m_WandBuffer.IsEmpty ? TrTransform.identity : m_WandBuffer.Front();
        public TrTransform CurrentHeadTr => m_HeadBuffer.IsEmpty ? TrTransform.identity : m_HeadBuffer.Front();


        public TransformBuffers(int size)
        {
            BrushBufferSize = size;
            WandBufferSize = size;
            HeadBufferSize = size;
        }

        public TrTransform PastBrushTr(int countBack)
        {
            if (m_BrushBuffer.IsEmpty) return TrTransform.identity;
            countBack = Mathf.Min(countBack, m_BrushBuffer.Size - 1);
            return m_BrushBuffer[countBack];
        }

        public TrTransform PastWandTr(int countBack)
        {
            if (m_WandBuffer.IsEmpty) return TrTransform.identity;
            countBack = Mathf.Min(countBack, m_WandBuffer.Size - 1);
            return m_WandBuffer[countBack];
        }

        public TrTransform PastHeadTr(int countBack)
        {
            if (m_HeadBuffer.IsEmpty) return TrTransform.identity;
            countBack = Mathf.Min(countBack, m_HeadBuffer.Size - 1);
            return m_HeadBuffer[countBack];
        }

        public int BrushBufferSize
        {
            get => m_BrushBufferSize;
            set
            {
                m_BrushBufferSize = value;
                m_BrushBuffer = new CircularBuffer<TrTransform>(m_BrushBufferSize);
            }
        }

        public int WandBufferSize
        {
            get => m_WandBufferSize;
            set
            {
                m_WandBufferSize = value;
                m_WandBuffer = new CircularBuffer<TrTransform>(m_WandBufferSize);
            }
        }

        public int HeadBufferSize
        {
            get => m_HeadBufferSize;
            set
            {
                m_HeadBufferSize = value;
                m_HeadBuffer = new CircularBuffer<TrTransform>(m_HeadBufferSize);
            }
        }

        public void AddBrushTr(TrTransform tr) { m_BrushBuffer.PushFront(tr); }
        public void AddWandTr(TrTransform tr) { m_WandBuffer.PushFront(tr); }
        public void AddHeadTr(TrTransform tr) { m_HeadBuffer.PushFront(tr); }
    }
}
