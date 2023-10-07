// lasreaditemcompressed_v3.cpp, lasreaditemcompressed_v4.cpp, laswriteitemcompressed_v3.cpp, laswriteitemcompressed_v4.cpp:
using System;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LasPoint14
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public UInt16 Intensity { get; set; }
        public byte Returns { get; set; }
        public byte Flags { get; set; }

        public byte Classification { get; set; }
        public byte UserData { get; set; }
        public Int16 ScanAngle { get; set; }
        public UInt16 PointSourceID { get; set; }
        public double Gpstime { get; set; }

        //public byte return_number : 4;
        public byte ReturnNumber { get { return (byte)(Returns & 0xF); } set { Returns = (byte)((Returns & 0xF0) | (value & 0xF)); } }
        //public byte number_of_returns_of_given_pulse : 4;
        public byte NumberOfReturnsOfGivenPulse { get { return (byte)((Returns >> 4) & 0xF); } set { Returns = (byte)((Returns & 0xF) | ((value & 0xF) << 4)); } }

        //public byte classification_flags : 4;
        public byte ClassificationFlags { get { return (byte)(Flags & 0xF); } set { Flags = (byte)((Flags & 0xF0) | (value & 0xF)); } }
        //public byte scanner_channel : 2;
        public byte ScannerChannel { get { return (byte)((Flags >> 4) & 3); } set { Flags = (byte)((Flags & 0xCF) | ((value & 3) << 4)); } }
        //public byte scan_direction_flag : 1;
        public byte ScanDirectionFlag { get { return (byte)((Flags >> 6) & 1); } set { Flags = (byte)((Flags & 0xBF) | ((value & 1) << 6)); } }
        //public byte edge_of_flight_line : 1;
        public byte EdgeOfFlightLine { get { return (byte)((Flags >> 7) & 1); } set { Flags = (byte)((Flags & 0x7F) | ((value & 1) << 7)); } }
    }
}
