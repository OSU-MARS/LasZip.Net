// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LASreadItemCompressedWavepacket13v1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private LasWavepacket13 lastItem;

        private int lastDiff32;
        private UInt32 symLastOffsetDiff;
        private readonly ArithmeticModel packetIndex;
        private readonly ArithmeticModel[] offsetDiff;
        private readonly IntegerCompressor icOffsetDiff;
        private readonly IntegerCompressor icPacketSize;
        private readonly IntegerCompressor icReturnPoint;
        private readonly IntegerCompressor icXyz;

        public LASreadItemCompressedWavepacket13v1(ArithmeticDecoder decoder)
        {
            this.decoder = decoder;
            this.offsetDiff = new ArithmeticModel[4];

            // create models and integer compressors
            packetIndex = ArithmeticDecoder.CreateSymbolModel(256);
            offsetDiff[0] = ArithmeticDecoder.CreateSymbolModel(4);
            offsetDiff[1] = ArithmeticDecoder.CreateSymbolModel(4);
            offsetDiff[2] = ArithmeticDecoder.CreateSymbolModel(4);
            offsetDiff[3] = ArithmeticDecoder.CreateSymbolModel(4);
            icOffsetDiff = new IntegerCompressor(decoder, 32);
            icPacketSize = new IntegerCompressor(decoder, 32);
            icReturnPoint = new IntegerCompressor(decoder, 32);
            icXyz = new IntegerCompressor(decoder, 32, 3);
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            this.lastDiff32 = 0;
            this.symLastOffsetDiff = 0;

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(this.packetIndex);
            ArithmeticDecoder.InitSymbolModel(this.offsetDiff[0]);
            ArithmeticDecoder.InitSymbolModel(this.offsetDiff[1]);
            ArithmeticDecoder.InitSymbolModel(this.offsetDiff[2]);
            ArithmeticDecoder.InitSymbolModel(this.offsetDiff[3]);
            this.icOffsetDiff.InitDecompressor();
            this.icPacketSize.InitDecompressor();
            this.icReturnPoint.InitDecompressor();
            this.icXyz.InitDecompressor();

            // init last item
            this.lastItem.Offset = BinaryPrimitives.ReadUInt64LittleEndian(item);
            this.lastItem.PacketSize = BinaryPrimitives.ReadUInt32LittleEndian(item[8..]);
            this.lastItem.ReturnPoint.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[12..]);
            this.lastItem.X.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[16..]);
            this.lastItem.Y.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[20..]);
            this.lastItem.Z.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[24..]);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            item[0] = (byte)decoder.DecodeSymbol(packetIndex); // wavepacket descriptor index

            symLastOffsetDiff = decoder.DecodeSymbol(offsetDiff[symLastOffsetDiff]);
            UInt64 offset;
            if (symLastOffsetDiff == 0)
            {
                offset = lastItem.Offset;
            }
            else if (symLastOffsetDiff == 1)
            {
                offset = lastItem.Offset + lastItem.PacketSize;
            }
            else if (symLastOffsetDiff == 2)
            {
                lastDiff32 = icOffsetDiff.Decompress(lastDiff32);
                offset = (UInt64)((Int64)lastItem.Offset + lastDiff32);
            }
            else
            {
                offset = decoder.ReadUInt64();
            }
            BinaryPrimitives.WriteUInt64LittleEndian(item[1..], offset);

            UInt32 packetSize = (UInt32)this.icPacketSize.Decompress((int)this.lastItem.PacketSize);
            Int32 returnPoint = this.icReturnPoint.Decompress(this.lastItem.ReturnPoint.Int32);
            Int32 x = this.icXyz.Decompress(lastItem.X.Int32, 0);
            Int32 y = this.icXyz.Decompress(lastItem.Y.Int32, 1);
            Int32 z = this.icXyz.Decompress(lastItem.Z.Int32, 2);

            BinaryPrimitives.WriteUInt32LittleEndian(item[9..], packetSize);
            BinaryPrimitives.WriteInt32LittleEndian(item[13..], returnPoint);
            BinaryPrimitives.WriteInt32LittleEndian(item[17..], x);
            BinaryPrimitives.WriteInt32LittleEndian(item[21..], y);
            BinaryPrimitives.WriteInt32LittleEndian(item[25..], z);

            this.lastItem.Offset = offset;
            this.lastItem.PacketSize = packetSize;
            this.lastItem.ReturnPoint.Int32 = returnPoint;
            this.lastItem.X.Int32 = x;
            this.lastItem.Y.Int32 = y;
            this.lastItem.Z.Int32 = z;

            return true;
        }
    }
}
