// laszip_api.h
using System;
using System.Collections.Generic;

namespace LasZip
{
    public class LasHeader
    {
        public UInt16 FileSourceID { get; set; }
        public UInt16 GlobalEncoding { get; set; }
        public UInt32 ProjectIDGuidData1 { get; set; }
        public UInt16 ProjectIDGuidData2 { get; set; }
        public UInt16 ProjectIDGuidData3 { get; set; }
        public byte[] ProjectIDGuidData4 { get; private init; }
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }
        public byte[] SystemIdentifier { get; private init; }
        public byte[] GeneratingSoftware { get; private init; }
        public UInt16 FileCreationDay { get; set; }
        public UInt16 FileCreationYear { get; set; }
        public UInt16 HeaderSize { get; set; }
        public UInt32 OffsetToPointData { get; set; }
        public UInt32 NumberOfVariableLengthRecords { get; set; }
        public byte PointDataFormat { get; set; }
        public UInt16 PointDataRecordLength { get; set; }
        public UInt32 NumberOfPointRecords { get; set; } // TODO: rename to LegacyNumberOfPointRecords
        public UInt32[] NumberOfPointsByReturn { get; private init; } // TODO: rename to LegacyNumberOfPointsByReturn
        public double XScaleFactor { get; set; }
        public double YScaleFactor { get; set; }
        public double ZScaleFactor { get; set; }
        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public double ZOffset { get; set; }
        public double MaxX { get; set; }
        public double MinX { get; set; }
        public double MaxY { get; set; }
        public double MinY { get; set; }
        public double MaxZ { get; set; }
        public double MinZ { get; set; }

        // LAS 1.3 and higher only
        public UInt64 StartOfWaveformDataPacketRecord { get; set; }

        // LAS 1.4 and higher only
        public UInt64 StartOfFirstExtendedVariableLengthRecord { get; set; }
        public UInt32 NumberOfExtendedVariableLengthRecords { get; set; }
        public UInt64 ExtendedNumberOfPointRecords { get; set; }
        public UInt64[] ExtendedNumberOfPointsByReturn { get; private init; }

        // optional
        public UInt32 UserDataInHeaderSize { get; set; }
        public byte[]? UserDataInHeader { get; set; }

        // optional VLRs
        public List<LasVariableLengthRecord> Vlrs { get; private init; }

        // optional
        public UInt32 UserDataAfterHeaderSize { get; set; }
        public byte[]? UserDataAfterHeader { get; set; }

        public LasHeader()
        {
            this.ProjectIDGuidData4 = new byte[8];
            this.SystemIdentifier = new byte[32];
            this.GeneratingSoftware = new byte[32];
            this.NumberOfPointsByReturn = new UInt32[5];

            this.ExtendedNumberOfPointsByReturn = new UInt64[15];
            this.Vlrs = new();
        }
    }
}
