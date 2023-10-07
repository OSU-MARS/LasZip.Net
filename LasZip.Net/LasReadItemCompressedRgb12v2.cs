// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedRgb12v2 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private readonly UInt16[] lastItem = new UInt16[3];

        private readonly ArithmeticModel byteUsed;
        private readonly ArithmeticModel rgbDiff0;
        private readonly ArithmeticModel rgbDiff1;
        private readonly ArithmeticModel rgbDiff2;
        private readonly ArithmeticModel rgbDiff3;
        private readonly ArithmeticModel rgbDiff4;
        private readonly ArithmeticModel rgbDiff5;

        public LasReadItemCompressedRgb12v2(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;

            // create models and integer compressors
            byteUsed = ArithmeticDecoder.CreateSymbolModel(128);
            rgbDiff0 = ArithmeticDecoder.CreateSymbolModel(256);
            rgbDiff1 = ArithmeticDecoder.CreateSymbolModel(256);
            rgbDiff2 = ArithmeticDecoder.CreateSymbolModel(256);
            rgbDiff3 = ArithmeticDecoder.CreateSymbolModel(256);
            rgbDiff4 = ArithmeticDecoder.CreateSymbolModel(256);
            rgbDiff5 = ArithmeticDecoder.CreateSymbolModel(256);
        }

        public override bool Init(LasPoint item)
        {
            // init state

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(byteUsed);
            ArithmeticDecoder.InitSymbolModel(rgbDiff0);
            ArithmeticDecoder.InitSymbolModel(rgbDiff1);
            ArithmeticDecoder.InitSymbolModel(rgbDiff2);
            ArithmeticDecoder.InitSymbolModel(rgbDiff3);
            ArithmeticDecoder.InitSymbolModel(rgbDiff4);
            ArithmeticDecoder.InitSymbolModel(rgbDiff5);

            // init last item
            Buffer.BlockCopy(item.Rgb, 0, lastItem, 0, 6);
            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            int corr;
            int diff = 0;

            UInt32 sym = dec.DecodeSymbol(byteUsed);
            if ((sym & (1 << 0)) != 0)
            {
                corr = (int)dec.DecodeSymbol(rgbDiff0);
                item.Rgb[0] = (UInt16)MyDefs.FoldUint8(corr + (lastItem[0] & 255));
            }
            else
            {
                item.Rgb[0] = (UInt16)(lastItem[0] & 0xFF);
            }

            if ((sym & (1 << 1)) != 0)
            {
                corr = (int)dec.DecodeSymbol(rgbDiff1);
                item.Rgb[0] |= (UInt16)((MyDefs.FoldUint8(corr + (lastItem[0] >> 8))) << 8);
            }
            else
            {
                item.Rgb[0] |= (UInt16)(lastItem[0] & 0xFF00);
            }

            if ((sym & (1 << 6)) != 0)
            {
                diff = (item.Rgb[0] & 0x00FF) - (lastItem[0] & 0x00FF);
                if ((sym & (1 << 2)) != 0)
                {
                    corr = (int)dec.DecodeSymbol(rgbDiff2);
                    item.Rgb[1] = (UInt16)MyDefs.FoldUint8(corr + MyDefs.ClampUint8(diff + (lastItem[1] & 255)));
                }
                else
                {
                    item.Rgb[1] = (UInt16)(lastItem[1] & 0xFF);
                }

                if ((sym & (1 << 4)) != 0)
                {
                    corr = (int)dec.DecodeSymbol(rgbDiff4);
                    diff = (diff + ((item.Rgb[1] & 0x00FF) - (lastItem[1] & 0x00FF))) / 2;
                    item.Rgb[2] = (UInt16)MyDefs.FoldUint8(corr + MyDefs.ClampUint8(diff + (lastItem[2] & 255)));
                }
                else
                {
                    item.Rgb[2] = (UInt16)(lastItem[2] & 0xFF);
                }

                diff = (item.Rgb[0] >> 8) - (lastItem[0] >> 8);
                if ((sym & (1 << 3)) != 0)
                {
                    corr = (int)dec.DecodeSymbol(rgbDiff3);
                    item.Rgb[1] |= (UInt16)((MyDefs.FoldUint8(corr + MyDefs.ClampUint8(diff + (lastItem[1] >> 8)))) << 8);
                }
                else
                {
                    item.Rgb[1] |= (UInt16)(lastItem[1] & 0xFF00);
                }

                if ((sym & (1 << 5)) != 0)
                {
                    corr = (int)dec.DecodeSymbol(rgbDiff5);
                    diff = (diff + ((item.Rgb[1] >> 8) - (lastItem[1] >> 8))) / 2;
                    item.Rgb[2] |= (UInt16)((MyDefs.FoldUint8(corr + MyDefs.ClampUint8(diff + (lastItem[2] >> 8)))) << 8);
                }
                else
                {
                    item.Rgb[2] |= (UInt16)(lastItem[2] & 0xFF00);
                }
            }
            else
            {
                item.Rgb[1] = item.Rgb[0];
                item.Rgb[2] = item.Rgb[0];
            }

            lastItem[0] = item.Rgb[0];
            lastItem[1] = item.Rgb[1];
            lastItem[2] = item.Rgb[2];

            return true;
        }
    }
}
