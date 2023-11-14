// laswriteitemcompressed_v2.{hpp, cpp}
using System;
using System.Buffers.Binary;

namespace LasZip
{
    internal class LasWriteItemCompressedPoint10v2 : LasWriteItemCompressed
    {
        private readonly ArithmeticEncoder encoder;
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

        public LasWriteItemCompressedPoint10v2(ArithmeticEncoder encoder)
        {
            this.encoder = encoder;
            this.lastItem = new();
            this.lastIntensity = new UInt16[16];
            this.lastXdiffMedian5 = new StreamingMedian5[16];
            this.lastYdiffMedian5 = new StreamingMedian5[16];
            this.lastHeight = new int[8];
            this.bitByte = new ArithmeticModel?[256]; // left as null
            this.classification = new ArithmeticModel?[256];
            this.userData = new ArithmeticModel?[256];

            // create models and integer compressors
            this.icX = new(encoder, 32, 2); // 32 bits, 2 context
            this.icY = new(encoder, 32, 22); // 32 bits, 22 contexts
            this.icZ = new(encoder, 32, 20); // 32 bits, 20 contexts
            this.icIntensity = new(encoder, 16, 4);
            this.scanAngleRank = new ArithmeticModel[] { ArithmeticEncoder.CreateSymbolModel(256), ArithmeticEncoder.CreateSymbolModel(256) };
            this.icPointSourceID = new(encoder, 16);
            this.changedValues = ArithmeticEncoder.CreateSymbolModel(64);
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
            icX.InitCompressor();
            icY.InitCompressor();
            icZ.InitCompressor();
            icIntensity.InitCompressor();
            ArithmeticEncoder.InitSymbolModel(scanAngleRank[0]);
            ArithmeticEncoder.InitSymbolModel(scanAngleRank[1]);
            icPointSourceID.InitCompressor();
            ArithmeticEncoder.InitSymbolModel(changedValues);
            for (int i = 0; i < 256; i++)
            {
                if (bitByte[i] != null) { ArithmeticEncoder.InitSymbolModel(bitByte[i]); }
                if (classification[i] != null) { ArithmeticEncoder.InitSymbolModel(classification[i]); }
                if (userData[i] != null) { ArithmeticEncoder.InitSymbolModel(userData[i]); }
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

        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            UInt16 intensity = BinaryPrimitives.ReadUInt16LittleEndian(item[12..]);
            byte returnNumbersAndFlags = item[14];
            UInt32 returnNumber = (UInt32)(returnNumbersAndFlags & 0x03);
            UInt32 numberOfReturns = (UInt32)((returnNumbersAndFlags & 0x38) >> 3);
            byte classificationAndFlags = item[15];
            sbyte scanAngleRank = (sbyte)item[16];
            byte userData = item[17];
            UInt16 pointSourceID = BinaryPrimitives.ReadUInt16LittleEndian(item[18..]);

            UInt32 m = LasZipCommonV2.NumberReturnMap[numberOfReturns, returnNumber];
            UInt32 l = LasZipCommonV2.NumberReturnLevel[numberOfReturns, returnNumber];

            // compress which other values have changed
            UInt32 changedValues = 0;

            bool needFlags = lastItem.ReturnNumbersAndFlags != returnNumbersAndFlags; 
            if (needFlags) 
                changedValues |= 32; // bit_byte
            bool needIntensity = lastIntensity[m] != intensity;
            if (needIntensity) 
                changedValues |= 16;
            bool needClassification = lastItem.Classification != classificationAndFlags; 
            if (needClassification) 
                changedValues |= 8;
            bool needScanAngleRank = lastItem.ScanAngleRank != scanAngleRank;
            if (needScanAngleRank) 
                changedValues |= 4;
            bool needUserData = lastItem.UserData != userData; 
            if (needUserData) 
                changedValues |= 2;
            bool needPointSourceID = lastItem.PointSourceID != pointSourceID; 
            if (needPointSourceID) 
                changedValues |= 1;

            this.encoder.EncodeSymbol(this.changedValues, changedValues);

            // compress the bit_byte (edge_of_flight_line, scan_direction_flag, returns, ...) if it has changed
            if (needFlags)
            {
                if (this.bitByte[this.lastItem.ReturnNumbersAndFlags] == null)
                {
                    this.bitByte[this.lastItem.ReturnNumbersAndFlags] = ArithmeticEncoder.CreateSymbolModel(256);
                    ArithmeticEncoder.InitSymbolModel(this.bitByte[this.lastItem.ReturnNumbersAndFlags]);
                }
                encoder.EncodeSymbol(this.bitByte[this.lastItem.ReturnNumbersAndFlags], returnNumbersAndFlags);
            }

            // compress the intensity if it has changed
            if (needIntensity)
            {
                this.icIntensity.Compress(lastIntensity[m], intensity, (m < 3 ? m : 3u));
                this.lastIntensity[m] = intensity;
            }

            // compress the classification ... if it has changed
            if (needClassification)
            {
                if (this.classification[this.lastItem.Classification] == null)
                {
                    this.classification[this.lastItem.Classification] = ArithmeticEncoder.CreateSymbolModel(256);
                    ArithmeticEncoder.InitSymbolModel(this.classification[this.lastItem.Classification]);
                }
                encoder.EncodeSymbol(classification[this.lastItem.Classification], classificationAndFlags);
            }

            // compress the scan_angle_rank ... if it has changed
            if (needScanAngleRank)
            {
                int scanDirectionFlag = (returnNumbersAndFlags & 0x40) >> 6;
                encoder.EncodeSymbol(this.scanAngleRank[scanDirectionFlag], (UInt32)MyDefs.FoldUInt8(scanAngleRank - lastItem.ScanAngleRank));
            }

            // compress the user_data ... if it has changed
            if (needUserData)
            {
                if (this.userData[this.lastItem.UserData] == null)
                {
                    this.userData[this.lastItem.UserData] = ArithmeticEncoder.CreateSymbolModel(256);
                    ArithmeticEncoder.InitSymbolModel(this.userData[this.lastItem.UserData]);
                }
                this.encoder.EncodeSymbol(this.userData[this.lastItem.UserData], userData);
            }

            // compress the point_source_ID ... if it has changed
            if (needPointSourceID)
            {
                this.icPointSourceID.Compress(this.lastItem.PointSourceID, pointSourceID);
            }

            // compress x coordinate
            Int32 x = BinaryPrimitives.ReadInt32LittleEndian(item);
            int median = this.lastXdiffMedian5[m].Get();
            int diff = x - lastItem.X;
            this.icX.Compress(median, diff, numberOfReturns == 1 ? 1u : 0u);
            this.lastXdiffMedian5[m].Add(diff);

            // compress y coordinate
            Int32 y = BinaryPrimitives.ReadInt32LittleEndian(item[4..]);
            UInt32 kBits = icX.GetK();
            median = this.lastYdiffMedian5[m].Get();
            diff = y - this.lastItem.Y;
            this.icY.Compress(median, diff, (numberOfReturns == 1 ? 1u : 0u) + (kBits < 20 ? kBits & 0xFEu : 20u)); // &0xFE round k_bits to next even number
            this.lastYdiffMedian5[m].Add(diff);

            // compress z coordinate
            Int32 z = BinaryPrimitives.ReadInt32LittleEndian(item[4..]);
            kBits = (icX.GetK() + icY.GetK()) / 2;
            this.icZ.Compress(this.lastHeight[l], z, (numberOfReturns == 1 ? 1u : 0u) + (kBits < 18 ? kBits & 0xFEu : 18u)); // &0xFE round k_bits to next even number
            this.lastHeight[l] = z;

            // copy the last point
            lastItem.X = x;
            lastItem.Y = y;
            lastItem.Z = z;
            lastItem.Intensity = intensity;
            lastItem.ReturnNumbersAndFlags = returnNumbersAndFlags;
            lastItem.Classification = classificationAndFlags;
            lastItem.ScanAngleRank = scanAngleRank;
            lastItem.UserData = userData;
            lastItem.PointSourceID = pointSourceID;

            return true;
        }
    }
}
