// lasreaditemcompressed_v1.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasReadItemCompressedPoint10v1 : LasReadItemCompressed
    {
        private readonly ArithmeticDecoder decoder;
        private LasPoint10 lastItem;

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

        public LasReadItemCompressedPoint10v1(ArithmeticDecoder decoder)
        {
            // set decoder
            this.decoder = decoder;
            this.lastItem = new();
            this.bitByte = new ArithmeticModel?[256]; // left as null
            this.classification = new ArithmeticModel?[256]; // left as null
            this.userData = new ArithmeticModel?[256]; // left as null

            // create models and integer compressors
            this.icX = new IntegerCompressor(decoder, 32); // 32 bits, 1 context
            this.icY = new IntegerCompressor(decoder, 32, 20); // 32 bits, 20 contexts
            this.icZ = new IntegerCompressor(decoder, 32, 20); // 32 bits, 20 contexts
            this.icIntensity = new IntegerCompressor(decoder, 16);
            this.icScanAngleRank = new IntegerCompressor(decoder, 8, 2);
            this.icPointSourceID = new IntegerCompressor(decoder, 16);
            this.changedValues = ArithmeticDecoder.CreateSymbolModel(64);
        }

        public override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            this.lastXdiff[0] = this.lastXdiff[1] = this.lastXdiff[2] = 0;
            this.lastYdiff[0] = this.lastYdiff[1] = this.lastYdiff[2] = 0;
            this.lastIncr = 0;

            // init models and integer compressors
            this.icX.InitDecompressor();
            this.icY.InitDecompressor();
            this.icZ.InitDecompressor();
            this.icIntensity.InitDecompressor();
            this.icScanAngleRank.InitDecompressor();
            this.icPointSourceID.InitDecompressor();
            ArithmeticDecoder.InitSymbolModel(this.changedValues);
            for (int i = 0; i < 256; i++)
            {
                if (this.bitByte[i] != null) { ArithmeticDecoder.InitSymbolModel(this.bitByte[i]); }
                if (this.classification[i] != null) { ArithmeticDecoder.InitSymbolModel(this.classification[i]); }
                if (this.userData[i] != null) { ArithmeticDecoder.InitSymbolModel(this.userData[i]); }
            }

            // init lastItem
            this.lastItem.X = BinaryPrimitives.ReadInt32LittleEndian(item);
            this.lastItem.Y = BinaryPrimitives.ReadInt32LittleEndian(item[4..]);
            this.lastItem.Z = BinaryPrimitives.ReadInt32LittleEndian(item[8..]);
            this.lastItem.Intensity = BinaryPrimitives.ReadUInt16LittleEndian(item[12..]);
            this.lastItem.ReturnNumbersAndFlags = item[14];
            this.lastItem.Classification = item[15];
            this.lastItem.ScanAngleRank = (sbyte)item[16];
            this.lastItem.UserData = item[17];
            this.lastItem.PointSourceID = BinaryPrimitives.ReadUInt16LittleEndian(item[18..]);

            return true;
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            // find median difference for x and y from 3 preceding differences
            int median_x;
            if (lastXdiff[0] < lastXdiff[1])
            {
                if (lastXdiff[1] < lastXdiff[2])
                    median_x = lastXdiff[1];
                else if (lastXdiff[0] < lastXdiff[2]) 
                    median_x = lastXdiff[2];
                else 
                    median_x = lastXdiff[0];
            }
            else
            {
                if (lastXdiff[0] < lastXdiff[2]) 
                    median_x = lastXdiff[0];
                else if (lastXdiff[1] < lastXdiff[2])
                    median_x = lastXdiff[2];
                else
                    median_x = lastXdiff[1];
            }

            int median_y;
            if (lastYdiff[0] < lastYdiff[1])
            {
                if (lastYdiff[1] < lastYdiff[2]) 
                    median_y = lastYdiff[1];
                else if (lastYdiff[0] < lastYdiff[2])
                    median_y = lastYdiff[2];
                else
                    median_y = lastYdiff[0];
            }
            else
            {
                if (lastYdiff[0] < lastYdiff[2])
                    median_y = lastYdiff[0];
                else if (lastYdiff[1] < lastYdiff[2]) 
                    median_y = lastYdiff[2];
                else
                    median_y = lastYdiff[1];
            }

            // decompress x y z coordinates
            int xDiff = icX.Decompress(median_x);
            lastItem.X += xDiff;

            // we use the number k of bits corrector bits to switch contexts
            UInt32 k_bits = icX.GetK();
            int yDiff = icY.Decompress(median_y, (k_bits < 19 ? k_bits : 19u));
            lastItem.Y += yDiff;

            k_bits = (k_bits + icY.GetK()) / 2;
            lastItem.Z = icZ.Decompress(lastItem.Z, (k_bits < 19 ? k_bits : 19u));

            // decompress which other values have changed
            UInt32 changed_values = decoder.DecodeSymbol(changedValues);

            if (changed_values != 0)
            {
                // decompress the intensity if it has changed
                if ((changed_values & 32) != 0)
                {
                    lastItem.Intensity = (UInt16)icIntensity.Decompress(lastItem.Intensity);
                }

                // decompress the edge_of_flight_line, scan_direction_flag, ... if it has changed
                if ((changed_values & 16) != 0)
                {
                    if (bitByte[lastItem.ReturnNumbersAndFlags] == null)
                    {
                        bitByte[lastItem.ReturnNumbersAndFlags] = ArithmeticDecoder.CreateSymbolModel(256);
                        ArithmeticDecoder.InitSymbolModel(bitByte[lastItem.ReturnNumbersAndFlags]);
                    }
                    lastItem.ReturnNumbersAndFlags = (byte)decoder.DecodeSymbol(bitByte[lastItem.ReturnNumbersAndFlags]);
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
                    lastItem.ScanAngleRank = (sbyte)(byte)icScanAngleRank.Decompress((byte)lastItem.ScanAngleRank, k_bits < 3 ? 1u : 0u);
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

            // record the difference
            this.lastXdiff[lastIncr] = xDiff;
            this.lastYdiff[lastIncr] = yDiff;
            this.lastIncr++;
            if (this.lastIncr > 2) 
                lastIncr = 0;

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
