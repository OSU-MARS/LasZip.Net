// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedByteV2 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private readonly int number;
        private readonly byte[] lastItem;

        private readonly ArithmeticModel[] mByte;

        public LasReadItemCompressedByteV2(ArithmeticDecoder decoder, UInt32 number)
        {
            Debug.Assert(number > 0);
            this.decoder = decoder;
            this.number = (int)number;

            // create models and integer compressors
            this.mByte = new ArithmeticModel[number];
            for (UInt32 i = 0; i < number; i++)
            {
                this.mByte[i] = ArithmeticDecoder.CreateSymbolModel(256);
            }

            // create last item
            this.lastItem = new byte[number];
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state

            // init models and integer compressors
            for (UInt32 i = 0; i < number; i++)
            {
                ArithmeticDecoder.InitSymbolModel(mByte[i]);
            }

            // init last item
            item[..this.number].CopyTo(this.lastItem);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            for (int i = 0; i < number; i++)
            {
                int value = (int)(lastItem[i] + decoder.DecodeSymbol(mByte[i]));
                item[i] = (byte)MyDefs.FoldUInt8(value);
            }

            item[..this.number].CopyTo(this.lastItem);
            return true;
        }
    }
}
