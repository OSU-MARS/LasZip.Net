// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedPoint10v1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private LasPoint10 last = new();

        private readonly int[] lastXdiff = new int[3];
        private readonly int[] lastYdiff = new int[3];
        private int lastIncr;
        private readonly IntegerCompressor icX;
        private readonly IntegerCompressor icY;
        private readonly IntegerCompressor icZ;
        private readonly IntegerCompressor icIntensity;
        private readonly IntegerCompressor icScanAngleRank;
        private readonly IntegerCompressor icPointSourceID;

        private readonly ArithmeticModel changedValues;
        private readonly ArithmeticModel?[] bitByte = new ArithmeticModel?[256];
        private readonly ArithmeticModel?[] classification = new ArithmeticModel?[256];
        private readonly ArithmeticModel?[] userData = new ArithmeticModel?[256];

        public LasReadItemCompressedPoint10v1(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;

            // create models and integer compressors
            icX = new IntegerCompressor(dec, 32); // 32 bits, 1 context
            icY = new IntegerCompressor(dec, 32, 20); // 32 bits, 20 contexts
            icZ = new IntegerCompressor(dec, 32, 20); // 32 bits, 20 contexts
            icIntensity = new IntegerCompressor(dec, 16);
            icScanAngleRank = new IntegerCompressor(dec, 8, 2);
            icPointSourceID = new IntegerCompressor(dec, 16);
            changedValues = ArithmeticDecoder.CreateSymbolModel(64);
            for (int i = 0; i < 256; i++)
            {
                bitByte[i] = null;
                classification[i] = null;
                userData[i] = null;
            }
        }

        public override bool Init(LasPoint item)
        {
            // init state
            lastXdiff[0] = lastXdiff[1] = lastXdiff[2] = 0;
            lastYdiff[0] = lastYdiff[1] = lastYdiff[2] = 0;
            lastIncr = 0;

            // init models and integer compressors
            icX.InitDecompressor();
            icY.InitDecompressor();
            icZ.InitDecompressor();
            icIntensity.InitDecompressor();
            icScanAngleRank.InitDecompressor();
            icPointSourceID.InitDecompressor();
            ArithmeticDecoder.InitSymbolModel(changedValues);
            for (int i = 0; i < 256; i++)
            {
                if (bitByte[i] != null) ArithmeticDecoder.InitSymbolModel(bitByte[i]);
                if (classification[i] != null) ArithmeticDecoder.InitSymbolModel(classification[i]);
                if (userData[i] != null) ArithmeticDecoder.InitSymbolModel(userData[i]);
            }

            // init last item
            last.X = item.X;
            last.Y = item.Y;
            last.Z = item.Z;
            last.Intensity = item.Intensity;
            last.ReturnNumbersAndFlags = item.ReturnNumbersAndFlags;
            last.Classification = item.ClassificationAndFlags;
            last.ScanAngleRank = item.ScanAngleRank;
            last.UserData = item.UserData;
            last.PointSourceID = item.PointSourceID;

            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            // find median difference for x and y from 3 preceding differences
            int median_x;
            if (lastXdiff[0] < lastXdiff[1])
            {
                if (lastXdiff[1] < lastXdiff[2]) median_x = lastXdiff[1];
                else if (lastXdiff[0] < lastXdiff[2]) median_x = lastXdiff[2];
                else median_x = lastXdiff[0];
            }
            else
            {
                if (lastXdiff[0] < lastXdiff[2]) median_x = lastXdiff[0];
                else if (lastXdiff[1] < lastXdiff[2]) median_x = lastXdiff[2];
                else median_x = lastXdiff[1];
            }

            int median_y;
            if (lastYdiff[0] < lastYdiff[1])
            {
                if (lastYdiff[1] < lastYdiff[2]) median_y = lastYdiff[1];
                else if (lastYdiff[0] < lastYdiff[2]) median_y = lastYdiff[2];
                else median_y = lastYdiff[0];
            }
            else
            {
                if (lastYdiff[0] < lastYdiff[2]) median_y = lastYdiff[0];
                else if (lastYdiff[1] < lastYdiff[2]) median_y = lastYdiff[2];
                else median_y = lastYdiff[1];
            }

            // decompress x y z coordinates
            int x_diff = icX.Decompress(median_x);
            last.X += x_diff;

            // we use the number k of bits corrector bits to switch contexts
            UInt32 k_bits = icX.GetK();
            int y_diff = icY.Decompress(median_y, (k_bits < 19 ? k_bits : 19u));
            last.Y += y_diff;

            k_bits = (k_bits + icY.GetK()) / 2;
            last.Z = icZ.Decompress(last.Z, (k_bits < 19 ? k_bits : 19u));

            // decompress which other values have changed
            UInt32 changed_values = dec.DecodeSymbol(changedValues);

            if (changed_values != 0)
            {
                // decompress the intensity if it has changed
                if ((changed_values & 32) != 0)
                {
                    last.Intensity = (UInt16)icIntensity.Decompress(last.Intensity);
                }

                // decompress the edge_of_flight_line, scan_direction_flag, ... if it has changed
                if ((changed_values & 16) != 0)
                {
                    if (bitByte[last.ReturnNumbersAndFlags] == null)
                    {
                        bitByte[last.ReturnNumbersAndFlags] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(bitByte[last.ReturnNumbersAndFlags]);
                    }
                    last.ReturnNumbersAndFlags = (byte)dec.DecodeSymbol(bitByte[last.ReturnNumbersAndFlags]);
                }

                // decompress the classification ... if it has changed
                if ((changed_values & 8) != 0)
                {
                    if (classification[last.Classification] == null)
                    {
                        classification[last.Classification] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(classification[last.Classification]);
                    }
                    last.Classification = (byte)dec.DecodeSymbol(classification[last.Classification]);
                }

                // decompress the scan_angle_rank ... if it has changed
                if ((changed_values & 4) != 0)
                {
                    last.ScanAngleRank = (sbyte)(byte)icScanAngleRank.Decompress((byte)last.ScanAngleRank, k_bits < 3 ? 1u : 0u);
                }

                // decompress the user_data ... if it has changed
                if ((changed_values & 2) != 0)
                {
                    if (userData[last.UserData] == null)
                    {
                        userData[last.UserData] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(userData[last.UserData]);
                    }
                    last.UserData = (byte)dec.DecodeSymbol(userData[last.UserData]);
                }

                // decompress the point_source_ID ... if it has changed
                if ((changed_values & 1) != 0)
                {
                    last.PointSourceID = (UInt16)icPointSourceID.Decompress(last.PointSourceID);
                }
            }

            // record the difference
            lastXdiff[lastIncr] = x_diff;
            lastYdiff[lastIncr] = y_diff;
            lastIncr++;
            if (lastIncr > 2) lastIncr = 0;

            // copy the last point
            item.X = last.X;
            item.Y = last.Y;
            item.Z = last.Z;
            item.Intensity = last.Intensity;
            item.ReturnNumbersAndFlags = last.ReturnNumbersAndFlags;
            item.ClassificationAndFlags = last.Classification;
            item.ScanAngleRank = last.ScanAngleRank;
            item.UserData = last.UserData;
            item.PointSourceID = last.PointSourceID;
            return true;
        }
    }
}
