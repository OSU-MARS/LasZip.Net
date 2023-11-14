// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasReadItemCompressedGpstime11v1 : LasReadItemCompressed
    {
        private const int LasZipGpstimeMultimax = 512;

        private readonly ArithmeticDecoder decoder;
        private Interpretable64 lastGpstime;

        private readonly ArithmeticModel gpstimeMulti;
        private readonly ArithmeticModel gpstime0diff;
        private readonly IntegerCompressor icGpstime;
        private int multiExtremeCounter;
        private int lastGpstimeDiff;

        public LasReadItemCompressedGpstime11v1(ArithmeticDecoder decoder)
        {
            this.decoder = decoder;

            // create entropy models and integer compressors
            this.gpstimeMulti = ArithmeticDecoder.CreateSymbolModel(LasZipGpstimeMultimax);
            this.gpstime0diff = ArithmeticDecoder.CreateSymbolModel(3);
            this.icGpstime = new IntegerCompressor(decoder, 32, 6); // 32 bits, 6 contexts
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            this.lastGpstimeDiff = 0;
            this.multiExtremeCounter = 0;

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(this.gpstimeMulti);
            ArithmeticDecoder.InitSymbolModel(this.gpstime0diff);
            this.icGpstime.InitDecompressor();

            // init last item
            this.lastGpstime.Double = BinaryPrimitives.ReadDoubleLittleEndian(item);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            if (this.lastGpstimeDiff == 0) // if the last integer difference was zero
            {
                int multi = (int)decoder.DecodeSymbol(gpstime0diff);
                if (multi == 1) // the difference can be represented with 32 bits
                {
                    lastGpstimeDiff = this.icGpstime.Decompress(0, 0);
                    this.lastGpstime.Int64 += lastGpstimeDiff;
                }
                else if (multi == 2) // the difference is huge
                {
                    this.lastGpstime.UInt64 = decoder.ReadUInt64();
                }
            }
            else
            {
                int multi = (int)decoder.DecodeSymbol(gpstimeMulti);

                if (multi < LasReadItemCompressedGpstime11v1.LasZipGpstimeMultimax - 2)
                {
                    int gpstimeDiff;
                    if (multi == 1)
                    {
                        gpstimeDiff = icGpstime.Decompress(lastGpstimeDiff, 1);
                        lastGpstimeDiff = gpstimeDiff;
                        multiExtremeCounter = 0;
                    }
                    else if (multi == 0)
                    {
                        gpstimeDiff = icGpstime.Decompress(lastGpstimeDiff / 4, 2);
                        multiExtremeCounter++;
                        if (multiExtremeCounter > 3)
                        {
                            lastGpstimeDiff = gpstimeDiff;
                            multiExtremeCounter = 0;
                        }
                    }
                    else if (multi < 10)
                    {
                        gpstimeDiff = icGpstime.Decompress(multi * lastGpstimeDiff, 3);
                    }
                    else if (multi < 50)
                    {
                        gpstimeDiff = icGpstime.Decompress(multi * lastGpstimeDiff, 4);
                    }
                    else
                    {
                        gpstimeDiff = icGpstime.Decompress(multi * lastGpstimeDiff, 5);
                        if (multi == LasZipGpstimeMultimax - 3)
                        {
                            multiExtremeCounter++;
                            if (multiExtremeCounter > 3)
                            {
                                lastGpstimeDiff = gpstimeDiff;
                                multiExtremeCounter = 0;
                            }
                        }
                    }
                    this.lastGpstime.Int64 += gpstimeDiff;
                }
                else if (multi < LasZipGpstimeMultimax - 1)
                {
                    this.lastGpstime.UInt64 = decoder.ReadUInt64();
                }
            }

            BinaryPrimitives.WriteDoubleLittleEndian(item, this.lastGpstime.Double);
            return true;
        }
    }
}
