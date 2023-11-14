// lasreaditemcompressed_v2.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasReadItemCompressedPoint10v2 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private LasPoint10 lastItem;

        private readonly UInt16[] lastIntensity;
        private readonly StreamingMedian5[] lastXdiffMedian5;
        private readonly StreamingMedian5[] lastYdiffMedian5;
        private readonly int[] lastHeight;

        private readonly IntegerCompressor icX;
        private readonly IntegerCompressor icY;
        private readonly IntegerCompressor icZ;
        private readonly IntegerCompressor icIntensity;
        private readonly IntegerCompressor icPointSourceID;
        private readonly ArithmeticModel changedValues;
        private readonly ArithmeticModel[] scanAngleRank;
        private readonly ArithmeticModel?[] bitByte;
        private readonly ArithmeticModel?[] classification;
        private readonly ArithmeticModel?[] userData;

        public LasReadItemCompressedPoint10v2(ArithmeticDecoder decoder)
        {
            // set decoder
            this.decoder = decoder;
            this.lastIntensity = new UInt16[16];
            this.lastXdiffMedian5 = new StreamingMedian5[16];
            this.lastYdiffMedian5 = new StreamingMedian5[16];
            this.lastHeight = new int[8];
            this.bitByte = new ArithmeticModel?[256]; // left as null
            this.classification = new ArithmeticModel?[256];
            this.userData = new ArithmeticModel?[256];

            // create models and integer compressors
            this.icX = new IntegerCompressor(decoder, 32, 2); // 32 bits, 2 context
            this.icY = new IntegerCompressor(decoder, 32, 22); // 32 bits, 22 contexts
            this.icZ = new IntegerCompressor(decoder, 32, 20); // 32 bits, 20 contexts
            this.icIntensity = new IntegerCompressor(decoder, 16, 4);
            this.scanAngleRank = new ArithmeticModel[] { ArithmeticDecoder.CreateSymbolModel(256), ArithmeticDecoder.CreateSymbolModel(256) };
            this.icPointSourceID = new IntegerCompressor(decoder, 16);
            this.changedValues = ArithmeticDecoder.CreateSymbolModel(64);
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            for (int i = 0; i < 16; i++)
            {
                lastXdiffMedian5[i].Init();
                lastYdiffMedian5[i].Init();
                lastIntensity[i] = 0;
                lastHeight[i / 2] = 0;
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
            this.lastItem.X = BinaryPrimitives.ReadInt32LittleEndian(item);
            this.lastItem.Y = BinaryPrimitives.ReadInt32LittleEndian(item[4..]);
            this.lastItem.Z = BinaryPrimitives.ReadInt32LittleEndian(item[8..]);
            this.lastItem.Intensity = 0; // set intensity to zero (ignore BinaryPrimitives.ReadUInt16LittleEndian(item[12..]))
            this.lastItem.ReturnNumbersAndFlags = item[14];
            this.lastItem.Classification = item[15];
            this.lastItem.ScanAngleRank = (sbyte)item[16];
            this.lastItem.UserData = item[17];
            this.lastItem.PointSourceID = BinaryPrimitives.ReadUInt16LittleEndian(item[18..]);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            // decompress which other values have changed
            UInt32 changed_values = decoder.DecodeSymbol(this.changedValues);

            byte r, n, m, l;

            if (changed_values != 0)
            {
                // decompress the edge_of_flight_line, scan_direction_flag, ... if it has changed
                if ((changed_values & 32) != 0)
                {
                    if (bitByte[lastItem.ReturnNumbersAndFlags] == null)
                    {
                        bitByte[lastItem.ReturnNumbersAndFlags] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(bitByte[lastItem.ReturnNumbersAndFlags]);
                    }
                    lastItem.ReturnNumbersAndFlags = (byte)decoder.DecodeSymbol(bitByte[lastItem.ReturnNumbersAndFlags]);
                }

                r = (byte)(lastItem.ReturnNumbersAndFlags & 0x7); // return_number
                n = (byte)((lastItem.ReturnNumbersAndFlags >> 3) & 0x7); // number_of_returns_of_given_pulse
                m = LasZipCommonV2.NumberReturnMap[n, r];
                l = LasZipCommonV2.NumberReturnLevel[n, r];

                // decompress the intensity if it has changed
                if ((changed_values & 16) != 0)
                {
                    lastItem.Intensity = (UInt16)icIntensity.Decompress(lastIntensity[m], (m < 3 ? m : 3u));
                    lastIntensity[m] = lastItem.Intensity;
                }
                else
                {
                    lastItem.Intensity = lastIntensity[m];
                }

                // decompress the classification ... if it has changed
                if ((changed_values & 8) != 0)
                {
                    if (classification[lastItem.Classification] == null)
                    {
                        classification[lastItem.Classification] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(classification[lastItem.Classification]);
                    }
                    lastItem.Classification = (byte)decoder.DecodeSymbol(classification[lastItem.Classification]);
                }

                // decompress the scan_angle_rank ... if it has changed
                if ((changed_values & 4) != 0)
                {
                    int val = (int)decoder.DecodeSymbol(scanAngleRank[(lastItem.ReturnNumbersAndFlags & 0x40) != 0 ? 1 : 0]); // scan_direction_flag
                                                                                                          //last->scan_angle_rank=(sbyte)MyDefs.U8_FOLD(val+(byte)last->scan_angle_rank);
                    lastItem.ScanAngleRank = (sbyte)((val + (byte)lastItem.ScanAngleRank) % 256);
                }

                // decompress the user_data ... if it has changed
                if ((changed_values & 2) != 0)
                {
                    if (userData[lastItem.UserData] == null)
                    {
                        userData[lastItem.UserData] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(userData[lastItem.UserData]);
                    }
                    lastItem.UserData = (byte)decoder.DecodeSymbol(userData[lastItem.UserData]);
                }

                // decompress the point_source_ID ... if it has changed
                if ((changed_values & 1) != 0)
                {
                    lastItem.PointSourceID = (UInt16)icPointSourceID.Decompress(lastItem.PointSourceID);
                }
            }
            else
            {
                r = (byte)(lastItem.ReturnNumbersAndFlags & 0x7); // return_number
                n = (byte)((lastItem.ReturnNumbersAndFlags >> 3) & 0x7); // number_of_returns_of_given_pulse
                m = LasZipCommonV2.NumberReturnMap[n, r];
                l = LasZipCommonV2.NumberReturnLevel[n, r];
            }

            // decompress x coordinate
            int median = lastXdiffMedian5[m].Get();
            int diff = icX.Decompress(median, n == 1 ? 1u : 0u);
            lastItem.X += diff;
            lastXdiffMedian5[m].Add(diff);

            // decompress y coordinate
            median = lastYdiffMedian5[m].Get();
            UInt32 kBits = icX.GetK();
            diff = icY.Decompress(median, (n == 1 ? 1u : 0u) + (kBits < 20 ? kBits & 0xFEu : 20u)); // &0xFE round k_bits to next even number
            lastItem.Y += diff;
            lastYdiffMedian5[m].Add(diff);

            // decompress z coordinate
            kBits = (icX.GetK() + icY.GetK()) / 2;
            this.lastItem.Z = icZ.Decompress(lastHeight[l], (n == 1 ? 1u : 0u) + (kBits < 18 ? kBits & 0xFEu : 18u)); // &0xFE round k_bits to next even number
            this.lastHeight[l] = this.lastItem.Z;

            // copy the last point
            BinaryPrimitives.WriteInt32LittleEndian(item, this.lastItem.X);
            BinaryPrimitives.WriteInt32LittleEndian(item[4..], this.lastItem.Y);
            BinaryPrimitives.WriteInt32LittleEndian(item[8..], this.lastItem.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(item[12..], this.lastItem.Intensity);
            item[14] = this.lastItem.ReturnNumbersAndFlags;
            item[15] = this.lastItem.Classification;
            item[16] = (byte)this.lastItem.ScanAngleRank;
            item[17] = this.lastItem.UserData;
            BinaryPrimitives.WriteUInt16LittleEndian(item[18..], this.lastItem.PointSourceID);

            return true;
        }
    }
}
