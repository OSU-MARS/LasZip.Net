// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedGpstime11v2 : LasReadItemCompressed
    {
        private const int LasZipGpstimeMulti = 500;
        private const int LasZipGpstimeMultiMinus = -10;
        private const int LasZipGpstimeMultiUnchanged = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 1);
        private const int LasZipGpstimeMultiCodeFull = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 2);

        private const int LasZipGpstimeMultiTotal = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 6);

        private readonly ArithmeticDecoder dec;
        private UInt32 last;
        private UInt32 next;
        private readonly Interpretable64[] lastGpstime;
        private readonly int[] lastGpstimeDiff;
        private readonly int[] multiExtremeCounter;

        private readonly ArithmeticModel gpstimeMulti;
        private readonly ArithmeticModel gpstime0diff;
        private readonly IntegerCompressor icGpstime;

        public LasReadItemCompressedGpstime11v2(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;
            this.lastGpstime = new Interpretable64[4];
            this.lastGpstimeDiff = new int[4];
            this.multiExtremeCounter = new int[4];

            // create entropy models and integer compressors
            gpstimeMulti = ArithmeticDecoder.CreateSymbolModel(LasZipGpstimeMultiTotal);
            gpstime0diff = ArithmeticDecoder.CreateSymbolModel(6);
            icGpstime = new IntegerCompressor(dec, 32, 9); // 32 bits, 9 contexts
        }

        public override bool Init(LasPoint item)
        {
            // init state
            last = 0; next = 0;
            lastGpstimeDiff[0] = 0;
            lastGpstimeDiff[1] = 0;
            lastGpstimeDiff[2] = 0;
            lastGpstimeDiff[3] = 0;
            multiExtremeCounter[0] = 0;
            multiExtremeCounter[1] = 0;
            multiExtremeCounter[2] = 0;
            multiExtremeCounter[3] = 0;

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(gpstimeMulti);
            ArithmeticDecoder.InitSymbolModel(gpstime0diff);
            icGpstime.InitDecompressor();

            // init last item
            lastGpstime[0].Double = item.Gpstime;
            lastGpstime[1].UInt64 = 0;
            lastGpstime[2].UInt64 = 0;
            lastGpstime[3].UInt64 = 0;
            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            if (lastGpstimeDiff[last] == 0) // if the last integer difference was zero
            {
                int multi = (int)dec.DecodeSymbol(gpstime0diff);
                if (multi == 1) // the difference can be represented with 32 bits
                {
                    lastGpstimeDiff[last] = icGpstime.Decompress(0, 0);
                    lastGpstime[last].Int64 += lastGpstimeDiff[last];
                    multiExtremeCounter[last] = 0;
                }
                else if (multi == 2) // the difference is huge
                {
                    next = (next + 1) & 3;
                    lastGpstime[next].UInt64 = (UInt64)icGpstime.Decompress((int)(lastGpstime[last].UInt64 >> 32), 8);
                    lastGpstime[next].UInt64 = lastGpstime[next].UInt64 << 32;
                    lastGpstime[next].UInt64 |= dec.ReadInt();
                    last = next;
                    lastGpstimeDiff[last] = 0;
                    multiExtremeCounter[last] = 0;
                }
                else if (multi > 2) // we switch to another sequence
                {
                    last = (UInt32)(last + multi - 2) & 3;
                    TryRead(item);
                }
            }
            else
            {
                int multi = (int)dec.DecodeSymbol(gpstimeMulti);
                if (multi == 1)
                {
                    lastGpstime[last].Int64 += icGpstime.Decompress(lastGpstimeDiff[last], 1); ;
                    multiExtremeCounter[last] = 0;
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
                    lastGpstime[last].Int64 += gpstime_diff;
                }
                else if (multi == LasZipGpstimeMultiCodeFull)
                {
                    next = (next + 1) & 3;
                    lastGpstime[next].UInt64 = (UInt64)icGpstime.Decompress((int)(lastGpstime[last].UInt64 >> 32), 8);
                    lastGpstime[next].UInt64 = lastGpstime[next].UInt64 << 32;
                    lastGpstime[next].UInt64 |= dec.ReadInt();
                    last = next;
                    lastGpstimeDiff[last] = 0;
                    multiExtremeCounter[last] = 0;
                }
                else if (multi >= LasZipGpstimeMultiCodeFull)
                {
                    last = (UInt32)(last + multi - LasZipGpstimeMultiCodeFull) & 3;
                    TryRead(item);
                }
            }
            item.Gpstime = lastGpstime[last].Double;
            return true;
        }
    }
}
