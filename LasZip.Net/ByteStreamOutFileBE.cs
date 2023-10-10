// bytestreamout_file.hpp
using System;
using System.IO;

namespace LasZip
{
    internal class ByteStreamOutFileBE : ByteStreamOutFile
    {
        public ByteStreamOutFileBE(FileStream file)
            : base(file)
        {
        }

        public override bool Put16bitsLE(ReadOnlySpan<byte> bytes)
        {
            Span<byte> swapped = new byte[2];
            swapped[0] = bytes[1];
            swapped[1] = bytes[0];
            return PutBytes(swapped, 2);
        }

        public override bool Put32bitsLE(ReadOnlySpan<byte> bytes)
        {
            Span<byte> swapped = new byte[4];
            swapped[0] = bytes[3];
            swapped[1] = bytes[2];
            swapped[2] = bytes[1];
            swapped[3] = bytes[0];
            return PutBytes(swapped, 4);
        }

        public override bool Put64bitsLE(ReadOnlySpan<byte> bytes)
        {
            Span<byte> swapped = new byte[4];
            swapped[0] = bytes[7];
            swapped[1] = bytes[6];
            swapped[2] = bytes[5];
            swapped[3] = bytes[4];
            swapped[4] = bytes[3];
            swapped[5] = bytes[2];
            swapped[6] = bytes[1];
            swapped[7] = bytes[0];
            return PutBytes(swapped, 8);
        }

        public override bool Put16bitsBE(ReadOnlySpan<byte> bytes)
        {
            return PutBytes(bytes, 2);
        }

        public override bool Put32bitsBE(ReadOnlySpan<byte> bytes)
        {
            return PutBytes(bytes, 4);
        }

        public override bool Put64bitsBE(ReadOnlySpan<byte> bytes)
        {
            return PutBytes(bytes, 8);
        }

    }
}
