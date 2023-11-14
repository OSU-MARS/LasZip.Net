// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasReadItemCompressedGpstime11v2 : LasReadItemCompressed
    {
        private const int LasZipGpstimeMulti = 500;
        private const int LasZipGpstimeMultiMinus = -10;
        private const int LasZipGpstimeMultiUnchanged = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 1);
        private const int LasZipGpstimeMultiCodeFull = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 2);

        private const int LasZipGpstimeMultiTotal = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 6);

        private readonly ArithmeticDecoder decoder;
        private UInt32 last;
        private UInt32 next;
        private readonly Interpretable64[] lastGpstime;
        private readonly int[] lastGpstimeDiff;
        private readonly int[] multiExtremeCounter;

        private readonly ArithmeticModel gpstimeMulti;
        private readonly ArithmeticModel gpstime0diff;
        private readonly IntegerCompressor icGpstime;

        public LasReadItemCompressedGpstime11v2(ArithmeticDecoder decoder)
        {
            this.decoder = decoder;
            this.lastGpstime = new Interpretable64[4];
            this.lastGpstimeDiff = new int[4];
            this.multiExtremeCounter = new int[4];

            // create entropy models and integer compressors
            this.gpstimeMulti = ArithmeticDecoder.CreateSymbolModel(LasReadItemCompressedGpstime11v2.LasZipGpstimeMultiTotal);
            this.gpstime0diff = ArithmeticDecoder.CreateSymbolModel(6);
            this.icGpstime = new IntegerCompressor(decoder, 32, 9); // 32 bits, 9 contexts
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            this.last = 0;
            this.next = 0;
            this.lastGpstimeDiff[0] = 0;
            this.lastGpstimeDiff[1] = 0;
            this.lastGpstimeDiff[2] = 0;
            this.lastGpstimeDiff[3] = 0;
            this.multiExtremeCounter[0] = 0;
            this.multiExtremeCounter[1] = 0;
            this.multiExtremeCounter[2] = 0;
            this.multiExtremeCounter[3] = 0;

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(gpstimeMulti);
            ArithmeticDecoder.InitSymbolModel(gpstime0diff);
            this.icGpstime.InitDecompressor();

            // init last item
            this.lastGpstime[0].Double = BinaryPrimitives.ReadDoubleLittleEndian(item);
            this.lastGpstime[1].UInt64 = 0;
            this.lastGpstime[2].UInt64 = 0;
            this.lastGpstime[3].UInt64 = 0;
            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            if (lastGpstimeDiff[last] == 0) // if the last integer difference was zero
            {
                int multi = (int)decoder.DecodeSymbol(gpstime0diff);
                if (multi == 1) // the difference can be represented with 32 bits
                {
                    this.lastGpstimeDiff[last] = this.icGpstime.Decompress(0, 0);
                    this.lastGpstime[last].Int64 += this.lastGpstimeDiff[last];
                    this.multiExtremeCounter[last] = 0;
                }
                else if (multi == 2) // the difference is huge
                {
                    this.next = (next + 1) & 3;
                    this.lastGpstime[next].UInt64 = (UInt64)icGpstime.Decompress((int)(lastGpstime[last].UInt64 >> 32), 8);
                    this.lastGpstime[next].UInt64 = this.lastGpstime[next].UInt64 << 32;
                    this.lastGpstime[next].UInt64 |= this.decoder.ReadUInt32();
                    this.last = next;
                    this.lastGpstimeDiff[last] = 0;
                    this.multiExtremeCounter[last] = 0;
                }
                else if (multi > 2) // we switch to another sequence
                {
                    this.last = (UInt32)(last + multi - 2) & 3;
                    this.TryRead(item, context);
                }
            }
            else
            {
                int multi = (int)decoder.DecodeSymbol(gpstimeMulti);
                if (multi == 1)
                {
                    this.lastGpstime[last].Int64 += icGpstime.Decompress(lastGpstimeDiff[last], 1); ;
                    this.multiExtremeCounter[last] = 0;
                }
                else if (multi < LasZipGpstimeMultiUnchanged)
                {
                    int gpstime_diff;
                    if (multi == 0)
                    {
                        gpstime_diff = icGpstime.Decompress(0, 7);
                        multiExtremeCounter[last]++;
                        if (multiExtremeCounter[last] > 3)
                        {
                            lastGpstimeDiff[last] = gpstime_diff;
                            multiExtremeCounter[last] = 0;
                        }
                    }
                    else if (multi < LasZipGpstimeMulti)
                    {
                        if (multi < 10)
                            gpstime_diff = icGpstime.Decompress(multi * lastGpstimeDiff[last], 2);
                        else
                            gpstime_diff = icGpstime.Decompress(multi * lastGpstimeDiff[last], 3);
                    }
                    else if (multi == LasZipGpstimeMulti)
                    {
                        gpstime_diff = icGpstime.Decompress(LasZipGpstimeMulti * lastGpstimeDiff[last], 4);
                        multiExtremeCounter[last]++;
                        if (multiExtremeCounter[last] > 3)
                        {
                            lastGpstimeDiff[last] = gpstime_diff;
                            multiExtremeCounter[last] = 0;
                        }
                    }
                    else
                    {
                        multi = LasZipGpstimeMulti - multi;
                        if (multi > LasZipGpstimeMultiMinus)
                        {
                            gpstime_diff = icGpstime.Decompress(multi * lastGpstimeDiff[last], 5);
                        }
                        else
                        {
                            gpstime_diff = icGpstime.Decompress(LasZipGpstimeMultiMinus * lastGpstimeDiff[last], 6);
                            multiExtremeCounter[last]++;
                            if (multiExtremeCounter[last] > 3)
                            {
                                lastGpstimeDiff[last] = gpstime_diff;
                                multiExtremeCounter[last] = 0;
                            }
                        }
                    }
                    this.lastGpstime[last].Int64 += gpstime_diff;
                }
                else if (multi == LasZipGpstimeMultiCodeFull)
                {
                    this.next = (next + 1) & 3;
                    this.lastGpstime[next].UInt64 = (UInt64)icGpstime.Decompress((int)(lastGpstime[last].UInt64 >> 32), 8);
                    this.lastGpstime[next].UInt64 = lastGpstime[next].UInt64 << 32;
                    this.lastGpstime[next].UInt64 |= decoder.ReadUInt32();
                    this.last = next;
                    this.lastGpstimeDiff[last] = 0;
                    this.multiExtremeCounter[last] = 0;
                }
                else if (multi >= LasZipGpstimeMultiCodeFull)
                {
                    this.last = (UInt32)(last + multi - LasZipGpstimeMultiCodeFull) & 3;
                    this.TryRead(item, context);
                }
            }

            BinaryPrimitives.WriteDoubleLittleEndian(item, this.lastGpstime[last].Double);
            return true;
        }
    }
}
