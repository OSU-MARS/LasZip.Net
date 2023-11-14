using System;
using System.Buffers.Binary;
using System.IO;

namespace LasZip.Extensions
{
    internal static class StreamExtensions
    {
        public static Int32 ReadInt32LittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }

        public static Int64 ReadInt64LittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[8];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadInt64LittleEndian(bytes);
        }

        public static double ReadDoubleLittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[8];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadDoubleLittleEndian(bytes);
        }

        public static float ReadSingleLittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadSingleLittleEndian(bytes);
        }

        public static UInt16 ReadUInt16LittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[2];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        }

        public static UInt32 ReadUInt32LittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }

        public static UInt64 ReadUInt64LittleEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[8];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, double value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
            stream.Write(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, Int16 value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, Int32 value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, Int64 value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, UInt16 value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, UInt32 value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        public static void WriteLittleEndian(this Stream stream, UInt64 value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            stream.Write(bytes);
        }
    }
}
