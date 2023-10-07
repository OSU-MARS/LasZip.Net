// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedRgb12v1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private UInt16 r, g, b;

        private readonly ArithmeticModel byteUsed;
        private readonly IntegerCompressor icRgb;

        public LasReadItemCompressedRgb12v1(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;

            // create models and integer compressors
            byteUsed = ArithmeticDecoder.CreateSymbolModel(64);
            icRgb = new IntegerCompressor(dec, 8, 6);
        }

        public override bool Init(LasPoint item)
        {
            // init state

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(byteUsed);
            icRgb.InitDecompressor();

            // init last item
            r = item.Rgb[0];
            g = item.Rgb[1];
            b = item.Rgb[2];

            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            UInt32 sym = dec.DecodeSymbol(byteUsed);

            UInt16[] item16 = item.Rgb;

            if ((sym & (1 << 0)) != 0) item16[0] = (UInt16)icRgb.Decompress(r & 255, 0);
            else item16[0] = (UInt16)(r & 0xFF);

            if ((sym & (1 << 1)) != 0) item16[0] |= (UInt16)(((UInt16)icRgb.Decompress(r >> 8, 1)) << 8);
            else item16[0] |= (UInt16)(r & 0xFF00);

            if ((sym & (1 << 2)) != 0) item16[1] = (UInt16)icRgb.Decompress(g & 255, 2);
            else item16[1] = (UInt16)(g & 0xFF);

            if ((sym & (1 << 3)) != 0) item16[1] |= (UInt16)(((UInt16)icRgb.Decompress(g >> 8, 3)) << 8);
            else item16[1] |= (UInt16)(g & 0xFF00);

            if ((sym & (1 << 4)) != 0) item16[2] = (UInt16)icRgb.Decompress(b & 255, 4);
            else item16[2] = (UInt16)(b & 0xFF);

            if ((sym & (1 << 5)) != 0) item16[2] |= (UInt16)(((UInt16)icRgb.Decompress(b >> 8, 5)) << 8);
            else item16[2] |= (UInt16)(b & 0xFF00);

            r = item16[0];
            g = item16[1];
            b = item16[2];

            return true;
        }
    }
}
