// laszip_api.h, laszip_decompress_selective_v3.hpp
using System;

namespace LasZip
{
    [Flags]
    public enum LasZipDecompressSelective : UInt32
    {
        ChannelReturnsXY = 0x00000000,
        Z = 0x00000001,
        Classification = 0x00000002,
        Flags = 0x00000004,
        Intensity = 0x00000008,
        ScanAngle = 0x00000010,
        UserData = 0x00000020,
        PointSource = 0x00000040,
        Gpstime = 0x00000080,
        Rgb = 0x00000100,
        Nir = 0x00000200,
        Wavepacket = 0x00000400,
        Byte0 = 0x00010000,
        Byte1 = 0x00020000,
        Byte2 = 0x00040000,
        Byte3 = 0x00080000,
        Byte4 = 0x00100000,
        Byte5 = 0x00200000,
        Byte6 = 0x00400000,
        Byte7 = 0x00800000,
        ExtraBytes = 0xffff0000,
        All = 0xffffffff
    }
}
