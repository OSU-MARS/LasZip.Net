// bytestreamin.hpp
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal abstract class ByteStreamIn
    {
        private UInt64 bit_buffer;
        private UInt32 num_buffer;

        public ByteStreamIn()
        {
            bit_buffer = 0;
            num_buffer = 0;
        }

        // read single bits
        public UInt32 GetBits(UInt32 num_bits)
        {
            // TODO: argument and alignment checking?
            if (num_buffer < num_bits)
            {
                Span<byte> inputBytes = stackalloc byte[4];
                Get32bitsLE(inputBytes);
                UInt32 input_bits = BinaryPrimitives.ReadUInt32LittleEndian(inputBytes);
                bit_buffer = bit_buffer | (((UInt64)input_bits) << (int)num_buffer);
                num_buffer = num_buffer + 32;
            }

            UInt32 new_bits = (UInt32)(bit_buffer & ((1U << (int)num_bits) - 1));
            bit_buffer = bit_buffer >> (int)num_bits;
            num_buffer = num_buffer - num_bits;
            return new_bits;
        }

        // TODO: change to normal C# serialization pattern rather than requiring callers to constantly marshall bytes
        // read a single byte
        public abstract UInt32 GetByte();
        // read an array of bytes
        public abstract void GetBytes(Span<byte> bytes, int num_bytes);
        // read 16 bit low-endian field
        public abstract void Get16bitsLE(Span<byte> bytes);
        // read 32 bit low-endian field
        public abstract void Get32bitsLE(Span<byte> bytes);
        // read 64 bit low-endian field
        public abstract void Get64bitsLE(Span<byte> bytes);
        // read 16 bit big-endian field
        public abstract void Get16bitsBE(Span<byte> bytes);
        // read 32 bit big-endian field
        public abstract void Get32bitsBE(Span<byte> bytes);
        // read 64 bit big-endian field
        public abstract void Get64bitsBE(Span<byte> bytes);
        // is the stream seekable (e.g. stdin is not)
        public abstract bool IsSeekable();
        // get current position of stream
        public abstract long Tell();
        // seek to this position in the stream
        public abstract bool Seek(long position);
        // seek to the end of the file
        public abstract bool SeekEnd(long distance);
    }
}
