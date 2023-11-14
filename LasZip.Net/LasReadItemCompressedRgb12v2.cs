// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasReadItemCompressedRgb12v2 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private readonly UInt16[] lastItem;

        private readonly ArithmeticModel byteUsed;
        private readonly ArithmeticModel rgbDiff0;
        private readonly ArithmeticModel rgbDiff1;
        private readonly ArithmeticModel rgbDiff2;
        private readonly ArithmeticModel rgbDiff3;
        private readonly ArithmeticModel rgbDiff4;
        private readonly ArithmeticModel rgbDiff5;

        public LasReadItemCompressedRgb12v2(ArithmeticDecoder decoder)
        {
            this.decoder = decoder;
            this.lastItem = new UInt16[3];

            // create models and integer compressors
            this.byteUsed = ArithmeticDecoder.CreateSymbolModel(128);
            this.rgbDiff0 = ArithmeticDecoder.CreateSymbolModel(256);
            this.rgbDiff1 = ArithmeticDecoder.CreateSymbolModel(256);
            this.rgbDiff2 = ArithmeticDecoder.CreateSymbolModel(256);
            this.rgbDiff3 = ArithmeticDecoder.CreateSymbolModel(256);
            this.rgbDiff4 = ArithmeticDecoder.CreateSymbolModel(256);
            this.rgbDiff5 = ArithmeticDecoder.CreateSymbolModel(256);
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(byteUsed);
            ArithmeticDecoder.InitSymbolModel(rgbDiff0);
            ArithmeticDecoder.InitSymbolModel(rgbDiff1);
            ArithmeticDecoder.InitSymbolModel(rgbDiff2);
            ArithmeticDecoder.InitSymbolModel(rgbDiff3);
            ArithmeticDecoder.InitSymbolModel(rgbDiff4);
            ArithmeticDecoder.InitSymbolModel(rgbDiff5);

            // init last item
            this.lastItem[0] = BinaryPrimitives.ReadUInt16LittleEndian(item);
            this.lastItem[1] = BinaryPrimitives.ReadUInt16LittleEndian(item[2..]);
            this.lastItem[2] = BinaryPrimitives.ReadUInt16LittleEndian(item[4..]);
            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            int corr;
            UInt32 sym = decoder.DecodeSymbol(byteUsed);
            UInt16 r;
            if ((sym & (1 << 0)) != 0)
            {
                corr = (int)decoder.DecodeSymbol(rgbDiff0);
                r = (UInt16)MyDefs.FoldUInt8(corr + (lastItem[0] & 255));
            }
            else
            {
                r = (UInt16)(lastItem[0] & 0xFF);
            }

            if ((sym & (1 << 1)) != 0)
            {
                corr = (int)decoder.DecodeSymbol(rgbDiff1);
                r |= (UInt16)((MyDefs.FoldUInt8(corr + (lastItem[0] >> 8))) << 8);
            }
            else
            {
                r |= (UInt16)(lastItem[0] & 0xFF00);
            }

            UInt16 g;
            UInt16 b;
            int diff;
            if ((sym & (1 << 6)) != 0)
            {
                diff = (r & 0x00FF) - (lastItem[0] & 0x00FF);
                if ((sym & (1 << 2)) != 0)
                {
                    corr = (int)decoder.DecodeSymbol(rgbDiff2);
                    g = (UInt16)MyDefs.FoldUInt8(corr + MyDefs.ClampUInt8(diff + (lastItem[1] & 255)));
                }
                else
                {
                    g = (UInt16)(lastItem[1] & 0xFF);
                }

                if ((sym & (1 << 4)) != 0)
                {
                    corr = (int)decoder.DecodeSymbol(rgbDiff4);
                    diff = (diff + ((g & 0x00FF) - (lastItem[1] & 0x00FF))) / 2;
                    b = (UInt16)MyDefs.FoldUInt8(corr + MyDefs.ClampUInt8(diff + (lastItem[2] & 255)));
                }
                else
                {
                    b = (UInt16)(lastItem[2] & 0xFF);
                }

                diff = (r >> 8) - (lastItem[0] >> 8);
                if ((sym & (1 << 3)) != 0)
                {
                    corr = (int)decoder.DecodeSymbol(rgbDiff3);
                    g |= (UInt16)((MyDefs.FoldUInt8(corr + MyDefs.ClampUInt8(diff + (lastItem[1] >> 8)))) << 8);
                }
                else
                {
                    g |= (UInt16)(lastItem[1] & 0xFF00);
                }

                if ((sym & (1 << 5)) != 0)
                {
                    corr = (int)decoder.DecodeSymbol(rgbDiff5);
                    diff = (diff + ((g >> 8) - (lastItem[1] >> 8))) / 2;
                    b |= (UInt16)((MyDefs.FoldUInt8(corr + MyDefs.ClampUInt8(diff + (lastItem[2] >> 8)))) << 8);
                }
                else
                {
                    b |= (UInt16)(lastItem[2] & 0xFF00);
                }
            }
            else
            {
                g = r;
                b = r;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(item, r);
            BinaryPrimitives.WriteUInt16LittleEndian(item[2..], g);
            BinaryPrimitives.WriteUInt16LittleEndian(item[4..], b);

            this.lastItem[0] = r;
            this.lastItem[1] = g;
            this.lastItem[2] = b;

            return true;
        }
    }
}
