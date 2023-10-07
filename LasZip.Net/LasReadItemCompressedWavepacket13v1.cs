// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LASreadItemCompressedWavepacket13v1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private LasWavepacket13 lastItem;

        private int lastDiff32;
        private UInt32 symLastOffsetDiff;
        private readonly ArithmeticModel packetIndex;
        private readonly ArithmeticModel[] offsetDiff = new ArithmeticModel[4];
        private readonly IntegerCompressor icOffsetDiff;
        private readonly IntegerCompressor icPacketSize;
        private readonly IntegerCompressor icReturnPoint;
        private readonly IntegerCompressor icXyz;

        public LASreadItemCompressedWavepacket13v1(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;

            // create models and integer compressors
            packetIndex = ArithmeticDecoder.CreateSymbolModel(256);
            offsetDiff[0] = ArithmeticDecoder.CreateSymbolModel(4);
            offsetDiff[1] = ArithmeticDecoder.CreateSymbolModel(4);
            offsetDiff[2] = ArithmeticDecoder.CreateSymbolModel(4);
            offsetDiff[3] = ArithmeticDecoder.CreateSymbolModel(4);
            icOffsetDiff = new IntegerCompressor(dec, 32);
            icPacketSize = new IntegerCompressor(dec, 32);
            icReturnPoint = new IntegerCompressor(dec, 32);
            icXyz = new IntegerCompressor(dec, 32, 3);
        }

        public unsafe override bool Init(LasPoint item)
        {
            // init state
            lastDiff32 = 0;
            symLastOffsetDiff = 0;

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(packetIndex);
            ArithmeticDecoder.InitSymbolModel(offsetDiff[0]);
            ArithmeticDecoder.InitSymbolModel(offsetDiff[1]);
            ArithmeticDecoder.InitSymbolModel(offsetDiff[2]);
            ArithmeticDecoder.InitSymbolModel(offsetDiff[3]);
            icOffsetDiff.InitDecompressor();
            icPacketSize.InitDecompressor();
            icReturnPoint.InitDecompressor();
            icXyz.InitDecompressor();

            // init last item
            fixed (byte* pItem = item.Wavepacket)
            {
                lastItem = *(LasWavepacket13*)(pItem + 1);
            }

            return true;
        }

        public unsafe override bool TryRead(LasPoint item)
        {
            item.Wavepacket[0] = (byte)(dec.DecodeSymbol(packetIndex));

            fixed (byte* pItem = item.Wavepacket)
            {
                LasWavepacket13* wave = (LasWavepacket13*)(pItem + 1);

                symLastOffsetDiff = dec.DecodeSymbol(offsetDiff[symLastOffsetDiff]);

                if (symLastOffsetDiff == 0)
                {
                    wave->Offset = lastItem.Offset;
                }
                else if (symLastOffsetDiff == 1)
                {
                    wave->Offset = lastItem.Offset + lastItem.PacketSize;
                }
                else if (symLastOffsetDiff == 2)
                {
                    lastDiff32 = icOffsetDiff.Decompress(lastDiff32);
                    wave->Offset = (UInt64)((Int64)lastItem.Offset + lastDiff32);
                }
                else
                {
                    wave->Offset = dec.ReadInt64();
                }

                wave->PacketSize = (UInt32)icPacketSize.Decompress((int)lastItem.PacketSize);
                wave->ReturnPoint.Int32 = icReturnPoint.Decompress(lastItem.ReturnPoint.Int32);
                wave->X.Int32 = icXyz.Decompress(lastItem.X.Int32, 0);
                wave->Y.Int32 = icXyz.Decompress(lastItem.Y.Int32, 1);
                wave->Z.Int32 = icXyz.Decompress(lastItem.Z.Int32, 2);

                lastItem = *wave;
            }

            return true;
        }
    }
}
