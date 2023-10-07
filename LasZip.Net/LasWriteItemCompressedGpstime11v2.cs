// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasWriteItemCompressedGpstime11v2 : LasWriteItemCompressed
    {
        private const int LasZipGpstimeMulti = 500;
        private const int LasZipGpstimeMultiMinus = -10;
        private const int LasZipGpstimeMultiUnchanged = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 1);
        private const int LasZipGpstimeMultiCodeFull = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 2);

        private const int LASZIP_GPSTIME_MULTI_TOTAL = (LasZipGpstimeMulti - LasZipGpstimeMultiMinus + 6);

        private readonly ArithmeticEncoder enc;
        private UInt32 last; 
        private UInt32 next;
        private readonly Interpretable64[] lastGpstime = new Interpretable64[4];
        private readonly int[] lastGpstimeDiff = new int[4];
        private readonly int[] multiExtremeCounter = new int[4];

        private readonly ArithmeticModel gpstimeMulti;
        private readonly ArithmeticModel gpstime0diff;
        private readonly IntegerCompressor icGpstime;

        public LasWriteItemCompressedGpstime11v2(ArithmeticEncoder enc)
        {
            // set encoder
            Debug.Assert(enc != null);
            this.enc = enc;

            // create entropy models and integer compressors
            gpstimeMulti = ArithmeticEncoder.CreateSymbolModel(LASZIP_GPSTIME_MULTI_TOTAL);
            gpstime0diff = ArithmeticEncoder.CreateSymbolModel(6);
            icGpstime = new IntegerCompressor(enc, 32, 9); // 32 bits, 9 contexts
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
            ArithmeticEncoder.InitSymbolModel(gpstimeMulti);
            ArithmeticEncoder.InitSymbolModel(gpstime0diff);
            icGpstime.InitCompressor();

            // init last item
            lastGpstime[0].Double = item.Gpstime;
            lastGpstime[1].UInt64 = 0;
            lastGpstime[2].UInt64 = 0;
            lastGpstime[3].UInt64 = 0;
            return true;
        }

        public override bool Write(LasPoint item)
        {
            Interpretable64 this_gpstime = new()
            {
                Double = item.Gpstime
            };

            if (lastGpstimeDiff[last] == 0) // if the last integer difference was zero
            {
                if (this_gpstime.Int64 == lastGpstime[last].Int64)
                {
                    enc.EncodeSymbol(gpstime0diff, 0); // the doubles have not changed
                }
                else
                {
                    // calculate the difference between the two doubles as an integer
                    Int64 curr_gpstime_diff_64 = this_gpstime.Int64 - lastGpstime[last].Int64;
                    int curr_gpstime_diff = (int)curr_gpstime_diff_64;
                    if (curr_gpstime_diff_64 == (Int64)(curr_gpstime_diff))
                    {
                        enc.EncodeSymbol(gpstime0diff, 1); // the difference can be represented with 32 bits
                        icGpstime.Compress(0, curr_gpstime_diff, 0);
                        lastGpstimeDiff[last] = curr_gpstime_diff;
                        multiExtremeCounter[last] = 0;
                    }
                    else // the difference is huge
                    {
                        // maybe the double belongs to another time sequence
                        for (UInt32 i = 1; i < 4; i++)
                        {
                            Int64 other_gpstime_diff_64 = this_gpstime.Int64 - lastGpstime[(last + i) & 3].Int64;
                            int other_gpstime_diff = (int)other_gpstime_diff_64;
                            if (other_gpstime_diff_64 == (Int64)(other_gpstime_diff))
                            {
                                enc.EncodeSymbol(gpstime0diff, i + 2); // it belongs to another sequence 
                                last = (last + i) & 3;
                                return Write(item);
                            }
                        }
                        // no other sequence found. start new sequence.
                        enc.EncodeSymbol(gpstime0diff, 2);
                        icGpstime.Compress((int)(lastGpstime[last].UInt64 >> 32), (int)(this_gpstime.UInt64 >> 32), 8);
                        enc.WriteInt((UInt32)(this_gpstime.UInt64));
                        next = (next + 1) & 3;
                        last = next;
                        lastGpstimeDiff[last] = 0;
                        multiExtremeCounter[last] = 0;
                    }
                    lastGpstime[last].Int64 = this_gpstime.Int64;
                }
            }
            else // the last integer difference was *not* zero
            {
                if (this_gpstime.Int64 == lastGpstime[last].Int64)
                {
                    // if the doubles have not changed use a special symbol
                    enc.EncodeSymbol(gpstimeMulti, LasZipGpstimeMultiUnchanged);
                }
                else
                {
                    // calculate the difference between the two doubles as an integer
                    Int64 curr_gpstime_diff_64 = this_gpstime.Int64 - lastGpstime[last].Int64;
                    int curr_gpstime_diff = (int)curr_gpstime_diff_64;

                    // if the current gpstime difference can be represented with 32 bits
                    if (curr_gpstime_diff_64 == (Int64)(curr_gpstime_diff))
                    {
                        // compute multiplier between current and last integer difference
                        double multi_f = (double)curr_gpstime_diff / (double)(lastGpstimeDiff[last]);
                        int multi = MyDefs.QuantizeInt32(multi_f);

                        // compress the residual curr_gpstime_diff in dependance on the multiplier
                        if (multi == 1)
                        {
                            // this is the case we assume we get most often for regular spaced pulses
                            enc.EncodeSymbol(gpstimeMulti, 1);
                            icGpstime.Compress(lastGpstimeDiff[last], curr_gpstime_diff, 1);
                            multiExtremeCounter[last] = 0;
                        }
                        else if (multi > 0)
                        {
                            if (multi < LasZipGpstimeMulti) // positive multipliers up to LASZIP_GPSTIME_MULTI are compressed directly
                            {
                                enc.EncodeSymbol(gpstimeMulti, (UInt32)multi);
                                if (multi < 10)
                                    icGpstime.Compress(multi * lastGpstimeDiff[last], curr_gpstime_diff, 2);
                                else
                                    icGpstime.Compress(multi * lastGpstimeDiff[last], curr_gpstime_diff, 3);
                            }
                            else
                            {
                                enc.EncodeSymbol(gpstimeMulti, LasZipGpstimeMulti);
                                icGpstime.Compress(LasZipGpstimeMulti * lastGpstimeDiff[last], curr_gpstime_diff, 4);
                                multiExtremeCounter[last]++;
                                if (multiExtremeCounter[last] > 3)
                                {
                                    lastGpstimeDiff[last] = curr_gpstime_diff;
                                    multiExtremeCounter[last] = 0;
                                }
                            }
                        }
                        else if (multi < 0)
                        {
                            if (multi > LasZipGpstimeMultiMinus) // negative multipliers larger than LASZIP_GPSTIME_MULTI_MINUS are compressed directly
                            {
                                enc.EncodeSymbol(gpstimeMulti, (UInt32)(LasZipGpstimeMulti - multi));
                                icGpstime.Compress(multi * lastGpstimeDiff[last], curr_gpstime_diff, 5);
                            }
                            else
                            {
                                enc.EncodeSymbol(gpstimeMulti, LasZipGpstimeMulti - LasZipGpstimeMultiMinus);
                                icGpstime.Compress(LasZipGpstimeMultiMinus * lastGpstimeDiff[last], curr_gpstime_diff, 6);
                                multiExtremeCounter[last]++;
                                if (multiExtremeCounter[last] > 3)
                                {
                                    lastGpstimeDiff[last] = curr_gpstime_diff;
                                    multiExtremeCounter[last] = 0;
                                }
                            }
                        }
                        else
                        {
                            enc.EncodeSymbol(gpstimeMulti, 0);
                            icGpstime.Compress(0, curr_gpstime_diff, 7);
                            multiExtremeCounter[last]++;
                            if (multiExtremeCounter[last] > 3)
                            {
                                lastGpstimeDiff[last] = curr_gpstime_diff;
                                multiExtremeCounter[last] = 0;
                            }
                        }
                    }
                    else // the difference is huge
                    {
                        // maybe the double belongs to another time sequence
                        for (UInt32 i = 1; i < 4; i++)
                        {
                            Int64 other_gpstime_diff_64 = this_gpstime.Int64 - lastGpstime[(last + i) & 3].Int64;
                            int other_gpstime_diff = (int)other_gpstime_diff_64;
                            if (other_gpstime_diff_64 == (Int64)(other_gpstime_diff))
                            {
                                // it belongs to this sequence 
                                enc.EncodeSymbol(gpstimeMulti, LasZipGpstimeMultiCodeFull + i);
                                last = (last + i) & 3;
                                return Write(item);
                            }
                        }
                        // no other sequence found. start new sequence.
                        enc.EncodeSymbol(gpstimeMulti, LasZipGpstimeMultiCodeFull);
                        icGpstime.Compress((int)(lastGpstime[last].UInt64 >> 32), (int)(this_gpstime.UInt64 >> 32), 8);
                        enc.WriteInt((UInt32)(this_gpstime.UInt64));
                        next = (next + 1) & 3;
                        last = next;
                        lastGpstimeDiff[last] = 0;
                        multiExtremeCounter[last] = 0;
                    }
                    lastGpstime[last].Int64 = this_gpstime.Int64;
                }
            }

            return true;
        }
    }
}
