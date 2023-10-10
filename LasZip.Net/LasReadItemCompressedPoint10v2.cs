// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasReadItemCompressedPoint10v2 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder dec;
        private LasPoint10 last = new();

        private readonly UInt16[] last_intensity = new UInt16[16];
        private readonly StreamingMedian5[] last_x_diff_median5 = new StreamingMedian5[16];
        private readonly StreamingMedian5[] last_y_diff_median5 = new StreamingMedian5[16];
        private readonly int[] last_height = new int[8];

        private readonly IntegerCompressor icX;
        private readonly IntegerCompressor icY;
        private readonly IntegerCompressor icZ;
        private readonly IntegerCompressor icIntensity;
        private readonly IntegerCompressor icPointSourceID;
        private readonly ArithmeticModel changedValues;
        private readonly ArithmeticModel[] scanAngleRank = new ArithmeticModel[2];
        private readonly ArithmeticModel?[] bitByte = new ArithmeticModel?[256];
        private readonly ArithmeticModel?[] classification = new ArithmeticModel?[256];
        private readonly ArithmeticModel?[] userData = new ArithmeticModel?[256];

        public LasReadItemCompressedPoint10v2(ArithmeticDecoder dec)
        {
            // set decoder
            Debug.Assert(dec != null);
            this.dec = dec;

            // create models and integer compressors
            icX = new IntegerCompressor(dec, 32, 2); // 32 bits, 2 context
            icY = new IntegerCompressor(dec, 32, 22); // 32 bits, 22 contexts
            icZ = new IntegerCompressor(dec, 32, 20); // 32 bits, 20 contexts
            icIntensity = new IntegerCompressor(dec, 16, 4);
            scanAngleRank[0] = ArithmeticDecoder.CreateSymbolModel(256);
            scanAngleRank[1] = ArithmeticDecoder.CreateSymbolModel(256);
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
            for (int i = 0; i < 16; i++)
            {
                last_x_diff_median5[i].Init();
                last_y_diff_median5[i].Init();
                last_intensity[i] = 0;
                last_height[i / 2] = 0;
            }

            // init models and integer compressors
            icX.InitDecompressor();
            icY.InitDecompressor();
            icZ.InitDecompressor();
            icIntensity.InitDecompressor();
            ArithmeticDecoder.InitSymbolModel(scanAngleRank[0]);
            ArithmeticDecoder.InitSymbolModel(scanAngleRank[1]);
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
            last.Intensity = 0; // but set intensity to zero
            last.ReturnNumbersAndFlags = item.ReturnNumbersAndFlags;
            last.Classification = item.ClassificationAndFlags;
            last.ScanAngleRank = item.ScanAngleRank;
            last.UserData = item.UserData;
            last.PointSourceID = item.PointSourceID;

            return true;
        }

        public override bool TryRead(LasPoint item)
        {
            // decompress which other values have changed
            UInt32 changed_values = dec.DecodeSymbol(changedValues);

            byte r, n, m, l;

            if (changed_values != 0)
            {
                // decompress the edge_of_flight_line, scan_direction_flag, ... if it has changed
                if ((changed_values & 32) != 0)
                {
                    if (bitByte[last.ReturnNumbersAndFlags] == null)
                    {
                        bitByte[last.ReturnNumbersAndFlags] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(bitByte[last.ReturnNumbersAndFlags]);
                    }
                    last.ReturnNumbersAndFlags = (byte)dec.DecodeSymbol(bitByte[last.ReturnNumbersAndFlags]);
                }

                r = (byte)(last.ReturnNumbersAndFlags & 0x7); // return_number
                n = (byte)((last.ReturnNumbersAndFlags >> 3) & 0x7); // number_of_returns_of_given_pulse
                m = LasZipCommonV2.NumberReturnMap[n, r];
                l = LasZipCommonV2.NumberReturnLevel[n, r];

                // decompress the intensity if it has changed
                if ((changed_values & 16) != 0)
                {
                    last.Intensity = (UInt16)icIntensity.Decompress(last_intensity[m], (m < 3 ? m : 3u));
                    last_intensity[m] = last.Intensity;
                }
                else
                {
                    last.Intensity = last_intensity[m];
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
                    int val = (int)dec.DecodeSymbol(scanAngleRank[(last.ReturnNumbersAndFlags & 0x40) != 0 ? 1 : 0]); // scan_direction_flag
                                                                                                          //last->scan_angle_rank=(sbyte)MyDefs.U8_FOLD(val+(byte)last->scan_angle_rank);
                    last.ScanAngleRank = (sbyte)((val + (byte)last.ScanAngleRank) % 256);
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
            else
            {
                r = (byte)(last.ReturnNumbersAndFlags & 0x7); // return_number
                n = (byte)((last.ReturnNumbersAndFlags >> 3) & 0x7); // number_of_returns_of_given_pulse
                m = LasZipCommonV2.NumberReturnMap[n, r];
                l = LasZipCommonV2.NumberReturnLevel[n, r];
            }

            // decompress x coordinate
            int median = last_x_diff_median5[m].Get();
            int diff = icX.Decompress(median, n == 1 ? 1u : 0u);
            last.X += diff;
            last_x_diff_median5[m].Add(diff);

            // decompress y coordinate
            median = last_y_diff_median5[m].Get();
            UInt32 k_bits = icX.GetK();
            diff = icY.Decompress(median, (n == 1 ? 1u : 0u) + (k_bits < 20 ? k_bits & 0xFEu : 20u)); // &0xFE round k_bits to next even number
            last.Y += diff;
            last_y_diff_median5[m].Add(diff);

            // decompress z coordinate
            k_bits = (icX.GetK() + icY.GetK()) / 2;
            last.Z = icZ.Decompress(last_height[l], (n == 1 ? 1u : 0u) + (k_bits < 18 ? k_bits & 0xFEu : 18u)); // &0xFE round k_bits to next even number
            last_height[l] = last.Z;

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
