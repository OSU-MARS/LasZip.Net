using LasZip.UnitTests.Extensions;
using System;

namespace LasZip.UnitTests
{
    internal class PointCloud
    {
        public LasHeader Header { get; set; }
        public double[] Gpstime { get; private init; }
        public UInt16[] Intensity { get; private init; }
        public int[] X { get; private init; }
        public int[] Y { get; private init; }
        public int[] Z { get; private init; }

        public PointCloud(LasHeader header)
        {
            this.Header = header;
            this.Gpstime = new double[header.NumberOfPointRecords];
            this.Intensity = new UInt16[header.NumberOfPointRecords];
            this.X = new int[header.NumberOfPointRecords];
            this.Y = new int[header.NumberOfPointRecords];
            this.Z = new int[header.NumberOfPointRecords];
        }

        public int Count
        {
            get { return this.X.Length; }
        }

        public static bool HeaderCoreEqual(PointCloud pointCloud, PointCloud other)
        {
            if (Object.ReferenceEquals(pointCloud, other) || Object.ReferenceEquals(pointCloud.Header, other.Header))
            {
                return true;
            }

            LasHeader header = pointCloud.Header;
            LasHeader otherHeader = other.Header;

            UInt64 headerPoints = UInt64.Max(header.ExtendedNumberOfPointRecords, header.NumberOfPointRecords);
            UInt64 otherPoints = UInt64.Max(otherHeader.ExtendedNumberOfPointRecords, otherHeader.NumberOfPointRecords);
            if (headerPoints != otherPoints)
            {
                return false;
            }

            if (header.XScaleFactor != otherHeader.XScaleFactor)
            {
                return false;
            }
            if (header.YScaleFactor != otherHeader.YScaleFactor)
            {
                return false;
            }
            if (header.ZScaleFactor != otherHeader.ZScaleFactor)
            {
                return false;
            }
            if (header.XOffset != otherHeader.XOffset)
            {
                return false;
            }
            if (header.YOffset != otherHeader.YOffset)
            {
                return false;
            }
            if (header.ZOffset != otherHeader.ZOffset)
            {
                return false;
            }
            if (header.MaxX != otherHeader.MaxX)
            {
                return false;
            }
            if (header.MinX != otherHeader.MinX)
            {
                return false;
            }
            if (header.MaxY != otherHeader.MaxY)
            {
                return false;
            }
            if (header.MinY != otherHeader.MinY)
            {
                return false;
            }
            if (header.MaxZ != otherHeader.MaxZ)
            {
                return false;
            }
            if (header.MinZ != otherHeader.MinZ)
            {
                return false;
            }

            // TODO: header.ExtendedNumberOfPointsByReturn
            // TODO: header.NumberOfPointsByReturn
            // TODO: support checking of header.Vlrs across different CRS indications

            // non-core properties not checked
            // header.FileSourceID
            // header.GlobalEncoding
            // header.ProjectIDGuidData1
            // header.ProjectIDGuidData2
            // header.ProjectIDGuidData3
            // header.ProjectIDGuidData4
            // header.VersionMajor
            // header.VersionMinor
            // header.FileCreationDay
            // header.FileCreationYear
            // header.OffsetToPointData
            // header.NumberOfVariableLengthRecords
            // header.PointDataFormat
            // header.PointDataRecordLength
            // header.SystemIdentifier
            // header.GeneratingSoftware

            // LAS 1.3 and higher only
            // header.StartOfWaveformDataPacketRecord

            // LAS 1.4 and higher only
            // header.StartOfFirstExtendedVariableLengthRecord != otherHeader.StartOfFirstExtendedVariableLengthRecord)
            // header.NumberOfExtendedVariableLengthRecords != otherHeader.NumberOfExtendedVariableLengthRecords)
            // header.ExtendedNumberOfPointRecords != otherHeader.ExtendedNumberOfPointRecords)

            // optional
            // header.UserDataInHeaderSize
            // header.UserDataInHeader
            // header.UserDataAfterHeaderSize

            return true;
        }

        public static bool HeaderCorePointGpstimeIntensityXyzEqual(PointCloud pointCloud, PointCloud other)
        {
            if (PointCloud.HeaderCorePointIntensityXyzEqual(pointCloud, other) == false)
            {
                return false;
            }
            if (ArrayExtensions.Equals(pointCloud.Gpstime, other.Gpstime) == false)
            {
                return false;
            }

            return true;
        }
        
        public static bool HeaderCorePointIntensityXyzEqual(PointCloud pointCloud, PointCloud other)
        {
            if (PointCloud.HeaderCoreAndPointXyzEqual(pointCloud, other) == false)
            {
                return false;
            }
            if (ArrayExtensions.Equals(pointCloud.Intensity, other.Intensity) == false)
            {
                return false;
            }

            return true;
        }

        public static bool HeaderCoreAndPointXyzEqual(PointCloud pointCloud, PointCloud other)
        {
            if (Object.ReferenceEquals(pointCloud, other))
            {
                return true;
            }
            if (PointCloud.HeaderCoreEqual(pointCloud, other) == false) 
            {
                return false;
            }

            if (pointCloud.Count != other.Count)
            {
                return false;
            }

            if (ArrayExtensions.Equals(pointCloud.X, other.X) == false)
            {
                return false;
            }
            if (ArrayExtensions.Equals(pointCloud.Y, other.Y) == false)
            {
                return false;
            }
            if (ArrayExtensions.Equals(pointCloud.Z, other.Z) == false)
            {
                return false;
            }

            return true;
        }
    }
}
