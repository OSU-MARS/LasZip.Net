// laspoint.{hpp, cpp}
using System;

namespace LasZip
{
    public class LasPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public UInt16 Intensity { get; set; }
        public byte ReturnNumbersAndFlags { get; set; } // ReturnNumber bits 0:2, NumberOfReturnsOfGivenPulse bits 3:5, ScanDirectionFlag bit 6, EdgeOfFlightLine bit 7
        public byte ClassificationAndFlags { get; set; }
        public sbyte ScanAngleRank { get; set; }
        public byte UserData { get; set; }
        public UInt16 PointSourceID { get; set; }

        public double Gpstime { get; set; }
        public UInt16[] Rgb { get; set; }
        public byte[] Wavepacket { get; set; }

        // LAS 1.4 only
        public byte ExtendedFlags { get; set; }
        public byte ExtendedClassification { get; set; }
        public byte ExtendedReturns { get; set; }
        public Int16 ExtendedScanAngle { get; set; }

        public int NumExtraBytes { get; set; }
        public byte[]? ExtraBytes { get; set; }

        public LasPoint()
        {
            this.Rgb = new UInt16[4];
            this.Wavepacket = new byte[29];
        }

        //public byte return_number : 3 { get; set; }
        public byte ReturnNumber { get { return (byte)(ReturnNumbersAndFlags & 7); } set { ReturnNumbersAndFlags = (byte)((ReturnNumbersAndFlags & 0xF8) | (value & 7)); } }
        //public byte number_of_returns_of_given_pulse : 3;
        public byte NumberOfReturnsOfGivenPulse { get { return (byte)((ReturnNumbersAndFlags >> 3) & 7); } set { ReturnNumbersAndFlags = (byte)((ReturnNumbersAndFlags & 0xC7) | ((value & 7) << 3)); } }
        //public byte scan_direction_flag : 1;
        public byte ScanDirectionFlag { get { return (byte)((ReturnNumbersAndFlags >> 6) & 1); } set { ReturnNumbersAndFlags = (byte)((ReturnNumbersAndFlags & 0xBF) | ((value & 1) << 6)); } }
        //public byte edge_of_flight_line : 1;
        public byte EdgeOfFlightLine { get { return (byte)((ReturnNumbersAndFlags >> 7) & 1); } set { ReturnNumbersAndFlags = (byte)((ReturnNumbersAndFlags & 0x7F) | ((value & 1) << 7)); } }

        public byte SyntheticFlag { get { return (byte)((ClassificationAndFlags >> 5) & 1); } set { ClassificationAndFlags = (byte)((ClassificationAndFlags & 0xDF) | ((value & 1) << 5)); } }
        public byte KeypointFlag { get { return (byte)((ClassificationAndFlags >> 6) & 1); } set { ClassificationAndFlags = (byte)((ClassificationAndFlags & 0xBF) | ((value & 1) << 6)); } }
        public byte WithheldFlag { get { return (byte)((ClassificationAndFlags >> 7) & 1); } set { ClassificationAndFlags = (byte)((ClassificationAndFlags & 0x7F) | ((value & 1) << 7)); } }

        // LAS 1.4 only
        //public byte extended_point_type : 2;
        public byte ExtendedPointType { get { return (byte)(ExtendedFlags & 3); } set { ExtendedFlags = (byte)((ExtendedFlags & 0xFC) | (value & 3)); } }
        //public byte extended_scanner_channel : 2;
        public byte ExtendedScannerChannel { get { return (byte)((ExtendedFlags >> 2) & 3); } set { ExtendedFlags = (byte)((ExtendedFlags & 0xF3) | ((value & 3) << 2)); } }
        //public byte extended_classification_flags : 4;
        public byte ExtendedClassificationFlags { get { return (byte)((ExtendedFlags >> 4) & 0xF); } set { ExtendedFlags = (byte)((ExtendedFlags & 0xF) | ((value & 0xF) << 4)); } }
        //public byte extended_return_number : 4;
        public byte ExtendedReturnNumber { get { return (byte)(ExtendedReturns & 0xF); } set { ExtendedReturns = (byte)((ExtendedReturns & 0xF0) | (value & 0xF)); } }
        //public byte extended_number_of_returns_of_given_pulse : 4;
        public byte ExtendedNumberOfReturnsOfGivenPulse { get { return (byte)((ExtendedReturns >> 4) & 0xF); } set { ExtendedReturns = (byte)((ExtendedReturns & 0xF) | ((value & 0xF) << 4)); } }

        public bool IsSame(LasPoint other)
        {
            if (X != other.X) { return false; }
            if (Y != other.Y) { return false; }
            if (Z != other.Z) { return false; }

            if (Intensity != other.Intensity) { return false; }
            if (ReturnNumbersAndFlags != other.ReturnNumbersAndFlags) { return false; }
            if (ClassificationAndFlags != other.ClassificationAndFlags) { return false; }
            if (ScanAngleRank != other.ScanAngleRank) { return false; }
            if (UserData != other.UserData) { return false; }

            if (PointSourceID != other.PointSourceID) { return false; }
            if (Gpstime != other.Gpstime) { return false; }
            if (Rgb[0] != other.Rgb[0]) { return false; }
            if (Rgb[1] != other.Rgb[1]) { return false; }
            if (Rgb[2] != other.Rgb[2]) { return false; }
            if (Rgb[3] != other.Rgb[3]) { return false; }
            for (int i = 0; i < 29; i++)
            {
                if (Wavepacket[i] != other.Wavepacket[i]) { return false; }
            }

            if (ExtendedFlags != other.ExtendedFlags) { return false; }
            if (ExtendedClassification != other.ExtendedClassification) { return false; }
            if (ExtendedReturns != other.ExtendedReturns) { return false; }
            if (ExtendedScanAngle != other.ExtendedScanAngle) { return false; }

            if (NumExtraBytes != other.NumExtraBytes) { return false; }
            if ((this.ExtraBytes == null) || (other.ExtraBytes == null)) { return false; }
            for (int i = 0; i < NumExtraBytes; i++)
            {
                if (ExtraBytes[i] != other.ExtraBytes[i]) { return false; }
            }

            return true;
        }
    }
}
