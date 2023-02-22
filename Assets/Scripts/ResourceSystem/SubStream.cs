using System;
using System.IO;
namespace TiltBrush
{
    /// <summary>
    /// A Stream to wrap another stream, starting at the current position.
    /// </summary>
    public class SubStream : Stream
    {
        private Stream m_Stream;
        private long m_Offset;
        private long m_Position;

        public SubStream(Stream original)
        {
            m_Stream = original;
            m_Offset = m_Stream.Position;
            m_Position = m_Stream.Position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();
            if (m_Stream.Position != m_Position)
            {
                m_Stream.Seek(m_Position, SeekOrigin.Begin);
            }
            int read = m_Stream.Read(buffer, offset, count);
            m_Position += read;
            return read;
        }

        private void CheckDisposed()
        {
            if (m_Stream == null) throw new ObjectDisposedException(GetType().Name);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    m_Position = m_Stream.Seek(offset + m_Offset, SeekOrigin.Begin);
                    break;
                case SeekOrigin.Current:
                    m_Position = m_Stream.Seek(m_Position + offset, SeekOrigin.Begin);
                    break;
                case SeekOrigin.End:
                    m_Position = m_Stream.Seek(offset, SeekOrigin.End);
                    break;
            }
            return m_Position - m_Offset;
        }

        public override bool CanRead => m_Stream.CanRead;

        public override bool CanSeek => m_Stream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => m_Stream.Length - m_Offset;

        public override long Position
        {
            get
            {
                return m_Position - m_Offset;
            }
            set
            {
                this.Seek(value + m_Offset, SeekOrigin.Begin);
                m_Position = value + m_Offset;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
