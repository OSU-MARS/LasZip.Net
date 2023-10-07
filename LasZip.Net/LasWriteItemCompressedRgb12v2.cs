// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasWriteItemCompressedRgb12v2 : LasWriteItemCompressed
    {
        public ArithmeticEncoder enc;
        public UInt16[] lastItem = new UInt16[3];

        public ArithmeticModel byteUsed;
        public ArithmeticModel rgbDiff0;
        public ArithmeticModel rgbDiff1;
        public ArithmeticModel rgbDiff2;
        public ArithmeticModel rgbDiff3;
        public ArithmeticModel rgbDiff4;
        public ArithmeticModel rgbDiff5;

        public LasWriteItemCompressedRgb12v2(ArithmeticEncoder enc)
        {
            // set encoder
            Debug.Assert(enc != null);
            this.enc = enc;

            // create models and integer compressors
            byteUsed = ArithmeticEncoder.CreateSymbolModel(128);
            rgbDiff0 = ArithmeticEncoder.CreateSymbolModel(256);
            rgbDiff1 = ArithmeticEncoder.CreateSymbolModel(256);
            rgbDiff2 = ArithmeticEncoder.CreateSymbolModel(256);
            rgbDiff3 = ArithmeticEncoder.CreateSymbolModel(256);
            rgbDiff4 = ArithmeticEncoder.CreateSymbolModel(256);
            rgbDiff5 = ArithmeticEncoder.CreateSymbolModel(256);
        }

        public override bool Init(LasPoint item)
        {
            // init state

            // init models and integer compressors
            ArithmeticEncoder.InitSymbolModel(byteUsed);
            ArithmeticEncoder.InitSymbolModel(rgbDiff0);
            ArithmeticEncoder.InitSymbolModel(rgbDiff1);
            ArithmeticEncoder.InitSymbolModel(rgbDiff2);
            ArithmeticEncoder.InitSymbolModel(rgbDiff3);
            ArithmeticEncoder.InitSymbolModel(rgbDiff4);
            ArithmeticEncoder.InitSymbolModel(rgbDiff5);

            // init last item
            Buffer.BlockCopy(item.Rgb, 0, lastItem, 0, 6);
            return true;
        }

        public override bool Write(LasPoint item)
        {
            int diff_l = 0;
            int diff_h = 0;

            UInt32 sym = 0;

            bool rl = (lastItem[0] & 0x00FF) != (item.Rgb[0] & 0x00FF); if (rl) sym |= 1;
            bool rh = (lastItem[0] & 0xFF00) != (item.Rgb[0] & 0xFF00); if (rh) sym |= 2;
            bool gl = (lastItem[1] & 0x00FF) != (item.Rgb[1] & 0x00FF); if (gl) sym |= 4;
            bool gh = (lastItem[1] & 0xFF00) != (item.Rgb[1] & 0xFF00); if (gh) sym |= 8;
            bool bl = (lastItem[2] & 0x00FF) != (item.Rgb[2] & 0x00FF); if (bl) sym |= 16;
            bool bh = (lastItem[2] & 0xFF00) != (item.Rgb[2] & 0xFF00); if (bh) sym |= 32;

            bool allColors = ((item.Rgb[0] & 0x00FF) != (item.Rgb[1] & 0x00FF)) || ((item.Rgb[0] & 0x00FF) != (item.Rgb[2] & 0x00FF)) ||
                ((item.Rgb[0] & 0xFF00) != (item.Rgb[1] & 0xFF00)) || ((item.Rgb[0] & 0xFF00) != (item.Rgb[2] & 0xFF00));
            if (allColors) sym |= 64;

            enc.EncodeSymbol(byteUsed, sym);
            if (rl)
            {
                diff_l = ((int)(item.Rgb[0] & 255)) - (lastItem[0] & 255);
                enc.EncodeSymbol(rgbDiff0, (byte)MyDefs.FoldUint8(diff_l));
            }
            if (rh)
            {
                diff_h = ((int)(item.Rgb[0] >> 8)) - (lastItem[0] >> 8);
                enc.EncodeSymbol(rgbDiff1, (byte)MyDefs.FoldUint8(diff_h));
            }

            if (allColors)
            {
                if (gl)
                {
                    int corr = ((int)(item.Rgb[1] & 255)) - MyDefs.ClampUint8(diff_l + (lastItem[1] & 255));
                    enc.EncodeSymbol(rgbDiff2, (byte)MyDefs.FoldUint8(corr));
                }
                if (bl)
                {
                    diff_l = (diff_l + (item.Rgb[1] & 255) - (lastItem[1] & 255)) / 2;
                    int corr = ((int)(item.Rgb[2] & 255)) - MyDefs.ClampUint8(diff_l + (lastItem[2] & 255));
                    enc.EncodeSymbol(rgbDiff4, (byte)MyDefs.FoldUint8(corr));
                }
                if (gh)
                {
                    int corr = ((int)(item.Rgb[1] >> 8)) - MyDefs.ClampUint8(diff_h + (lastItem[1] >> 8));
                    enc.EncodeSymbol(rgbDiff3, (byte)MyDefs.FoldUint8(corr));
                }
                if (bh)
                {
                    diff_h = (diff_h + (item.Rgb[1] >> 8) - (lastItem[1] >> 8)) / 2;
                    int corr = ((int)(item.Rgb[2] >> 8)) - MyDefs.ClampUint8(diff_h + (lastItem[2] >> 8));
                    enc.EncodeSymbol(rgbDiff5, (byte)MyDefs.FoldUint8(corr));
                }
            }

            lastItem[0] = item.Rgb[0];
            lastItem[1] = item.Rgb[1];
            lastItem[2] = item.Rgb[2];

            return true;
        }
    }
}
