// lasreaditemcompressed_v1.{hpp, cpp}
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedGpstime11v1 : LasReadItemCompressed
    {
        private const int LasZipGpstimeMultimax = 512;

        private readonly ArithmeticDecoder dec;
        private Interpretable64 lastGpstime;

        private readonly ArithmeticModel gpstimeMulti;
        private readonly ArithmeticModel gpstime0diff;
        private readonly IntegerCompressor icGpstime;
        private int multiExtremeCounter;
        private int lastGpstimeDiff;

        public LasReadItemCompressedGpstime11v1(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;

            // create entropy models and integer compressors
            gpstimeMulti = ArithmeticDecoder.CreateSymbolModel(LasZipGpstimeMultimax);
            gpstime0diff = ArithmeticDecoder.CreateSymbolModel(3);
            icGpstime = new IntegerCompressor(dec, 32, 6); // 32 bits, 6 contexts
        }

        public override bool Init(LasPoint item)
        {
            // init state
            lastGpstimeDiff = 0;
            multiExtremeCounter = 0;

            // init models and integer compressors
            ArithmeticDecoder.InitSymbolModel(gpstimeMulti);
            ArithmeticDecoder.InitSymbolModel(gpstime0diff);
            icGpstime.InitDecompressor();

            // init last item
            lastGpstime.Double = item.Gpstime;

            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            if (lastGpstimeDiff == 0) // if the last integer difference was zero
            {
                int multi = (int)dec.DecodeSymbol(gpstime0diff);
                if (multi == 1) // the difference can be represented with 32 bits
                {
                    lastGpstimeDiff = icGpstime.Decompress(0, 0);
                    lastGpstime.Int64 += lastGpstimeDiff;
                }
                else if (multi == 2) // the difference is huge
                {
                    lastGpstime.UInt64 = dec.ReadInt64();
                }
            }
            else
            {
                int multi = (int)dec.DecodeSymbol(gpstimeMulti);

                if (multi < LasZipGpstimeMultimax - 2)
                {
                    int gpstime_diff;
                    if (multi == 1)
                    {
                        gpstime_diff = icGpstime.Decompress(lastGpstimeDiff, 1);
                        lastGpstimeDiff = gpstime_diff;
                        multiExtremeCounter = 0;
                    }
                    else if (multi == 0)
                    {
                        gpstime_diff = icGpstime.Decompress(lastGpstimeDiff / 4, 2);
                        multiExtremeCounter++;
                        if (multiExtremeCounter > 3)
                        {
                            lastGpstimeDiff = gpstime_diff;
                            multiExtremeCounter = 0;
                        }
                    }
                    else if (multi < 10)
                    {
                        gpstime_diff = icGpstime.Decompress(multi * lastGpstimeDiff, 3);
                    }
                    else if (multi < 50)
                    {
                        gpstime_diff = icGpstime.Decompress(multi * lastGpstimeDiff, 4);
                    }
                    else
                    {
                        gpstime_diff = icGpstime.Decompress(multi * lastGpstimeDiff, 5);
                        if (multi == LasZipGpstimeMultimax - 3)
                        {
                            multiExtremeCounter++;
                            if (multiExtremeCounter > 3)
                            {
                                lastGpstimeDiff = gpstime_diff;
                                multiExtremeCounter = 0;
                            }
                        }
                    }
                    lastGpstime.Int64 += gpstime_diff;
                }
                else if (multi < LasZipGpstimeMultimax - 1)
                {
                    lastGpstime.UInt64 = dec.ReadInt64();
                }
            }

            item.Gpstime = lastGpstime.Double;
            return true;
        }
    }
}
