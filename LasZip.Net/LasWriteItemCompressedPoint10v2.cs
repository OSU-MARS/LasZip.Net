//===============================================================================
//
//  FILE:  laswriteitemcompressed_point10_v2.cs
//
//  CONTENTS:
//
//    Implementation of LASwriteItemCompressed for POINT10 items (version 2).
//
//  PROGRAMMERS:
//
//    martin.isenburg@rapidlasso.com  -  http://rapidlasso.com
//
//  COPYRIGHT:
//
//    (c) 2005-2012, martin isenburg, rapidlasso - tools to catch reality
//    (c) of the C# port 2014 by Shinta <shintadono@googlemail.com>
//
//    This is free software; you can redistribute and/or modify it under the
//    terms of the GNU Lesser General Licence as published by the Free Software
//    Foundation. See the COPYING file for more information.
//
//    This software is distributed WITHOUT ANY WARRANTY and without even the
//    implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
//  CHANGE HISTORY: omitted for easier Copy&Paste (pls see the original)
//
//===============================================================================

using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasWriteItemCompressedPoint10v2 : LasWriteItemCompressed
    {
        private readonly ArithmeticEncoder enc;
        private LasPoint10 last = new();

        private readonly UInt16[] last_intensity = new UInt16[16];
        private readonly StreamingMedian5[] lastXdiffMedian5 = new StreamingMedian5[16];
        private readonly StreamingMedian5[] lastYdiffMedian5 = new StreamingMedian5[16];
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

        public LasWriteItemCompressedPoint10v2(ArithmeticEncoder enc)
        {
            // set encoder
            Debug.Assert(enc != null);
            this.enc = enc;

            // create models and integer compressors
            icX = new IntegerCompressor(enc, 32, 2); // 32 bits, 2 context
            icY = new IntegerCompressor(enc, 32, 22); // 32 bits, 22 contexts
            icZ = new IntegerCompressor(enc, 32, 20); // 32 bits, 20 contexts
            icIntensity = new IntegerCompressor(enc, 16, 4);
            scanAngleRank[0] = ArithmeticEncoder.CreateSymbolModel(256);
            scanAngleRank[1] = ArithmeticEncoder.CreateSymbolModel(256);
            icPointSourceID = new IntegerCompressor(enc, 16);
            changedValues = ArithmeticEncoder.CreateSymbolModel(64);
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
                lastXdiffMedian5[i].Init();
                lastYdiffMedian5[i].Init();
                last_intensity[i] = 0;
                last_height[i / 2] = 0;
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
                if (bitByte[i] != null) ArithmeticEncoder.InitSymbolModel(bitByte[i]);
                if (classification[i] != null) ArithmeticEncoder.InitSymbolModel(classification[i]);
                if (userData[i] != null) ArithmeticEncoder.InitSymbolModel(userData[i]);
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

        public override bool Write(LasPoint item)
        {
            UInt32 r = item.ReturnNumber;
            UInt32 n = item.NumberOfReturnsOfGivenPulse;
            UInt32 m = LasZipCommonV2.NumberReturnMap[n, r];
            UInt32 l = LasZipCommonV2.NumberReturnLevel[n, r];

            // compress which other values have changed
            UInt32 changed_values = 0;

            bool needFlags = last.ReturnNumbersAndFlags != item.ReturnNumbersAndFlags; if (needFlags) changed_values |= 32; // bit_byte
            bool needIntensity = last_intensity[m] != item.Intensity; if (needIntensity) changed_values |= 16;
            bool needClassification = last.Classification != item.ClassificationAndFlags; if (needClassification) changed_values |= 8;
            bool needScanAngleRank = last.ScanAngleRank != item.ScanAngleRank; if (needScanAngleRank) changed_values |= 4;
            bool needUserData = last.UserData != item.UserData; if (needUserData) changed_values |= 2;
            bool needPointSourceID = last.PointSourceID != item.PointSourceID; if (needPointSourceID) changed_values |= 1;

            enc.EncodeSymbol(changedValues, changed_values);

            // compress the bit_byte (edge_of_flight_line, scan_direction_flag, returns, ...) if it has changed
            if (needFlags)
            {
                if (bitByte[last.ReturnNumbersAndFlags] == null)
                {
                    bitByte[last.ReturnNumbersAndFlags] = ArithmeticEncoder.CreateSymbolModel(256);
                    ArithmeticEncoder.InitSymbolModel(bitByte[last.ReturnNumbersAndFlags]);
                }
                enc.EncodeSymbol(bitByte[last.ReturnNumbersAndFlags], item.ReturnNumbersAndFlags);
            }

            // compress the intensity if it has changed
            if (needIntensity)
            {
                icIntensity.Compress(last_intensity[m], item.Intensity, (m < 3 ? m : 3u));
                last_intensity[m] = item.Intensity;
            }

            // compress the classification ... if it has changed
            if (needClassification)
            {
                if (classification[last.Classification] == null)
                {
                    classification[last.Classification] = ArithmeticEncoder.CreateSymbolModel(256);
                    ArithmeticEncoder.InitSymbolModel(classification[last.Classification]);
                }
                enc.EncodeSymbol(classification[last.Classification], item.ClassificationAndFlags);
            }

            // compress the scan_angle_rank ... if it has changed
            if (needScanAngleRank)
            {
                enc.EncodeSymbol(scanAngleRank[item.ScanDirectionFlag], (UInt32)MyDefs.FoldUint8(item.ScanAngleRank - last.ScanAngleRank));
            }

            // compress the user_data ... if it has changed
            if (needUserData)
            {
                if (userData[last.UserData] == null)
                {
                    userData[last.UserData] = ArithmeticEncoder.CreateSymbolModel(256);
                    ArithmeticEncoder.InitSymbolModel(userData[last.UserData]);
                }
                enc.EncodeSymbol(userData[last.UserData], item.UserData);
            }

            // compress the point_source_ID ... if it has changed
            if (needPointSourceID)
            {
                icPointSourceID.Compress(last.PointSourceID, item.PointSourceID);
            }

            // compress x coordinate
            int median = lastXdiffMedian5[m].Get();
            int diff = item.X - last.X;
            icX.Compress(median, diff, n == 1 ? 1u : 0u);
            lastXdiffMedian5[m].Add(diff);

            // compress y coordinate
            UInt32 k_bits = icX.GetK();
            median = lastYdiffMedian5[m].Get();
            diff = item.Y - last.Y;
            icY.Compress(median, diff, (n == 1 ? 1u : 0u) + (k_bits < 20 ? k_bits & 0xFEu : 20u)); // &0xFE round k_bits to next even number
            lastYdiffMedian5[m].Add(diff);

            // compress z coordinate
            k_bits = (icX.GetK() + icY.GetK()) / 2;
            icZ.Compress(last_height[l], item.Z, (n == 1 ? 1u : 0u) + (k_bits < 18 ? k_bits & 0xFEu : 18u)); // &0xFE round k_bits to next even number
            last_height[l] = item.Z;

            // copy the last point
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
    }
}
