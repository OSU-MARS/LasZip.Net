using System;
using System.Buffers.Binary;
using System.IO;

namespace LasZip.Extensions
{
    internal static class StreamExtensions
    {
        public static bool Put32bitsLE(this Stream stream, UInt32 value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
            stream.Write(bytes);
            return true;
        }

        public static bool Put64bitsLE(this Stream stream, double value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
            stream.Write(bytes);
            return true;
        }
    }
}
