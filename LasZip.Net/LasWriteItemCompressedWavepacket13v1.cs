//===============================================================================
//
//  FILE:  laswriteitemcompressed_wavepacket13_v1.cs
//
//  CONTENTS:
//
//    Implementation of LASwriteItemCompressed for WAVEPACKET13 items (version 1).
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
    internal class LasWriteItemCompressedWavepacket13v1 : LasWriteItemCompressed
    {
        private readonly ArithmeticEncoder enc;
        private LasWavepacket13 lastItem;

        private int lastDiff32;
        private UInt32 symLastOffsetDiff;
        private readonly ArithmeticModel packetIndex;
        private readonly ArithmeticModel[] offsetDiff = new ArithmeticModel[4];
        private readonly IntegerCompressor icOffsetDiff;
        private readonly IntegerCompressor icPacketSize;
        private readonly IntegerCompressor icReturnPoint;
        private readonly IntegerCompressor icXyz;

        public LasWriteItemCompressedWavepacket13v1(ArithmeticEncoder enc)
        {
            // set encoder
            Debug.Assert(enc != null);
            this.enc = enc;

            // create models and integer compressors
            packetIndex = ArithmeticEncoder.CreateSymbolModel(256);
            offsetDiff[0] = ArithmeticEncoder.CreateSymbolModel(4);
            offsetDiff[1] = ArithmeticEncoder.CreateSymbolModel(4);
            offsetDiff[2] = ArithmeticEncoder.CreateSymbolModel(4);
            offsetDiff[3] = ArithmeticEncoder.CreateSymbolModel(4);
            icOffsetDiff = new IntegerCompressor(enc, 32);
            icPacketSize = new IntegerCompressor(enc, 32);
            icReturnPoint = new IntegerCompressor(enc, 32);
            icXyz = new IntegerCompressor(enc, 32, 3);
        }

        public unsafe override bool Init(LasPoint item)
        {
            // init state
            lastDiff32 = 0;
            symLastOffsetDiff = 0;

            // init models and integer compressors
            ArithmeticEncoder.InitSymbolModel(packetIndex);
            ArithmeticEncoder.InitSymbolModel(offsetDiff[0]);
            ArithmeticEncoder.InitSymbolModel(offsetDiff[1]);
            ArithmeticEncoder.InitSymbolModel(offsetDiff[2]);
            ArithmeticEncoder.InitSymbolModel(offsetDiff[3]);
            icOffsetDiff.InitCompressor();
            icPacketSize.InitCompressor();
            icReturnPoint.InitCompressor();
            icXyz.InitCompressor();

            // init last item
            fixed (byte* pItem = item.Wavepacket)
            {
                lastItem = *(LasWavepacket13*)(pItem + 1);
            }

            return true;
        }

        public unsafe override bool Write(LasPoint item)
        {
            enc.EncodeSymbol(packetIndex, item.Wavepacket[0]);

            fixed (byte* pItem = item.Wavepacket)
            {
                LasWavepacket13* wave = (LasWavepacket13*)(pItem + 1);

                // calculate the difference between the two offsets
                Int64 curr_diff_64 = (Int64)(wave->Offset - lastItem.Offset);
                int curr_diff_32 = (int)curr_diff_64;

                // if the current difference can be represented with 32 bits
                if (curr_diff_64 == (Int64)(curr_diff_32))
                {
                    if (curr_diff_32 == 0) // current difference is zero
                    {
                        enc.EncodeSymbol(offsetDiff[symLastOffsetDiff], 0);
                        symLastOffsetDiff = 0;
                    }
                    else if (curr_diff_32 == (int)lastItem.PacketSize) // current difference is size of last packet
                    {
                        enc.EncodeSymbol(offsetDiff[symLastOffsetDiff], 1);
                        symLastOffsetDiff = 1;
                    }
                    else // 
                    {
                        enc.EncodeSymbol(offsetDiff[symLastOffsetDiff], 2);
                        symLastOffsetDiff = 2;
                        icOffsetDiff.Compress(lastDiff32, curr_diff_32);
                        lastDiff32 = curr_diff_32;
                    }
                }
                else
                {
                    enc.EncodeSymbol(offsetDiff[symLastOffsetDiff], 3);
                    symLastOffsetDiff = 3;
                    enc.WriteInt64(wave->Offset);
                }

                icPacketSize.Compress((int)lastItem.PacketSize, (int)wave->PacketSize);
                icReturnPoint.Compress(lastItem.ReturnPoint.Int32, wave->ReturnPoint.Int32);
                icXyz.Compress(lastItem.X.Int32, wave->X.Int32, 0);
                icXyz.Compress(lastItem.Y.Int32, wave->Y.Int32, 1);
                icXyz.Compress(lastItem.Z.Int32, wave->Z.Int32, 2);

                lastItem = *wave;
            }

            return true;
        }
    }
}
