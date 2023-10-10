// bytestreamin_file.hpp
using System;
using System.IO;

namespace LasZip
{
    internal class ByteStreamInFileBE : ByteStreamInFile
    {
        public ByteStreamInFileBE(FileStream file)
            : base(file)
        {
        }

        public override void Get16bitsLE(Span<byte> bytes)
        {
            Span<byte> swapped = stackalloc byte[2];
            this.GetBytes(swapped, 2);
            bytes[0] = swapped[1];
            bytes[1] = swapped[0];
        }

        public override void Get32bitsLE(Span<byte> bytes)
        {
            Span<byte> swapped = stackalloc byte[4];
            this.GetBytes(swapped, 4);
            bytes[0] = swapped[3];
            bytes[1] = swapped[2];
            bytes[2] = swapped[1];
            bytes[3] = swapped[0];
        }

        public override void Get64bitsLE(Span<byte> bytes)
        {
            Span<byte> swapped = stackalloc byte[8];
            this.GetBytes(swapped, 8);
            bytes[0] = swapped[7];
            bytes[1] = swapped[6];
            bytes[2] = swapped[5];
            bytes[3] = swapped[4];
            bytes[4] = swapped[3];
            bytes[5] = swapped[2];
            bytes[6] = swapped[1];
            bytes[7] = swapped[0];
        }

        public override void Get16bitsBE(Span<byte> bytes)
        {
            this.GetBytes(bytes, 2);
        }

        public override void Get32bitsBE(Span<byte> bytes)
        {
            this.GetBytes(bytes, 4);
        }

        public override void Get64bitsBE(Span<byte> bytes)
        {
            this.GetBytes(bytes, 8);
        }
    }
}
