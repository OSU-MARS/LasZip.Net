// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    class LasWriteItemCompressedByteV2 : LasWriteItemCompressed
    {
        private readonly ArithmeticEncoder enc;
        private readonly UInt32 number;
        private readonly byte[] lastItem;

        private readonly ArithmeticModel[] byteModel;

        public LasWriteItemCompressedByteV2(ArithmeticEncoder enc, UInt32 number)
        {
            // set encoder
            Debug.Assert(enc != null);
            this.enc = enc;
            Debug.Assert(number > 0);
            this.number = number;

            // create models and integer compressors
            byteModel = new ArithmeticModel[number];
            for (UInt32 i = 0; i < number; i++)
            {
                byteModel[i] = ArithmeticEncoder.CreateSymbolModel(256);
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
                ArithmeticEncoder.InitSymbolModel(byteModel[i]);
            }

            // init last item
            Buffer.BlockCopy(item.ExtraBytes, 0, lastItem, 0, (int)number);

            return true;
        }

        public override bool Write(LasPoint item)
        {
            for (UInt32 i = 0; i < number; i++)
            {
                int diff = item.ExtraBytes[i] - lastItem[i];
                enc.EncodeSymbol(byteModel[i], (byte)MyDefs.FoldUint8(diff));
            }

            Buffer.BlockCopy(item.ExtraBytes, 0, lastItem, 0, (int)number);
            return true;
        }
    }
}
