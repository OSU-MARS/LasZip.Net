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
using System.Buffers.Binary;
using System.Diagnostics;

namespace LasZip
{
    internal class LasWriteItemCompressedWavepacket13v1 : LasWriteItemCompressed
    {
        private readonly ArithmeticEncoder encoder;
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
            this.encoder = enc;

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

        public unsafe override bool Init(ReadOnlySpan<byte> item, UInt32 context)
        {
            // init state
            this.lastDiff32 = 0;
            this.symLastOffsetDiff = 0;

            // init models and integer compressors
            ArithmeticEncoder.InitSymbolModel(this.packetIndex);
            ArithmeticEncoder.InitSymbolModel(this.offsetDiff[0]);
            ArithmeticEncoder.InitSymbolModel(this.offsetDiff[1]);
            ArithmeticEncoder.InitSymbolModel(this.offsetDiff[2]);
            ArithmeticEncoder.InitSymbolModel(this.offsetDiff[3]);
            this.icOffsetDiff.InitCompressor();
            this.icPacketSize.InitCompressor();
            this.icReturnPoint.InitCompressor();
            this.icXyz.InitCompressor();

            // init last item
            this.lastItem.Offset = BinaryPrimitives.ReadUInt64LittleEndian(item);
            this.lastItem.PacketSize = BinaryPrimitives.ReadUInt32LittleEndian(item[8..]);
            this.lastItem.ReturnPoint.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[12..]);
            this.lastItem.X.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[16..]);
            this.lastItem.Y.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[20..]);
            this.lastItem.Z.Int32 = BinaryPrimitives.ReadInt32LittleEndian(item[24..]);

            return true;
        }

        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            this.encoder.EncodeSymbol(packetIndex, item[0]); // wavepacket descriptor index

            // calculate the difference between the two offsets
            UInt64 offset = BinaryPrimitives.ReadUInt64LittleEndian(item[1..]);
            Int64 currDiff64 = (Int64)(offset - lastItem.Offset);
            int currDiff32 = (int)currDiff64;

            // if the current difference can be represented with 32 bits
            if (currDiff64 == (Int64)(currDiff32))
            {
                if (currDiff32 == 0) // current difference is zero
                {
                    this.encoder.EncodeSymbol(offsetDiff[symLastOffsetDiff], 0);
                    symLastOffsetDiff = 0;
                }
                else if (currDiff32 == (int)lastItem.PacketSize) // current difference is size of last packet
                {
                    this.encoder.EncodeSymbol(offsetDiff[symLastOffsetDiff], 1);
                    symLastOffsetDiff = 1;
                }
                else
                {
                    this.encoder.EncodeSymbol(offsetDiff[symLastOffsetDiff], 2);
                    symLastOffsetDiff = 2;
                    this.icOffsetDiff.Compress(lastDiff32, currDiff32);
                    lastDiff32 = currDiff32;
                }
            }
            else
            {
                this.encoder.EncodeSymbol(offsetDiff[symLastOffsetDiff], 3);
                symLastOffsetDiff = 3;
                this.encoder.WriteInt64(offset);
            }

            UInt32 packetSize = BinaryPrimitives.ReadUInt32LittleEndian(item[9..]);
            Int32 returnPoint = BinaryPrimitives.ReadInt32LittleEndian(item[13..]);
            Int32 x = BinaryPrimitives.ReadInt32LittleEndian(item[17..]);
            Int32 y = BinaryPrimitives.ReadInt32LittleEndian(item[21..]);
            Int32 z = BinaryPrimitives.ReadInt32LittleEndian(item[25..]);

            this.icPacketSize.Compress((int)lastItem.PacketSize, (int)packetSize);
            this.icReturnPoint.Compress(lastItem.ReturnPoint.Int32, returnPoint);
            this.icXyz.Compress(lastItem.X.Int32, x, 0);
            this.icXyz.Compress(lastItem.Y.Int32, y, 1);
            this.icXyz.Compress(lastItem.Z.Int32, y, 2);

            this.lastItem.Offset = offset;
            this.lastItem.PacketSize = packetSize;
            this.lastItem.ReturnPoint.Int32 = returnPoint;
            this.lastItem.X.Int32 = x;
            this.lastItem.Y.Int32 = y;
            this.lastItem.Z.Int32 = z;

            return true;
        }
    }
}
