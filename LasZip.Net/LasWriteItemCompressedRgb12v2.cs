// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasWriteItemCompressedRgb12v2 : LasWriteItemCompressed
    {
        public ArithmeticEncoder encoder;
        public UInt16[] lastItem;

        public ArithmeticModel byteUsed;
        public ArithmeticModel rgbDiff0;
        public ArithmeticModel rgbDiff1;
        public ArithmeticModel rgbDiff2;
        public ArithmeticModel rgbDiff3;
        public ArithmeticModel rgbDiff4;
        public ArithmeticModel rgbDiff5;

        public LasWriteItemCompressedRgb12v2(ArithmeticEncoder encoder)
        {
            this.encoder = encoder;
            this.lastItem = new UInt16[3];

            // create models and integer compressors
            this.byteUsed = ArithmeticEncoder.CreateSymbolModel(128);
            this.rgbDiff0 = ArithmeticEncoder.CreateSymbolModel(256);
            this.rgbDiff1 = ArithmeticEncoder.CreateSymbolModel(256);
            this.rgbDiff2 = ArithmeticEncoder.CreateSymbolModel(256);
            this.rgbDiff3 = ArithmeticEncoder.CreateSymbolModel(256);
            this.rgbDiff4 = ArithmeticEncoder.CreateSymbolModel(256);
            this.rgbDiff5 = ArithmeticEncoder.CreateSymbolModel(256);
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init models and integer compressors
            ArithmeticEncoder.InitSymbolModel(byteUsed);
            ArithmeticEncoder.InitSymbolModel(rgbDiff0);
            ArithmeticEncoder.InitSymbolModel(rgbDiff1);
            ArithmeticEncoder.InitSymbolModel(rgbDiff2);
            ArithmeticEncoder.InitSymbolModel(rgbDiff3);
            ArithmeticEncoder.InitSymbolModel(rgbDiff4);
            ArithmeticEncoder.InitSymbolModel(rgbDiff5);

            // init last item
            this.lastItem[0] = BinaryPrimitives.ReadUInt16LittleEndian(item);
            this.lastItem[1] = BinaryPrimitives.ReadUInt16LittleEndian(item[2..]);
            this.lastItem[2] = BinaryPrimitives.ReadUInt16LittleEndian(item[4..]);
            return true;
        }

        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            int diffL = 0;
            int diffH = 0;
            UInt32 sym = 0;

            UInt16 r = BinaryPrimitives.ReadUInt16LittleEndian(item);
            UInt16 g = BinaryPrimitives.ReadUInt16LittleEndian(item[2..]);
            UInt16 b = BinaryPrimitives.ReadUInt16LittleEndian(item[4..]);

            bool rl = (lastItem[0] & 0x00FF) != (r & 0x00FF); if (rl) sym |= 1;
            bool rh = (lastItem[0] & 0xFF00) != (r & 0xFF00); if (rh) sym |= 2;
            bool gl = (lastItem[1] & 0x00FF) != (g & 0x00FF); if (gl) sym |= 4;
            bool gh = (lastItem[1] & 0xFF00) != (g & 0xFF00); if (gh) sym |= 8;
            bool bl = (lastItem[2] & 0x00FF) != (b & 0x00FF); if (bl) sym |= 16;
            bool bh = (lastItem[2] & 0xFF00) != (b & 0xFF00); if (bh) sym |= 32;

            bool allColors = ((r & 0x00FF) != (g & 0x00FF)) || ((r & 0x00FF) != (b & 0x00FF)) ||
                ((r & 0xFF00) != (g & 0xFF00)) || ((r & 0xFF00) != (b & 0xFF00));
            if (allColors) sym |= 64;

            encoder.EncodeSymbol(byteUsed, sym);
            if (rl)
            {
                diffL = ((int)(r & 255)) - (lastItem[0] & 255);
                encoder.EncodeSymbol(rgbDiff0, (byte)MyDefs.FoldUInt8(diffL));
            }
            if (rh)
            {
                diffH = ((int)(r >> 8)) - (lastItem[0] >> 8);
                encoder.EncodeSymbol(rgbDiff1, (byte)MyDefs.FoldUInt8(diffH));
            }

            if (allColors)
            {
                if (gl)
                {
                    int corr = ((int)(g & 255)) - MyDefs.ClampUInt8(diffL + (lastItem[1] & 255));
                    encoder.EncodeSymbol(rgbDiff2, (byte)MyDefs.FoldUInt8(corr));
                }
                if (bl)
                {
                    diffL = (diffL + (g & 255) - (lastItem[1] & 255)) / 2;
                    int corr = ((int)(b & 255)) - MyDefs.ClampUInt8(diffL + (lastItem[2] & 255));
                    encoder.EncodeSymbol(rgbDiff4, (byte)MyDefs.FoldUInt8(corr));
                }
                if (gh)
                {
                    int corr = ((int)(g >> 8)) - MyDefs.ClampUInt8(diffH + (lastItem[1] >> 8));
                    encoder.EncodeSymbol(rgbDiff3, (byte)MyDefs.FoldUInt8(corr));
                }
                if (bh)
                {
                    diffH = (diffH + (g >> 8) - (lastItem[1] >> 8)) / 2;
                    int corr = ((int)(b >> 8)) - MyDefs.ClampUInt8(diffH + (lastItem[2] >> 8));
                    encoder.EncodeSymbol(rgbDiff5, (byte)MyDefs.FoldUInt8(corr));
                }
            }

            lastItem[0] = r;
            lastItem[1] = g;
            lastItem[2] = b;

            return true;
        }
    }
}
