// bytestreamout.hpp
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal abstract class ByteStreamOut
    {
        private UInt64 bit_buffer;
        private UInt32 num_buffer;

        public ByteStreamOut()
        {
            bit_buffer = 0;
            num_buffer = 0;
        }

        // write single bits
        public bool PutBits(UInt32 bits, UInt32 num_bits)
        {
            UInt64 new_bits = bits;
            bit_buffer |= (new_bits << (int)num_buffer);
            num_buffer += num_bits;
            if (num_buffer >= 32)
            {
                UInt32 output_bits = (UInt32)(bit_buffer & UInt32.MaxValue);
                bit_buffer = bit_buffer >> 32;
                num_buffer = num_buffer - 32;

                Span<byte> outputBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(outputBytes, output_bits);
                return Put32bitsLE(outputBytes);
            }
            return true;
        }

        // called after writing bits before closing or writing bytes
        public bool FlushBits()
        {
            if (num_buffer != 0)
            {
                UInt32 num_zero_bits = 32 - num_buffer;
                UInt32 output_bits = (UInt32)(bit_buffer >> (int)num_zero_bits);
                bit_buffer = 0;
                num_buffer = 0;

                Span<byte> outputBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(outputBytes, output_bits);
                return Put32bitsLE(outputBytes);
            }
            return true;
        }

        // write a single byte
        public abstract bool PutByte(byte value);
        // write an array of bytes
        public abstract bool PutBytes(ReadOnlySpan<byte> bytes, UInt32 num_bytes);
        // write 16 bit low-endian field
        public abstract bool Put16bitsLE(ReadOnlySpan<byte> bytes);
        
        // write 32 bit low-endian field
        public abstract bool Put32bitsLE(ReadOnlySpan<byte> bytes);

        public bool Put32bitsLE(float value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
            return this.Put32bitsLE(bytes);
        }

        public bool Put32bitsLE(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            return this.Put32bitsLE(bytes);
        }

        public bool Put32bitsLE(UInt32 value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            return this.Put32bitsLE(bytes);
        }
        
        // write 64 bit low-endian field
        public abstract bool Put64bitsLE(ReadOnlySpan<byte> bytes);
        // write 16 bit big-endian field
        public abstract bool Put16bitsBE(ReadOnlySpan<byte> bytes);
        // write 32 bit big-endian field
        public abstract bool Put32bitsBE(ReadOnlySpan<byte> bytes);
        // write 64 bit big-endian field
        public abstract bool Put64bitsBE(ReadOnlySpan<byte> bytes);
        // is the stream seekable (e.g. standard out is not)
        public abstract bool IsSeekable();
        // get current position of stream
        public abstract long Tell();
        // seek to this position in the stream
        public abstract bool Seek(long position);
        // seek to the end of the file
        public abstract bool SeekEnd();
    }
}
