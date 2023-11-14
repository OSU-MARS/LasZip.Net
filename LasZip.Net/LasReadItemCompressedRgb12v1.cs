// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasReadItemCompressedRgb12v1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private UInt16 r;
        private UInt16 g;
        private UInt16 b;

        private readonly ArithmeticModel byteUsed;
        private readonly IntegerCompressor icRgb;

        public LasReadItemCompressedRgb12v1(ArithmeticDecoder decoder)
        {
            this.decoder = decoder;

            // create models and integer compressors
            this.byteUsed = ArithmeticDecoder.CreateSymbolModel(64);
            this.icRgb = new IntegerCompressor(decoder, 8, 6);
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(byteUsed);
            icRgb.InitDecompressor();

            // init last item
            r = BinaryPrimitives.ReadUInt16LittleEndian(item);
            g = BinaryPrimitives.ReadUInt16LittleEndian(item[2..]);
            b = BinaryPrimitives.ReadUInt16LittleEndian(item[4..]);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            Span<UInt16> item16 = stackalloc UInt16[] 
            { 
                BinaryPrimitives.ReadUInt16LittleEndian(item),
                BinaryPrimitives.ReadUInt16LittleEndian(item[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(item[4..])
            };

            UInt32 sym = decoder.DecodeSymbol(byteUsed);
            if ((sym & (1 << 0)) != 0) 
                item16[0] = (UInt16)icRgb.Decompress(r & 255, 0);
            else
                item16[0] = (UInt16)(r & 0xFF);

            if ((sym & (1 << 1)) != 0) 
                item16[0] |= (UInt16)(((UInt16)icRgb.Decompress(r >> 8, 1)) << 8);
            else
                item16[0] |= (UInt16)(r & 0xFF00);

            if ((sym & (1 << 2)) != 0) 
                item16[1] = (UInt16)icRgb.Decompress(g & 255, 2);
            else
                item16[1] = (UInt16)(g & 0xFF);

            if ((sym & (1 << 3)) != 0) 
                item16[1] |= (UInt16)(((UInt16)icRgb.Decompress(g >> 8, 3)) << 8);
            else
                item16[1] |= (UInt16)(g & 0xFF00);

            if ((sym & (1 << 4)) != 0) 
                item16[2] = (UInt16)icRgb.Decompress(b & 255, 4);
            else
                item16[2] = (UInt16)(b & 0xFF);

            if ((sym & (1 << 5)) != 0) 
                item16[2] |= (UInt16)(((UInt16)icRgb.Decompress(b >> 8, 5)) << 8);
            else
                item16[2] |= (UInt16)(b & 0xFF00);

            this.r = item16[0];
            this.g = item16[1];
            this.b = item16[2];

            return true;
        }
    }
}
