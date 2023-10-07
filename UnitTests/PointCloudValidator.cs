using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;

namespace LasZip.UnitTests
{
    internal class PointCloudValidator
    {
        public string? GeneratingSoftware { get; init; }

        public PointCloud ReadAndValidate(string filePath, Version lasVersion, int pointDataFormat)
        {
            LasZipDll reader = new();
            bool isCompressedFile = true;
            reader.OpenReader(filePath, ref isCompressedFile);
            Assert.IsTrue(isCompressedFile == filePath.EndsWith(".laz", StringComparison.Ordinal));

            this.ValidateHeader(reader, isCompressedFile, lasVersion, pointDataFormat);
            PointCloud pointCloud = new(reader.Header);

            double[] coordinates = new double[3];
            UInt64 numberOfPointRecords = UInt64.Max(reader.Header.ExtendedNumberOfPointRecords, reader.Header.NumberOfPointRecords);
            bool pointFormatHasGpsTime = (pointDataFormat != 0) && (pointDataFormat != 2);
            UInt64 pointIndex = 0;
            while (reader.TryReadPoint())
            {
                Assert.IsTrue(reader.Point.Classification < 2);
                Assert.IsTrue(reader.Point.EdgeOfFlightLine == 0);
                Assert.IsTrue(reader.Point.ExtendedClassification == 0);
                Assert.IsTrue(reader.Point.ExtendedClassificationFlags == 0);
                Assert.IsTrue(reader.Point.ExtendedFlags == 0);
                Assert.IsTrue(reader.Point.ExtendedNumberOfReturnsOfGivenPulse == 0);
                Assert.IsTrue(reader.Point.ExtendedPointType == 0);
                Assert.IsTrue(reader.Point.ExtendedReturnNumber == 0);
                Assert.IsTrue(reader.Point.ExtendedReturns == 0);
                Assert.IsTrue(reader.Point.ExtendedScanAngle == 0);
                Assert.IsTrue(reader.Point.ExtendedScannerChannel == 0);
                Assert.IsTrue(reader.Point.ExtraBytes == null);
                Assert.IsTrue(reader.Point.Flags == 0);
                if (pointFormatHasGpsTime)
                {
                    Assert.IsTrue(reader.Point.Gpstime > 1688896198.0); // July 2023
                }
                else
                {
                    Assert.IsTrue(reader.Point.Gpstime == 0.0);
                }
                Assert.IsTrue(reader.Point.Intensity <= 29812); // no generally useful validation but full range happens not to be used in data
                Assert.IsTrue(reader.Point.NumberOfReturnsOfGivenPulse == 0);
                Assert.IsTrue(reader.Point.NumExtraBytes == 0);
                Assert.IsTrue(reader.Point.PointSourceID == 0);
                Assert.IsTrue(reader.Point.ReturnNumber == 0);
                Assert.IsTrue(reader.Point.Rgb[0] == 0);
                Assert.IsTrue(reader.Point.Rgb[1] == 0);
                Assert.IsTrue(reader.Point.Rgb[2] == 0);
                Assert.IsTrue(reader.Point.ScanAngleRank == 0);
                Assert.IsTrue(reader.Point.ScanDirectionFlag == 0);
                Assert.IsTrue(reader.Point.UserData == 0);
                Assert.IsTrue(reader.Point.Wavepacket.Length == 29);

                Assert.IsTrue(reader.GetPointCoordinates(coordinates) == 0);
                Assert.IsTrue((reader.Header.MinX <= coordinates[0]) && (coordinates[0] <= reader.Header.MaxX));
                Assert.IsTrue((reader.Header.MinY <= coordinates[1]) && (coordinates[1] <= reader.Header.MaxY));
                Assert.IsTrue((reader.Header.MinZ <= coordinates[2]) && (coordinates[2] <= reader.Header.MaxZ));

                pointCloud.Gpstime[pointIndex] = reader.Point.Gpstime;
                pointCloud.Intensity[pointIndex] = reader.Point.Intensity;
                pointCloud.X[pointIndex] = reader.Point.X;
                pointCloud.Y[pointIndex] = reader.Point.Y;
                pointCloud.Z[pointIndex] = reader.Point.Z;
                ++pointIndex;
                if (isCompressedFile && (pointIndex == numberOfPointRecords))
                {
                    break; // work around .laz file read bug
                }
            }

            Assert.IsTrue(pointIndex == numberOfPointRecords);
            reader.GetPointIndex(out long readerPointIndex);
            Assert.IsTrue(readerPointIndex == reader.Header.NumberOfPointRecords);
            Assert.IsTrue(reader.GetLastWarning() == null);

            return pointCloud;
        }

        private void ValidateHeader(LasZipDll reader, bool isCompressed, Version lasVersion, int pointDataFormat)
        {
            UInt32 expectedNumberOfPointRecords = 207617;
            UInt64 expectedExtendedNumberOfPointRecords = 0;
            if (pointDataFormat >= 6)
            {
                expectedNumberOfPointRecords = isCompressed ? 0U : 207617U;
                expectedExtendedNumberOfPointRecords = isCompressed ? 207617UL : reader.Header.NumberOfPointRecords;
            }
            Assert.IsTrue(reader.Header.ExtendedNumberOfPointRecords == expectedExtendedNumberOfPointRecords);
            for (int returnIndex = 0; returnIndex < reader.Header.ExtendedNumberOfPointsByReturn.Length; ++returnIndex)
            {
                Assert.IsTrue(reader.Header.ExtendedNumberOfPointsByReturn[returnIndex] == 0);
            }
            Assert.IsTrue(reader.Header.FileCreationDay == 282);
            Assert.IsTrue(reader.Header.FileCreationYear == 2023);
            Assert.IsTrue(reader.Header.FileSourceID == 0);

            string generatingSoftware = Encoding.ASCII.GetString(reader.Header.GeneratingSoftware);
            Assert.IsTrue(String.Equals(generatingSoftware, this.GeneratingSoftware, StringComparison.Ordinal));

            Assert.IsTrue(reader.Header.GlobalEncoding == (isCompressed ? 0 : 16));
            Assert.IsTrue(reader.Header.HeaderSize == (lasVersion.Minor < 4 ? 227 : 375));
            Assert.IsTrue(reader.Header.MaxX == 608760.87655364827);
            Assert.IsTrue(reader.Header.MaxY == 4927011.2347104261);
            Assert.IsTrue(reader.Header.MaxZ == 928.63061226561206);
            Assert.IsTrue(reader.Header.MinX == 608757.26795364823);
            Assert.IsTrue(reader.Header.MinY == 4927007.0106104258);
            Assert.IsTrue(reader.Header.MinZ == 920.520912265612);
            Assert.IsTrue(reader.Header.NumberOfExtendedVariableLengthRecords == 0);
            Assert.IsTrue(reader.Header.NumberOfPointRecords == expectedNumberOfPointRecords);
            Assert.IsTrue(reader.Header.NumberOfPointsByReturn[0] == 0);
            Assert.IsTrue(reader.Header.NumberOfPointsByReturn[1] == 0);
            Assert.IsTrue(reader.Header.NumberOfPointsByReturn[2] == 0);
            Assert.IsTrue(reader.Header.NumberOfPointsByReturn[3] == 0);
            Assert.IsTrue(reader.Header.NumberOfPointsByReturn[4] == 0);
            Assert.IsTrue(reader.Header.NumberOfVariableLengthRecords == 1);

            UInt32 expectedOffsetToPointData;
            if (isCompressed)
            {
                expectedOffsetToPointData = lasVersion.Minor >= 4 ? 453U : 305U;
            }
            else
            {
                expectedOffsetToPointData = lasVersion.Minor switch
                {
                    1 => 983U,
                    2 => 5283U,
                    4 => 1131U,
                    _ => throw new NotSupportedException("Unhandled LAS version " + lasVersion.Major + "." + lasVersion.Minor + ".")
                };
            }
            Assert.IsTrue(reader.Header.OffsetToPointData == expectedOffsetToPointData);
            Assert.IsTrue(reader.Header.PointDataFormat == pointDataFormat);
            int expectedPointDataRecordLength = pointDataFormat switch
            {
                0 => 20,
                1 => 28,
                6 => 30,
                _ => throw new NotSupportedException("Unhandled point data format " + pointDataFormat + ".")
            };
            Assert.IsTrue(reader.Header.PointDataRecordLength == expectedPointDataRecordLength);

            Assert.IsTrue(reader.Header.ProjectIDGuidData1 == 0);
            Assert.IsTrue(reader.Header.ProjectIDGuidData2 == 0);
            Assert.IsTrue(reader.Header.ProjectIDGuidData3 == 0);
            Assert.IsTrue(reader.Header.ProjectIDGuidData4.Length == 8);
            for (int index = 0; index < reader.Header.ProjectIDGuidData4.Length; ++index)
            {
                Assert.IsTrue(reader.Header.ProjectIDGuidData4[index] == 0);
            }

            UInt64 expectedStartOfFirstExtendedVariableLengthRecord = 0UL;
            if ((lasVersion.Minor >= 4) && (isCompressed == false))
            {
                expectedStartOfFirstExtendedVariableLengthRecord = 6229641UL;
            }
            Assert.IsTrue(reader.Header.StartOfFirstExtendedVariableLengthRecord == expectedStartOfFirstExtendedVariableLengthRecord);
            Assert.IsTrue(reader.Header.StartOfWaveformDataPacketRecord == 0);
            if (isCompressed)
            {
                Assert.IsTrue(reader.Header.UserDataAfterHeader == null);
                Assert.IsTrue(reader.Header.UserDataAfterHeaderSize == 0);
            }
            else
            {
                Assert.IsTrue((reader.Header.UserDataAfterHeader != null) && (reader.Header.UserDataAfterHeader.Length == reader.Header.UserDataAfterHeaderSize));
                Assert.IsTrue(reader.Header.UserDataAfterHeaderSize == (lasVersion.Minor == 2 ? 2U : 654U));
            }
            Assert.IsTrue(reader.Header.UserDataInHeader == null);
            Assert.IsTrue(reader.Header.UserDataInHeaderSize == 0);
            Assert.IsTrue(reader.Header.VersionMajor == lasVersion.Major);
            Assert.IsTrue(reader.Header.VersionMinor == lasVersion.Minor);

            Assert.IsTrue(reader.Header.Vlrs.Count == reader.Header.NumberOfVariableLengthRecords);
            LasVariableLengthRecord vlr = reader.Header.Vlrs[0];
            string description = Encoding.ASCII.GetString(vlr.Description);
            bool expectGeoTiffVlr = false;
            if (isCompressed)
            {
                Assert.IsTrue(String.Equals(description, "by LAStools of rapidlasso GmbH\0\0", StringComparison.Ordinal));
                expectGeoTiffVlr = true;
            }
            else
            {
                if (lasVersion.Minor == 2)
                {
                    Assert.IsTrue(String.Equals(description, "OGC Coordinate System WKT\0\0\0\0\0\0\0", StringComparison.Ordinal));
                }
                else
                {
                    Assert.IsTrue(String.Equals(description, "Geotiff Projection Keys\0\0\0\0\0\0\0\0\0", StringComparison.Ordinal));
                    expectGeoTiffVlr = true;
                }
            }
            string userID = Encoding.ASCII.GetString(vlr.UserID);
            Assert.IsTrue(userID == "LASF_Projection\0");
            if (expectGeoTiffVlr)
            {
                Assert.IsTrue(vlr.RecordID == 34735); // GeoKeyDirectoryTag Record
                Assert.IsTrue(vlr.RecordLengthAfterHeader == (isCompressed ? 24 : 48));
                Assert.IsTrue(vlr.Reserved == (isCompressed ? 0 : 43707));

                Assert.IsTrue((vlr.Data != null) && (vlr.Data.Length == vlr.RecordLengthAfterHeader));
                LasGeoKeys geoKey = new(vlr.Data);
                Assert.IsTrue(geoKey.KeyDirectoryVersion == 1);
                Assert.IsTrue(geoKey.KeyRevision == 1);
                Assert.IsTrue(geoKey.MinorRevision == 0);
                Assert.IsTrue(geoKey.NumberOfKeys == (isCompressed ? 2 : 3)); // should probably be 2 in .las files but QTM indicates 3
                Assert.IsTrue((geoKey.Keys != null) && (geoKey.Keys.Length == geoKey.NumberOfKeys));
                Assert.IsTrue(geoKey.Keys[0].KeyID == 3072); // ProjectedCSTypeGeoKey
                Assert.IsTrue(geoKey.Keys[0].TiffTagLocation == 0);
                Assert.IsTrue(geoKey.Keys[0].Count == 1);
                Assert.IsTrue(geoKey.Keys[0].ValueOffset == 32610);
                Assert.IsTrue(geoKey.Keys[1].KeyID == 4099); // VerticalUnitsGeoKey
                Assert.IsTrue(geoKey.Keys[1].TiffTagLocation == 0);
                Assert.IsTrue(geoKey.Keys[1].Count == 1);
                Assert.IsTrue(geoKey.Keys[1].ValueOffset == 32610);
                // third key is all zero if present, so ignore for now
            }
            else
            {
                Assert.IsTrue(vlr.RecordID == 2112); // OGC Coordinate System WKT Record
                Assert.IsTrue(vlr.RecordLengthAfterHeader == 5000);
                Assert.IsTrue(vlr.Reserved == 43707);

                Assert.IsTrue((vlr.Data != null) && (vlr.Data.Length == vlr.RecordLengthAfterHeader));
                string wkt = Encoding.UTF8.GetString(vlr.Data);
                const string expectedWkt = "PROJCS[\"WGS 84 / UTM zone 10N\",GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",-123],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"metre\",1,AUTHORITY[\"EPSG\",\"9001\"]],AXIS[\"Easting\",EAST],AXIS[\"Northing\",NORTH],AUTHORITY[\"EPSG\",\"32610\"]]";
                Assert.IsTrue(String.Equals(wkt[..expectedWkt.Length], expectedWkt, StringComparison.Ordinal)); // skip comparison of 4.4 kB of trailing \0s
            }

            Assert.IsTrue(reader.Header.XOffset == 608571.86565364827);
            Assert.IsTrue(reader.Header.XScaleFactor == 0.0001);
            Assert.IsTrue(reader.Header.YOffset == 4926891.3199104257);
            Assert.IsTrue(reader.Header.YScaleFactor == 0.0001);
            Assert.IsTrue(reader.Header.ZOffset == 912.835012265612);
            Assert.IsTrue(reader.Header.ZScaleFactor == 0.0001);

            reader.GetNumberOfPoints(out long numberOfPoints);
            reader.GetPointIndex(out long pointCount);
            Assert.IsTrue(numberOfPoints == expectedNumberOfPointRecords);
            Assert.IsTrue(pointCount == 0);
        }
    }
}
