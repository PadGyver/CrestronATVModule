using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
namespace AndroidTvLib
{
    public static class ProtoWriter
    {
        public static byte[] Frame(byte[] payload)
        {
            var lenBytes = WriteVarint((ulong)payload.Length);
            var result = new byte[lenBytes.Length + payload.Length];
            Buffer.BlockCopy(lenBytes, 0, result, 0, lenBytes.Length);
            Buffer.BlockCopy(payload, 0, result, lenBytes.Length, payload.Length);
            return result;
        }

        public static byte[] WriteVarint(ulong value)
        {
            var bytes = new List<byte>();
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                bytes.Add(b);
            } while (value != 0);
            return bytes.ToArray();
        }

        public static void WriteTag(MemoryStream ms, int fieldNum, int wireType)
        {
            int tag = (fieldNum << 3) | wireType;
            var b = WriteVarint((ulong)tag);
            ms.Write(b, 0, b.Length);
        }

        public static void WriteVarintField(MemoryStream ms, int fieldNum, ulong value)
        {
            WriteTag(ms, fieldNum, 0);
            var b = WriteVarint(value);
            ms.Write(b, 0, b.Length);
        }

        public static void WriteBytesField(MemoryStream ms, int fieldNum, byte[] data)
        {
            WriteTag(ms, fieldNum, 2);
            var lenB = WriteVarint((ulong)data.Length);
            ms.Write(lenB, 0, lenB.Length);
            ms.Write(data, 0, data.Length);
        }

        public static void WriteStringField(MemoryStream ms, int fieldNum, string s)
            => WriteBytesField(ms, fieldNum, Encoding.UTF8.GetBytes(s));
    }

    public static class ProtoReader
    {
        public static ulong ReadVarint(byte[] data, ref int pos)
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                byte b = data[pos++];
                result |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }
    }
}