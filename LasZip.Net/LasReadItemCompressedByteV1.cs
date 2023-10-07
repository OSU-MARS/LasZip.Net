// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedByteV1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private readonly UInt32 number;
        private readonly byte[] lastItem;

        private readonly IntegerCompressor icByte;

        public LasReadItemCompressedByteV1(ArithmeticDecoder dec, UInt32 number)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;
            Debug.Assert(number != 0);
            this.number = number;

            // create models and integer compressors
            icByte = new IntegerCompressor(dec, 8, number);

            // create last item
            lastItem = new byte[number];
        }

        public override bool Init(LasPoint item)
        {
            if (item.ExtraBytes == null)
            {
                throw new InvalidOperationException();
            }

            // init state

            // init models and integer compressors
            icByte.InitDecompressor();

            // init last item
            Buffer.BlockCopy(item.ExtraBytes, 0, lastItem, 0, (int)number);

            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            for (UInt32 i = 0; i < this.number; i++)
            {
                lastItem[i] = item.ExtraBytes[i] = (byte)(icByte.Decompress(lastItem[i], i));
            }

            return true;
        }
    }
}
