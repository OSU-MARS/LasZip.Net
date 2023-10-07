// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedByteV2 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private readonly UInt32 number;
        private readonly byte[] lastItem;

        private readonly ArithmeticModel[] mByte;

        public LasReadItemCompressedByteV2(ArithmeticDecoder dec, UInt32 number)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;
            Debug.Assert(number > 0);
            this.number = number;

            // create models and integer compressors
            mByte = new ArithmeticModel[number];
            for (UInt32 i = 0; i < number; i++)
            {
                mByte[i] = ArithmeticDecoder.CreateSymbolModel(256);
            }

            // create last item
            lastItem = new byte[number];
        }

        public override bool Init(LasPoint item)
        {
            // init state

            // init models and integer compressors
            for (UInt32 i = 0; i < number; i++)
            {
                ArithmeticDecoder.InitSymbolModel(mByte[i]);
            }

            // init last item
            Buffer.BlockCopy(item.ExtraBytes, 0, lastItem, 0, (int)number);

            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            for (UInt32 i = 0; i < number; i++)
            {
                int value = (int)(lastItem[i] + dec.DecodeSymbol(mByte[i]));
                item.ExtraBytes[i] = (byte)MyDefs.FoldUint8(value);
            }

            Buffer.BlockCopy(item.ExtraBytes, 0, lastItem, 0, (int)number);
            return true;
        }
    }
}
