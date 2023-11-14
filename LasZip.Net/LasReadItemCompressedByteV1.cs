// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedByteV1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private readonly int number;
        private readonly byte[] lastItem;

        private readonly IntegerCompressor icByte;

        public LasReadItemCompressedByteV1(ArithmeticDecoder decoder, UInt32 number)
        {
            Debug.Assert(number > 0);

            this.decoder = decoder;
            this.number = (int)number;

            // create models and integer compressors
            this.icByte = new IntegerCompressor(decoder, 8, number);

            // create last item
            this.lastItem = new byte[number];
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            // init models and integer compressors
            this.icByte.InitDecompressor();

            // init last item
            item[..this.number].CopyTo(this.lastItem);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            for (int index = 0; index < this.number; index++)
            {
                item[index] = (byte)icByte.Decompress(lastItem[index], (UInt32)index);
            }

            return true;
        }
    }
}
