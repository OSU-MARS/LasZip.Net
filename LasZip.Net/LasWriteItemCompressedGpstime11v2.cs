// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Buffers.Binary;
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

        private readonly ArithmeticEncoder encoder;
        private UInt32 last; 
        private UInt32 next;
        private readonly Interpretable64[] lastGpstime = new Interpretable64[4];
        private readonly int[] lastGpstimeDiff = new int[4];
        private readonly int[] multiExtremeCounter = new int[4];

        private readonly ArithmeticModel gpstimeMulti;
        private readonly ArithmeticModel gpstime0diff;
        private readonly IntegerCompressor icGpstime;

        public LasWriteItemCompressedGpstime11v2(ArithmeticEncoder encoder)
        {
            this.encoder = encoder;

            // create entropy models and integer compressors
            this.gpstimeMulti = ArithmeticEncoder.CreateSymbolModel(LASZIP_GPSTIME_MULTI_TOTAL);
            this.gpstime0diff = ArithmeticEncoder.CreateSymbolModel(6);
            this.icGpstime = new IntegerCompressor(encoder, 32, 9); // 32 bits, 9 contexts
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
            ArithmeticEncoder.InitSymbolModel(this.gpstimeMulti);
            ArithmeticEncoder.InitSymbolModel(this.gpstime0diff);
            this.icGpstime.InitCompressor();

            // init last item
            this.lastGpstime[0].Double = BinaryPrimitives.ReadDoubleLittleEndian(item);
            this.lastGpstime[1].UInt64 = 0;
            this.lastGpstime[2].UInt64 = 0;
            this.lastGpstime[3].UInt64 = 0;
            return true;
        }

        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            Interpretable64 thisGpstime = new()
            {
                Double = BinaryPrimitives.ReadDoubleLittleEndian(item)
            };

            if (this.lastGpstimeDiff[last] == 0) // if the last integer difference was zero
            {
                if (thisGpstime.Int64 == this.lastGpstime[last].Int64)
                {
                    this.encoder.EncodeSymbol(gpstime0diff, 0); // the doubles have not changed
                }
                else
                {
                    // calculate the difference between the two doubles as an integer
                    Int64 currGpstimeDiff64 = thisGpstime.Int64 - lastGpstime[last].Int64;
                    int currGpstimeDiff = (int)currGpstimeDiff64;
                    if (currGpstimeDiff64 == (Int64)(currGpstimeDiff))
                    {
                        this.encoder.EncodeSymbol(gpstime0diff, 1); // the difference can be represented with 32 bits
                        this.icGpstime.Compress(0, currGpstimeDiff, 0);
                        this.lastGpstimeDiff[last] = currGpstimeDiff;
                        this.multiExtremeCounter[last] = 0;
                    }
                    else // the difference is huge
                    {
                        // maybe the double belongs to another time sequence
                        for (UInt32 i = 1; i < 4; i++)
                        {
                            Int64 otherGpstimeDiff64 = thisGpstime.Int64 - lastGpstime[(last + i) & 3].Int64;
                            int otherGpstimeDiff = (int)otherGpstimeDiff64;
                            if (otherGpstimeDiff64 == (Int64)(otherGpstimeDiff))
                            {
                                encoder.EncodeSymbol(gpstime0diff, i + 2); // it belongs to another sequence 
                                last = (last + i) & 3;
                                return this.Write(item, context);
                            }
                        }
                        // no other sequence found. start new sequence.
                        this.encoder.EncodeSymbol(gpstime0diff, 2);
                        this.icGpstime.Compress((int)(lastGpstime[last].UInt64 >> 32), (int)(thisGpstime.UInt64 >> 32), 8);
                        this.encoder.WriteInt((UInt32)(thisGpstime.UInt64));
                        this.next = (next + 1) & 3;
                        this.last = next;
                        this.lastGpstimeDiff[last] = 0;
                        this.multiExtremeCounter[last] = 0;
                    }
                    this.lastGpstime[last].Int64 = thisGpstime.Int64;
                }
            }
            else // the last integer difference was *not* zero
            {
                if (thisGpstime.Int64 == this.lastGpstime[last].Int64)
                {
                    // if the doubles have not changed use a special symbol
                    this.encoder.EncodeSymbol(gpstimeMulti, LasZipGpstimeMultiUnchanged);
                }
                else
                {
                    // calculate the difference between the two doubles as an integer
                    Int64 currGpstimeDiff64 = thisGpstime.Int64 - lastGpstime[last].Int64;
                    int currGpstimeDiff = (int)currGpstimeDiff64;

                    // if the current gpstime difference can be represented with 32 bits
                    if (currGpstimeDiff64 == (Int64)(currGpstimeDiff))
                    {
                        // compute multiplier between current and last integer difference
                        double multiF = (double)currGpstimeDiff / (double)this.lastGpstimeDiff[last];
                        int multi = MyDefs.QuantizeInt32(multiF);

                        // compress the residual curr_gpstime_diff in dependance on the multiplier
                        if (multi == 1)
                        {
                            // this is the case we assume we get most often for regular spaced pulses
                            this.encoder.EncodeSymbol(gpstimeMulti, 1);
                            this.icGpstime.Compress(lastGpstimeDiff[last], currGpstimeDiff, 1);
                            this.multiExtremeCounter[last] = 0;
                        }
                        else if (multi > 0)
                        {
                            if (multi < LasZipGpstimeMulti) // positive multipliers up to LASZIP_GPSTIME_MULTI are compressed directly
                            {
                                this.encoder.EncodeSymbol(gpstimeMulti, (UInt32)multi);
                                if (multi < 10)
                                    this.icGpstime.Compress(multi * lastGpstimeDiff[last], currGpstimeDiff, 2);
                                else
                                    this.icGpstime.Compress(multi * lastGpstimeDiff[last], currGpstimeDiff, 3);
                            }
                            else
                            {
                                this.encoder.EncodeSymbol(gpstimeMulti, LasZipGpstimeMulti);
                                this.icGpstime.Compress(LasZipGpstimeMulti * lastGpstimeDiff[last], currGpstimeDiff, 4);
                                this.multiExtremeCounter[last]++;
                                if (this.multiExtremeCounter[last] > 3)
                                {
                                    this.lastGpstimeDiff[last] = currGpstimeDiff;
                                    this.multiExtremeCounter[last] = 0;
                                }
                            }
                        }
                        else if (multi < 0)
                        {
                            if (multi > LasZipGpstimeMultiMinus) // negative multipliers larger than LASZIP_GPSTIME_MULTI_MINUS are compressed directly
                            {
                                this.encoder.EncodeSymbol(gpstimeMulti, (UInt32)(LasZipGpstimeMulti - multi));
                                this.icGpstime.Compress(multi * lastGpstimeDiff[last], currGpstimeDiff, 5);
                            }
                            else
                            {
                                this.encoder.EncodeSymbol(gpstimeMulti, LasZipGpstimeMulti - LasZipGpstimeMultiMinus);
                                this.icGpstime.Compress(LasZipGpstimeMultiMinus * lastGpstimeDiff[last], currGpstimeDiff, 6);
                                this.multiExtremeCounter[last]++;
                                if (multiExtremeCounter[last] > 3)
                                {
                                    this.lastGpstimeDiff[last] = currGpstimeDiff;
                                    this.multiExtremeCounter[last] = 0;
                                }
                            }
                        }
                        else
                        {
                            this.encoder.EncodeSymbol(gpstimeMulti, 0);
                            this.icGpstime.Compress(0, currGpstimeDiff, 7);
                            this.multiExtremeCounter[last]++;
                            if (this.multiExtremeCounter[last] > 3)
                            {
                                this.lastGpstimeDiff[last] = currGpstimeDiff;
                                this.multiExtremeCounter[last] = 0;
                            }
                        }
                    }
                    else // the difference is huge
                    {
                        // maybe the double belongs to another time sequence
                        for (UInt32 i = 1; i < 4; i++)
                        {
                            Int64 otherGpstimeDiff64 = thisGpstime.Int64 - lastGpstime[(last + i) & 3].Int64;
                            int otherGpstimeDiff = (int)otherGpstimeDiff64;
                            if (otherGpstimeDiff64 == (Int64)(otherGpstimeDiff))
                            {
                                // it belongs to this sequence 
                                this.encoder.EncodeSymbol(gpstimeMulti, LasZipGpstimeMultiCodeFull + i);
                                this.last = (last + i) & 3;
                                return this.Write(item, context);
                            }
                        }
                        // no other sequence found. start new sequence.
                        this.encoder.EncodeSymbol(gpstimeMulti, LasZipGpstimeMultiCodeFull);
                        this.icGpstime.Compress((int)(lastGpstime[last].UInt64 >> 32), (int)(thisGpstime.UInt64 >> 32), 8);
                        this.encoder.WriteInt((UInt32)(thisGpstime.UInt64));
                        this.next = (next + 1) & 3;
                        this.last = next;
                        this.lastGpstimeDiff[last] = 0;
                        this.multiExtremeCounter[last] = 0;
                    }

                    this.lastGpstime[last].Int64 = thisGpstime.Int64;
                }
            }

            return true;
        }
    }
}
