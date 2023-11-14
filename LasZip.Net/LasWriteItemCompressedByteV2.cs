// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    class LasWriteItemCompressedByteV2 : LasWriteItemCompressed
    {
        private readonly ArithmeticEncoder encoder;
        private readonly int number;
        private readonly byte[] lastItem;

        private readonly ArithmeticModel[] byteModel;

        public LasWriteItemCompressedByteV2(ArithmeticEncoder encoder, UInt32 number)
        {
            Debug.Assert(number > 0);

            this.encoder = encoder;
            this.number = (int)number;

            // create models and integer compressors
            byteModel = new ArithmeticModel[number];
            for (UInt32 i = 0; i < number; i++)
            {
                byteModel[i] = ArithmeticEncoder.CreateSymbolModel(256);
            }

            // create last item
            lastItem = new byte[number];
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state

            // init models and integer compressors
            for (UInt32 i = 0; i < number; i++)
            {
                ArithmeticEncoder.InitSymbolModel(byteModel[i]);
            }

            // init last item
            item[..this.number].CopyTo(this.lastItem);

            return true;
        }

        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            for (int i = 0; i < number; i++)
            {
                int diff = item[i] - this.lastItem[i];
                this.encoder.EncodeSymbol(this.byteModel[i], (byte)MyDefs.FoldUInt8(diff));
            }

            item[..this.number].CopyTo(this.lastItem);
            return true;
        }
    }
}
