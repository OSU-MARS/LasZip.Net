// lasreaditemcompressed_v1.cpp, lasreaditemcompressed_v2.cpp, laswriteitemcompressed_v1.cpp, laswriteitemcompressed_v2.cpp
using System;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LasPoint10
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public UInt16 Intensity { get; set; }

        // all these bits combine to flags
        //public byte return_number : 3 { get; set; }
        //public byte number_of_returns_of_given_pulse : 3 { get; set; }
        //public byte scan_direction_flag : 1 { get; set; }
        //public byte edge_of_flight_line : 1 { get; set; }
        public byte ReturnNumbersAndFlags { get; set; }

        public byte Classification { get; set; }
        public sbyte ScanAngleRank { get; set; }
        public byte UserData { get; set; }
        public UInt16 PointSourceID { get; set; }
    }
}
