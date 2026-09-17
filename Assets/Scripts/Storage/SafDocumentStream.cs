// Copyright 2026 The Open Brush Authors
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
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    /// A stream that can be made durable, not merely flushed to the operating system.
    /// FileStream.Flush(flushToDisk: true) is the equivalent; SAF documents need their own
    /// because the descriptor backing them is not reachable from managed code.
    public interface ISyncableStream
    {
        void FlushToDisk();
    }

    /// Positioned access to an open SAF document. The implementation that matters lives in Java;
    /// this exists so the buffering in SafDocumentStream can be tested without a device.
    /// Signed bytes, because Java's byte is signed and Unity converts element by element -
    /// with a deprecation warning apiece - when handed a byte[]. Buffer.BlockCopy moves between
    /// sbyte[] and byte[] freely, so the signedness never reaches the Stream API above.
    internal interface ISafDocumentChannel
    {
        /// Returns the bytes read - fewer than asked for only at end of file - or null on failure.
        sbyte[] Read(long position, int count);
        /// Returns the number of bytes written, or -1 on failure.
        int Write(long position, sbyte[] data, int count);
        bool Truncate(long length);
        bool Flush(bool toDisk);
        string DescribeLastError();
        void Close();
    }

    /// A seekable stream over a SAF document whose file descriptor never crosses JNI.
    ///
    /// The obvious implementation - ParcelFileDescriptor.detachFd() into a SafeFileHandle into a
    /// FileStream - segfaults IL2CPP inside the FileStream constructor, reproducibly and for every
    /// overload, so the descriptor stays in Java and every read and write here is a positioned
    /// call against the FileChannel holding it. Measured on a Nothing Phone (3a): reads 327 MB/s,
    /// writes 30 MB/s, the latter bounded by marshalling the byte[] argument into Java rather than
    /// by storage.
    ///
    /// Each crossing costs enough that unbuffered small operations would dominate: a zip central
    /// directory is read a few bytes at a time. Hence the read-ahead and write-behind buffers
    /// below. At most one of them holds data at any moment.
    public sealed class SafDocumentStream : Stream, ISyncableStream
    {
        private const int kReadBufferSize = 64 * 1024;
        private const int kWriteBufferSize = 256 * 1024;

        private readonly bool m_CanWrite;
        private ISafDocumentChannel m_Channel;
        private long m_Position;
        private long m_Length;

        private sbyte[] m_ReadBuffer;
        private long m_ReadBufferStart;
        private int m_ReadBufferLength;

        private sbyte[] m_WriteBuffer;
        private long m_WriteBufferStart;
        private int m_WriteBufferLength;

        internal SafDocumentStream(ISafDocumentChannel channel, long length, bool canWrite)
        {
            m_Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            m_Length = length;
            m_CanWrite = canWrite;
        }

        internal SafDocumentStream(int handle, long length, bool canWrite)
            : this(new SafJniDocumentChannel(handle), length, canWrite)
        {
        }

        public override bool CanRead => m_Channel != null;
        public override bool CanSeek => m_Channel != null;
        public override bool CanWrite => m_Channel != null && m_CanWrite;

        public override long Length
        {
            get
            {
                ThrowIfClosed();
                return m_Length;
            }
        }

        public override long Position
        {
            get => m_Position;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                ThrowIfClosed();
                m_Position = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfClosed();
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => m_Position + offset,
                SeekOrigin.End => m_Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (target < 0)
            {
                throw new IOException("Cannot seek before the start of the document.");
            }
            m_Position = target;
            return m_Position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            ThrowIfClosed();
            if (count == 0)
            {
                return 0;
            }
            DrainWriteBuffer();
            // Filled rather than merely served: a short read is legal for a Stream, but enough
            // callers here predate that contract - zip readers especially - that it is not worth
            // finding out which ones. Only end of file returns less than was asked for.
            int total = 0;
            while (total < count)
            {
                int served = ReadOnce(buffer, offset + total, count - total);
                if (served == 0)
                {
                    break;
                }
                total += served;
            }
            return total;
        }

        public override int ReadByte()
        {
            byte[] one = new byte[1];
            return Read(one, 0, 1) == 1 ? one[0] : -1;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            ThrowIfClosed();
            if (!m_CanWrite)
            {
                throw new NotSupportedException("The SAF document is open for reading only.");
            }
            if (count == 0)
            {
                return;
            }
            m_ReadBufferLength = 0;

            if (m_WriteBufferLength > 0 &&
                m_WriteBufferStart + m_WriteBufferLength != m_Position)
            {
                // A seek moved the write point away from the pending bytes, which still have to
                // land where they were written.
                DrainWriteBuffer();
            }
            if (count >= kWriteBufferSize)
            {
                DrainWriteBuffer();
                WriteThrough(m_Position, buffer, offset, count);
                Advance(count);
                return;
            }

            m_WriteBuffer ??= new sbyte[kWriteBufferSize];
            if (m_WriteBufferLength == 0)
            {
                m_WriteBufferStart = m_Position;
            }
            else if (count > kWriteBufferSize - m_WriteBufferLength)
            {
                DrainWriteBuffer();
                m_WriteBufferStart = m_Position;
            }
            Buffer.BlockCopy(buffer, offset, m_WriteBuffer, m_WriteBufferLength, count);
            m_WriteBufferLength += count;
            Advance(count);
        }

        public override void WriteByte(byte value)
        {
            Write(new[] { value }, 0, 1);
        }

        public override void SetLength(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            ThrowIfClosed();
            if (!m_CanWrite)
            {
                throw new NotSupportedException("The SAF document is open for reading only.");
            }
            DrainWriteBuffer();
            m_ReadBufferLength = 0;
            if (!m_Channel.Truncate(value))
            {
                throw new IOException(DescribeError());
            }
            m_Length = value;
            if (m_Position > m_Length)
            {
                m_Position = m_Length;
            }
        }

        public override void Flush()
        {
            FlushCore(toDisk: false);
        }

        public void FlushToDisk()
        {
            FlushCore(toDisk: true);
        }

        protected override void Dispose(bool disposing)
        {
            ISafDocumentChannel channel = m_Channel;
            if (channel == null)
            {
                return;
            }
            try
            {
                if (disposing)
                {
                    DrainWriteBuffer();
                }
            }
            finally
            {
                m_Channel = null;
                m_ReadBuffer = null;
                m_WriteBuffer = null;
                m_ReadBufferLength = 0;
                m_WriteBufferLength = 0;
                channel.Close();
            }
            base.Dispose(disposing);
        }

        private int ReadOnce(byte[] buffer, int offset, int count)
        {
            if (m_ReadBufferLength > 0 &&
                m_Position >= m_ReadBufferStart &&
                m_Position < m_ReadBufferStart + m_ReadBufferLength)
            {
                int available = (int)(m_ReadBufferStart + m_ReadBufferLength - m_Position);
                int served = Math.Min(available, count);
                Buffer.BlockCopy(
                    m_ReadBuffer, (int)(m_Position - m_ReadBufferStart),
                    buffer, offset, served);
                m_Position += served;
                return served;
            }

            m_ReadBufferLength = 0;
            // A read large enough to pay for its own crossing skips the buffer, so a bulk copy
            // does not also pay for a read-ahead it will never look at again.
            sbyte[] block = ReadBlock(m_Position, Math.Max(count, kReadBufferSize));
            if (block.Length == 0)
            {
                return 0;
            }
            int taken = Math.Min(block.Length, count);
            Buffer.BlockCopy(block, 0, buffer, offset, taken);
            m_Position += taken;
            if (block.Length > taken)
            {
                m_ReadBuffer = block;
                m_ReadBufferStart = m_Position - taken;
                m_ReadBufferLength = block.Length;
            }
            return taken;
        }

        private void FlushCore(bool toDisk)
        {
            if (m_Channel == null)
            {
                return;
            }
            DrainWriteBuffer();
            if (m_CanWrite && !m_Channel.Flush(toDisk))
            {
                throw new IOException(DescribeError());
            }
        }

        private void Advance(int count)
        {
            m_Position += count;
            if (m_Position > m_Length)
            {
                m_Length = m_Position;
            }
        }

        private void DrainWriteBuffer()
        {
            if (m_WriteBufferLength == 0)
            {
                return;
            }
            int length = m_WriteBufferLength;
            long start = m_WriteBufferStart;
            // Cleared first: a failed write must not be retried by the next drain, or a Dispose
            // in a finally block would throw over whatever error is already unwinding.
            m_WriteBufferLength = 0;
            WriteThrough(start, m_WriteBuffer, 0, length);
        }

        private sbyte[] ReadBlock(long position, int count)
        {
            sbyte[] block = m_Channel.Read(position, count);
            if (block == null)
            {
                throw new IOException(DescribeError());
            }
            return block;
        }

        private void WriteThrough(long position, Array buffer, int offset, int count)
        {
            sbyte[] payload;
            if (offset == 0 && buffer is sbyte[] alreadySigned)
            {
                payload = alreadySigned;
            }
            else
            {
                payload = new sbyte[count];
                Buffer.BlockCopy(buffer, offset, payload, 0, count);
            }
            int written = m_Channel.Write(position, payload, count);
            if (written < 0)
            {
                throw new IOException(DescribeError());
            }
            if (written != count)
            {
                throw new IOException(
                    $"The shared document accepted {written} of {count} bytes.");
            }
        }

        private string DescribeError()
        {
            string error = m_Channel?.DescribeLastError();
            return string.IsNullOrEmpty(error)
                ? "The shared document could not be accessed."
                : error;
        }

        private void ThrowIfClosed()
        {
            if (m_Channel == null)
            {
                throw new ObjectDisposedException(nameof(SafDocumentStream));
            }
        }

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || buffer.Length - offset < count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }
    }

    /// The channel table entry in OpenBrushStorageBridge, addressed by handle.
    internal sealed class SafJniDocumentChannel : ISafDocumentChannel
    {
        private const string kBridgeClass =
            "foundation.icosa.openbrush.storage.OpenBrushStorageBridge";

        private static readonly object sm_BridgeGate = new object();
        private static AndroidJavaClass sm_Bridge;

        private int m_Handle;

        internal SafJniDocumentChannel(int handle)
        {
            m_Handle = handle;
        }

        public sbyte[] Read(long position, int count)
        {
            AndroidSafStorage.AttachToJvmIfNeeded();
            return Bridge.CallStatic<sbyte[]>("readChannel", m_Handle, position, count);
        }

        public int Write(long position, sbyte[] data, int count)
        {
            AndroidSafStorage.AttachToJvmIfNeeded();
            if (count != data.Length)
            {
                // Java sees whole arrays, so a partly filled buffer has to be trimmed first.
                var exact = new sbyte[count];
                Buffer.BlockCopy(data, 0, exact, 0, count);
                data = exact;
            }
            return Bridge.CallStatic<int>("writeChannel", m_Handle, position, data);
        }

        public bool Truncate(long length)
        {
            AndroidSafStorage.AttachToJvmIfNeeded();
            return Bridge.CallStatic<bool>("truncateChannel", m_Handle, length);
        }

        public bool Flush(bool toDisk)
        {
            AndroidSafStorage.AttachToJvmIfNeeded();
            return Bridge.CallStatic<bool>("flushChannel", m_Handle, toDisk);
        }

        public string DescribeLastError()
        {
            AndroidSafStorage.AttachToJvmIfNeeded();
            return Bridge.CallStatic<string>("channelError", m_Handle);
        }

        public void Close()
        {
            if (m_Handle < 0)
            {
                return;
            }
            int handle = m_Handle;
            m_Handle = -1;
            AndroidSafStorage.AttachToJvmIfNeeded();
            Bridge.CallStatic("closeChannel", handle);
        }

        private static AndroidJavaClass Bridge
        {
            get
            {
                // One global reference for the life of the process. Streams are opened often
                // enough - every glTF buffer, every Lua module - that a per-call class lookup
                // shows up next to the read itself.
                if (sm_Bridge != null)
                {
                    return sm_Bridge;
                }
                lock (sm_BridgeGate)
                {
                    sm_Bridge ??= new AndroidJavaClass(kBridgeClass);
                    return sm_Bridge;
                }
            }
        }
    }
}
