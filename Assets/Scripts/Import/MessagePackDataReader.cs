// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Data-only MessagePack decoder. No CLR type activation or third-party application code.
    /// Wire reference: https://github.com/msgpack/msgpack/blob/master/spec.md
    internal sealed class MessagePackDataReader
    {
        private readonly byte[] m_Data;
        private int m_Position;
        private static readonly Encoding s_Utf8 = new UTF8Encoding(false, true);

        private MessagePackDataReader(byte[] data) { m_Data = data; }

        public static JToken Decode(byte[] data)
        {
            var reader = new MessagePackDataReader(data ?? throw new ArgumentNullException(nameof(data)));
            var value = reader.Read(0);
            if (reader.m_Position != data.Length) { throw new InvalidDataException("Trailing MessagePack data"); }
            return value;
        }

        private JToken Read(int depth)
        {
            if (depth > 128) { throw new InvalidDataException("MessagePack nesting exceeds 128 levels"); }
            byte code = Byte();
            if (code <= 0x7f) { return new JValue((int)code); }
            if (code >= 0xe0) { return new JValue(unchecked((sbyte)code)); }
            if ((code & 0xe0) == 0xa0) { return new JValue(Text(code & 0x1f)); }
            if ((code & 0xf0) == 0x90) { return Array(code & 0xf, depth); }
            if ((code & 0xf0) == 0x80) { return Map(code & 0xf, depth); }
            switch (code)
            {
                case 0xc0: return JValue.CreateNull();
                case 0xc2: return new JValue(false);
                case 0xc3: return new JValue(true);
                case 0xc4: return new JValue(Bytes(Length(1)));
                case 0xc5: return new JValue(Bytes(Length(2)));
                case 0xc6: return new JValue(Bytes(Length(4)));
                case 0xc7: return Extension(Length(1));
                case 0xc8: return Extension(Length(2));
                case 0xc9: return Extension(Length(4));
                case 0xca: return new JValue(BitConverter.Int32BitsToSingle(unchecked((int)Unsigned(4))));
                case 0xcb: return new JValue(BitConverter.Int64BitsToDouble(unchecked((long)Unsigned(8))));
                case 0xcc: return new JValue(Unsigned(1));
                case 0xcd: return new JValue(Unsigned(2));
                case 0xce: return new JValue(Unsigned(4));
                case 0xcf: return new JValue(Unsigned(8));
                case 0xd0: return new JValue(unchecked((sbyte)Unsigned(1)));
                case 0xd1: return new JValue(unchecked((short)Unsigned(2)));
                case 0xd2: return new JValue(unchecked((int)Unsigned(4)));
                case 0xd3: return new JValue(unchecked((long)Unsigned(8)));
                case 0xd4: return Extension(1);
                case 0xd5: return Extension(2);
                case 0xd6: return Extension(4);
                case 0xd7: return Extension(8);
                case 0xd8: return Extension(16);
                case 0xd9: return new JValue(Text(Length(1)));
                case 0xda: return new JValue(Text(Length(2)));
                case 0xdb: return new JValue(Text(Length(4)));
                case 0xdc: return Array(Length(2), depth);
                case 0xdd: return Array(Length(4), depth);
                case 0xde: return Map(Length(2), depth);
                case 0xdf: return Map(Length(4), depth);
                default: throw new InvalidDataException($"Invalid MessagePack code: 0x{code:x2}");
            }
        }

        private JArray Array(int count, int depth)
        {
            Require(count); // Even nil elements require one byte each.
            var result = new JArray();
            for (int i = 0; i < count; ++i) { result.Add(Read(depth + 1)); }
            return result;
        }

        private JArray Map(int count, int depth)
        {
            // Keep typed keys and ordering rather than coercing arbitrary MessagePack keys to strings.
            if (count > (m_Data.Length - m_Position) / 2) { throw new EndOfStreamException("Truncated MessagePack map"); }
            var entries = new JArray();
            for (int i = 0; i < count; ++i) { entries.Add(new JArray(Read(depth + 1), Read(depth + 1))); }
            return new JArray("MessagePackMap", entries);
        }

        private JObject Extension(int count)
        {
            int type = unchecked((sbyte)Byte());
            return new JObject { ["MessagePackExtensionType"] = type, ["Data"] = new JValue(Bytes(count)) };
        }

        private ulong Unsigned(int count)
        {
            Require(count);
            ulong value = 0;
            for (int i = 0; i < count; ++i) { value = (value << 8) | m_Data[m_Position++]; }
            return value;
        }

        private int Length(int count)
        {
            ulong length = Unsigned(count);
            if (length > int.MaxValue) { throw new InvalidDataException("MessagePack length exceeds supported range"); }
            return (int)length;
        }

        private byte Byte() { Require(1); return m_Data[m_Position++]; }

        private byte[] Bytes(int count)
        {
            Require(count);
            var bytes = new byte[count];
            Buffer.BlockCopy(m_Data, m_Position, bytes, 0, count);
            m_Position += count;
            return bytes;
        }

        private string Text(int count)
        {
            Require(count);
            string text = s_Utf8.GetString(m_Data, m_Position, count);
            m_Position += count;
            return text;
        }

        private void Require(int count)
        {
            if (count < 0 || count > m_Data.Length - m_Position) { throw new EndOfStreamException("Truncated MessagePack value"); }
        }
    }
}
