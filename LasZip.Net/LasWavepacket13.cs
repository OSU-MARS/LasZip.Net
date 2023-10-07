// laszip_common_v1.hpp
using System;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LasWavepacket13
    {
        public UInt64 Offset { get; set; }
        public UInt32 PacketSize { get; set; }
        public Interpretable32 ReturnPoint;
        public Interpretable32 X;
        public Interpretable32 Y;
        public Interpretable32 Z;

        //public LASwavepacket13 unpack(byte[] item)
        //{
        //	// unpack a LAS wavepacket out of raw memory
        //	LASwavepacket13 r;

        //	r.offset=makeU64(item);
        //	r.packet_size=makeU32(item+8);
        //	r.return_point.u32=makeU32(item+12);

        //	r.x.u32=makeU32(item+16);
        //	r.y.u32=makeU32(item+20);
        //	r.z.u32=makeU32(item+24);

        //	return r;
        //}

        //public void pack(byte[] item)
        //{
        //	// pack a LAS wavepacket into raw memory
        //	packU32((U32)(offset&0xFFFFFFFF), item);
        //	packU32((U32)(offset>>32), item+4);

        //	packU32(packet_size, item+8);
        //	packU32(return_point.u32, item+12);
        //	packU32(x.u32, item+16);
        //	packU32(y.u32, item+20);
        //	packU32(z.u32, item+24);
        //}

        //static UInt64 makeU64(byte[] item)
        //{
        //	U64 dw0=(U64)makeU32(item);
        //	U64 dw1=(U64)makeU32(item+4);

        //	return dw0|(dw1<<32);
        //}

        //static UInt32 makeU32(byte[] item)
        //{
        //	U32 b0=(U32)item[0];
        //	U32 b1=(U32)item[1];
        //	U32 b2=(U32)item[2];
        //	U32 b3=(U32)item[3];

        //	return b0|(b1<<8)|(b2<<16)|(b3<<24);
        //}

        //static void packU32(UInt32 v, byte[] item)
        //{
        //	item[0]=v&0xFF;
        //	item[1]=(v>>8)&0xFF;
        //	item[2]=(v>>16)&0xFF;
        //	item[3]=(v>>24)&0xFF;
        //}
    }
}
