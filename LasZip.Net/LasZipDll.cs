//===============================================================================
//
//  FILE:  laszip_dll.cs
//
//  CONTENTS:
//
//    C# port of a simple DLL interface to LASzip.
//
//  PROGRAMMERS:
//
//    martin.isenburg@rapidlasso.com  -  http://rapidlasso.com
//
//  COPYRIGHT:
//
//    (c) 2005-2012, martin isenburg, rapidlasso - tools to catch reality
//    (c) of the C# port 2014-2017 by Shinta <shintadono@googlemail.com>
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
using System.IO;
using System.Text;

namespace LasZip
{
    // BUGBUG: error and warning strings aren't appended to, so only the last error or warning encountered will be reported
    public class LasZipDll
    {
        private long currentPointIndex;
        // TODO: remove along with GetNumberOfPoints() since duplicate of this.Header.NumberOfPointRecords
        // TODO: LasHeader.GetNumberOfPoints() should probably abstract details of NumberOfPointRecords versus ExtendedNumberOfPointRecords
        private long nPoints;

        private Stream? streamIn;
        private bool leaveStreamInOpen;
        private LasReadPoint? reader;

        private Stream? streamOut;
        private bool leaveStreamOutOpen;
        private LasWritePoint? writer;

        private string? warning;

        public LasHeader Header;
        public LasPoint Point;

        public LasZipDll()
        {
            this.Header = new();
            this.Point = new();
        }

        public string? GetLastWarning()
        {
            return warning;
        }

        public void Clear()
        {
            if (reader != null)
            {
                throw new InvalidOperationException("cannot clean while reader is open.");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot clean while writer is open.");
            }

            // zero everything
            Header.FileSourceID = 0;
            Header.GlobalEncoding = 0;
            Header.ProjectIDGuidData1 = 0;
            Header.ProjectIDGuidData2 = 0;
            Header.ProjectIDGuidData3 = 0;
            Array.Clear(Header.ProjectIDGuidData4, 0, Header.ProjectIDGuidData4.Length);
            Header.VersionMajor = 0;
            Header.VersionMinor = 0;
            Array.Clear(Header.SystemIdentifier, 0, Header.SystemIdentifier.Length);
            Array.Clear(Header.GeneratingSoftware, 0, Header.GeneratingSoftware.Length);
            Header.FileCreationDay = 0;
            Header.FileCreationYear = 0;
            Header.HeaderSize = 0;
            Header.OffsetToPointData = 0;
            Header.NumberOfVariableLengthRecords = 0;
            Header.PointDataFormat = 0;
            Header.PointDataRecordLength = 0;
            Header.NumberOfPointRecords = 0;
            Array.Clear(Header.NumberOfPointsByReturn, 0, Header.NumberOfPointsByReturn.Length);
            Header.XScaleFactor = 0;
            Header.YScaleFactor = 0;
            Header.ZScaleFactor = 0;
            Header.XOffset = 0;
            Header.YOffset = 0;
            Header.ZOffset = 0;
            Header.MaxX = 0;
            Header.MinX = 0;
            Header.MaxY = 0;
            Header.MinY = 0;
            Header.MaxZ = 0;
            Header.MinZ = 0;
            Header.StartOfWaveformDataPacketRecord = 0;
            Header.StartOfFirstExtendedVariableLengthRecord = 0;
            Header.NumberOfExtendedVariableLengthRecords = 0;
            Header.ExtendedNumberOfPointRecords = 0;
            Array.Clear(Header.ExtendedNumberOfPointsByReturn, 0, Header.ExtendedNumberOfPointsByReturn.Length);
            Header.UserDataInHeaderSize = 0;
            Header.UserDataInHeader = null;
            Header.Vlrs.Clear();
            Header.UserDataAfterHeaderSize = 0;
            Header.UserDataAfterHeader = null;

            currentPointIndex = 0;
            nPoints = 0;

            Point.X = 0;
            Point.Y = 0;
            Point.Z = 0;
            Point.Intensity = 0;
            Point.ReturnNumber = 0;// : 3;
            Point.NumberOfReturnsOfGivenPulse = 0;// : 3;
            Point.ScanDirectionFlag = 0;// : 1;
            Point.EdgeOfFlightLine = 0;// : 1;
            Point.Classification = 0;
            Point.ScanAngleRank = 0;
            Point.UserData = 0;
            Point.PointSourceID = 0;
            Point.Gpstime = 0;
            Point.Rgb = new UInt16[4];
            Point.Wavepacket = new byte[29];
            Point.ExtendedPointType = 0;// : 2;
            Point.ExtendedScannerChannel = 0;// : 2;
            Point.ExtendedClassificationFlags = 0;// : 4;
            Point.ExtendedClassification = 0;
            Point.ExtendedReturnNumber = 0;// : 4;
            Point.ExtendedNumberOfReturnsOfGivenPulse = 0;// : 4;
            Point.ExtendedScanAngle = 0;
            Point.NumExtraBytes = 0;
            Point.ExtraBytes = null;

            streamIn = null;
            reader = null;

            streamOut = null;
            writer = null;

            warning = null;

            // create default header
            byte[] generatingSoftware = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
            Array.Copy(generatingSoftware, Header.GeneratingSoftware, Math.Min(generatingSoftware.Length, 32));
            Header.VersionMajor = 1;
            Header.VersionMinor = 2;
            Header.HeaderSize = 227;
            Header.OffsetToPointData = 227;
            Header.PointDataFormat = 1;
            Header.PointDataRecordLength = 28;
            Header.XScaleFactor = 0.01;
            Header.YScaleFactor = 0.01;
            Header.ZScaleFactor = 0.01;
        }

        public void GetPointIndex(out long index)
        {
            if ((reader == null) && (writer == null))
            {
                throw new InvalidOperationException("getting count before reader or writer was opened");
            }

            index = currentPointIndex;
        }

        public void GetNumberOfPoints(out long pointCount)
        {
            if ((reader == null) && (writer == null))
            {
                throw new InvalidOperationException("getting count before reader or writer was opened");
            }

            pointCount = this.nPoints;
        }

        public int SetHeader(LasHeader header)
        {
            if (header == null)
            {
                throw new InvalidOperationException("laszip_header_struct pointer is null");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot set header after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot set header after writer was opened");
            }

            this.Header.FileSourceID = header.FileSourceID;
            this.Header.GlobalEncoding = header.GlobalEncoding;
            this.Header.ProjectIDGuidData1 = header.ProjectIDGuidData1;
            this.Header.ProjectIDGuidData2 = header.ProjectIDGuidData2;
            this.Header.ProjectIDGuidData3 = header.ProjectIDGuidData3;
            Array.Copy(header.ProjectIDGuidData4, this.Header.ProjectIDGuidData4, 8);
            this.Header.VersionMajor = header.VersionMajor;
            this.Header.VersionMinor = header.VersionMinor;
            Array.Copy(header.SystemIdentifier, this.Header.SystemIdentifier, 32);
            Array.Copy(header.GeneratingSoftware, this.Header.GeneratingSoftware, 32);
            this.Header.FileCreationDay = header.FileCreationDay;
            this.Header.FileCreationYear = header.FileCreationYear;
            this.Header.HeaderSize = header.HeaderSize;
            this.Header.OffsetToPointData = header.OffsetToPointData;
            this.Header.NumberOfVariableLengthRecords = header.NumberOfVariableLengthRecords;
            this.Header.PointDataFormat = header.PointDataFormat;
            this.Header.PointDataRecordLength = header.PointDataRecordLength;
            this.Header.NumberOfPointRecords = header.NumberOfPointRecords;
            for (int i = 0; i < 5; i++) this.Header.NumberOfPointsByReturn[i] = header.NumberOfPointsByReturn[i];
            this.Header.XScaleFactor = header.XScaleFactor;
            this.Header.YScaleFactor = header.YScaleFactor;
            this.Header.ZScaleFactor = header.ZScaleFactor;
            this.Header.XOffset = header.XOffset;
            this.Header.YOffset = header.YOffset;
            this.Header.ZOffset = header.ZOffset;
            this.Header.MaxX = header.MaxX;
            this.Header.MinX = header.MinX;
            this.Header.MaxY = header.MaxY;
            this.Header.MinY = header.MinY;
            this.Header.MaxZ = header.MaxZ;
            this.Header.MinZ = header.MinZ;

            if (this.Header.VersionMinor >= 3)
            {
                this.Header.StartOfWaveformDataPacketRecord = header.StartOfFirstExtendedVariableLengthRecord;
            }

            if (this.Header.VersionMinor >= 4)
            {
                this.Header.StartOfFirstExtendedVariableLengthRecord = header.StartOfFirstExtendedVariableLengthRecord;
                this.Header.NumberOfExtendedVariableLengthRecords = header.NumberOfExtendedVariableLengthRecords;
                this.Header.ExtendedNumberOfPointRecords = header.ExtendedNumberOfPointRecords;
                for (int i = 0; i < 15; i++) this.Header.ExtendedNumberOfPointsByReturn[i] = header.ExtendedNumberOfPointsByReturn[i];
            }

            this.Header.UserDataInHeaderSize = header.UserDataInHeaderSize;
            this.Header.UserDataInHeader = null;

            if (header.UserDataInHeaderSize != 0)
            {
                this.Header.UserDataInHeader = new byte[header.UserDataInHeaderSize];
                Array.Copy(header.UserDataInHeader, this.Header.UserDataInHeader, header.UserDataInHeaderSize);
            }

            if (header.NumberOfVariableLengthRecords != 0)
            {
                for (int i = 0; i < header.NumberOfVariableLengthRecords; i++)
                {
                    this.Header.Vlrs.Add(new LasVariableLengthRecord());
                    this.Header.Vlrs[i].Reserved = header.Vlrs[i].Reserved;
                    Array.Copy(header.Vlrs[i].UserID, this.Header.Vlrs[i].UserID, 16);
                    this.Header.Vlrs[i].RecordID = header.Vlrs[i].RecordID;
                    this.Header.Vlrs[i].RecordLengthAfterHeader = header.Vlrs[i].RecordLengthAfterHeader;
                    Array.Copy(header.Vlrs[i].Description, this.Header.Vlrs[i].Description, 32);
                    if (header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        this.Header.Vlrs[i].Data = new byte[header.Vlrs[i].RecordLengthAfterHeader];
                        Array.Copy(header.Vlrs[i].Data, this.Header.Vlrs[i].Data, header.Vlrs[i].RecordLengthAfterHeader);
                    }
                    else
                    {
                        this.Header.Vlrs[i].Data = null;
                    }
                }
            }

            this.Header.UserDataAfterHeaderSize = header.UserDataAfterHeaderSize;
            this.Header.UserDataAfterHeader = null;
            if (header.UserDataAfterHeaderSize != 0)
            {
                this.Header.UserDataAfterHeader = new byte[header.UserDataAfterHeaderSize];
                Array.Copy(header.UserDataAfterHeader, this.Header.UserDataAfterHeader, header.UserDataAfterHeaderSize);
            }

            return 0;
        }

        private int CheckForIntegerOverflow()
        {
            // quantize and dequantize the bounding box with current scale_factor and offset
            int quant_min_x = MyDefs.QuantizeInt32((Header.MinX - Header.XOffset) / Header.XScaleFactor);
            int quant_max_x = MyDefs.QuantizeInt32((Header.MaxX - Header.XOffset) / Header.XScaleFactor);
            int quant_min_y = MyDefs.QuantizeInt32((Header.MinY - Header.YOffset) / Header.YScaleFactor);
            int quant_max_y = MyDefs.QuantizeInt32((Header.MaxY - Header.YOffset) / Header.YScaleFactor);
            int quant_min_z = MyDefs.QuantizeInt32((Header.MinZ - Header.ZOffset) / Header.ZScaleFactor);
            int quant_max_z = MyDefs.QuantizeInt32((Header.MaxZ - Header.ZOffset) / Header.ZScaleFactor);

            double dequant_min_x = Header.XScaleFactor * quant_min_x + Header.XOffset;
            double dequant_max_x = Header.XScaleFactor * quant_max_x + Header.XOffset;
            double dequant_min_y = Header.YScaleFactor * quant_min_y + Header.YOffset;
            double dequant_max_y = Header.YScaleFactor * quant_max_y + Header.YOffset;
            double dequant_min_z = Header.ZScaleFactor * quant_min_z + Header.ZOffset;
            double dequant_max_z = Header.ZScaleFactor * quant_max_z + Header.ZOffset;

            // make sure that there is not sign flip (a 32-bit integer overflow) for the bounding box
            if ((Header.MinX > 0) != (dequant_min_x > 0))
            {
                throw new InvalidOperationException(String.Format("quantization sign flip for min_x from {0} to {1}. set scale factor for x coarser than {2}", Header.MinX, dequant_min_x, Header.XScaleFactor));
            }
            if ((Header.MaxX > 0) != (dequant_max_x > 0))
            {
                throw new InvalidOperationException(String.Format("quantization sign flip for max_x from {0} to {1}. set scale factor for x coarser than {2}", Header.MaxX, dequant_max_x, Header.XScaleFactor));
            }
            if ((Header.MinY > 0) != (dequant_min_y > 0))
            {
                throw new InvalidOperationException(String.Format("quantization sign flip for min_y from {0} to {1}. set scale factor for y coarser than {2}", Header.MinY, dequant_min_y, Header.YScaleFactor));
            }
            if ((Header.MaxY > 0) != (dequant_max_y > 0))
            {
                throw new InvalidOperationException(String.Format("quantization sign flip for max_y from {0} to {1}. set scale factor for y coarser than {2}", Header.MaxY, dequant_max_y, Header.YScaleFactor));
            }
            if ((Header.MinZ > 0) != (dequant_min_z > 0))
            {
                throw new InvalidOperationException(String.Format("quantization sign flip for min_z from {0} to {1}. set scale factor for z coarser than {2}", Header.MinZ, dequant_min_z, Header.ZScaleFactor));
            }
            if ((Header.MaxZ > 0) != (dequant_max_z > 0))
            {
                throw new InvalidOperationException(String.Format("quantization sign flip for max_z from {0} to {1}. set scale factor for z coarser than {2}", Header.MaxZ, dequant_max_z, Header.ZScaleFactor));
            }

            return 0;
        }

        private int AutoOffset()
        {
            if (reader != null)
            {
                throw new InvalidOperationException("cannot auto offset after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot auto offset after writer was opened");
            }

            // check scale factor
            double x_scale_factor = Header.XScaleFactor;
            double y_scale_factor = Header.YScaleFactor;
            double z_scale_factor = Header.ZScaleFactor;

            if ((x_scale_factor <= 0.0) || Double.IsInfinity(x_scale_factor))
            {
                throw new InvalidOperationException(String.Format("Invalid x scale_factor {0} in header.", Header.XScaleFactor));
            }

            if ((y_scale_factor <= 0.0) || Double.IsInfinity(y_scale_factor))
            {
                throw new InvalidOperationException(String.Format("Invalid y scale_factor {0} in header.", Header.YScaleFactor));
            }

            if ((z_scale_factor <= 0.0) || Double.IsInfinity(z_scale_factor))
            {
                throw new InvalidOperationException(String.Format("Invalid z scale_factor {0} in header.", Header.ZScaleFactor));
            }

            double center_bb_x = (Header.MinX + Header.MaxX) / 2;
            double center_bb_y = (Header.MinY + Header.MaxY) / 2;
            double center_bb_z = (Header.MinZ + Header.MaxZ) / 2;

            if (double.IsInfinity(center_bb_x))
            {
                throw new InvalidOperationException(String.Format("invalid x coordinate at center of bounding box (min: {0} max: {1})", Header.MinX, Header.MaxX));
            }

            if (double.IsInfinity(center_bb_y))
            {
                throw new InvalidOperationException(String.Format("invalid y coordinate at center of bounding box (min: {0} max: {1})", Header.MinY, Header.MaxY));
            }

            if (double.IsInfinity(center_bb_z))
            {
                throw new InvalidOperationException(String.Format("invalid z coordinate at center of bounding box (min: {0} max: {1})", Header.MinZ, Header.MaxZ));
            }

            double x_offset = Header.XOffset;
            double y_offset = Header.YOffset;
            double z_offset = Header.ZOffset;

            Header.XOffset = (MyDefs.FloorInt64(center_bb_x / x_scale_factor / 10000000)) * 10000000 * x_scale_factor;
            Header.YOffset = (MyDefs.FloorInt64(center_bb_y / y_scale_factor / 10000000)) * 10000000 * y_scale_factor;
            Header.ZOffset = (MyDefs.FloorInt64(center_bb_z / z_scale_factor / 10000000)) * 10000000 * z_scale_factor;

            if (CheckForIntegerOverflow() != 0)
            {
                Header.XOffset = x_offset;
                Header.YOffset = y_offset;
                Header.ZOffset = z_offset;
                return 1;
            }

            return 0;
        }

        public int SetPoint(LasPoint point)
        {
            if (point == null)
            {
                throw new InvalidOperationException("laszip_point_struct pointer is zero");
            }

            if (reader != null)
            {
                throw new InvalidOperationException("cannot set point for reader");
            }

            this.Point.Classification = point.Classification;
            this.Point.EdgeOfFlightLine = point.EdgeOfFlightLine;
            this.Point.ExtendedClassification = point.ExtendedClassification;
            this.Point.ExtendedClassificationFlags = point.ExtendedClassificationFlags;
            this.Point.ExtendedNumberOfReturnsOfGivenPulse = point.ExtendedNumberOfReturnsOfGivenPulse;
            this.Point.ExtendedPointType = point.ExtendedPointType;
            this.Point.ExtendedReturnNumber = point.ExtendedReturnNumber;
            this.Point.ExtendedScanAngle = point.ExtendedScanAngle;
            this.Point.ExtendedScannerChannel = point.ExtendedScannerChannel;
            this.Point.Gpstime = point.Gpstime;
            this.Point.Intensity = point.Intensity;
            this.Point.NumExtraBytes = point.NumExtraBytes;
            this.Point.NumberOfReturnsOfGivenPulse = point.NumberOfReturnsOfGivenPulse;
            this.Point.PointSourceID = point.PointSourceID;
            this.Point.ReturnNumber = point.ReturnNumber;
            Array.Copy(point.Rgb, this.Point.Rgb, 4);
            this.Point.ScanAngleRank = point.ScanAngleRank;
            this.Point.ScanDirectionFlag = point.ScanDirectionFlag;
            this.Point.UserData = point.UserData;
            this.Point.X = point.X;
            this.Point.Y = point.Y;
            this.Point.Z = point.Z;
            Array.Copy(point.Wavepacket, this.Point.Wavepacket, 29);

            if (this.Point.ExtraBytes != null)
            {
                if (point.ExtraBytes != null)
                {
                    if (this.Point.NumExtraBytes == point.NumExtraBytes)
                    {
                        Array.Copy(point.ExtraBytes, this.Point.ExtraBytes, point.NumExtraBytes);
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("target point has {0} extra bytes but source point has {1}", this.Point.NumExtraBytes, point.NumExtraBytes));
                    }
                }
                else
                {
                    throw new InvalidOperationException("target point has extra bytes but source point does not");
                }
            }
            else
            {
                if (point.ExtraBytes != null)
                {
                    throw new InvalidOperationException("source point has extra bytes but target point does not");
                }
            }

            return 0;
        }

        public int SetCoordinates(double[] coordinates)
        {
            if (coordinates == null)
            {
                throw new InvalidOperationException("laszip_F64 coordinates pointer is zero");
            }

            if (reader != null)
            {
                throw new InvalidOperationException("cannot set coordinates for reader");
            }

            // set the coordinates
            Point.X = MyDefs.QuantizeInt32((coordinates[0] - Header.XOffset) / Header.XScaleFactor);
            Point.Y = MyDefs.QuantizeInt32((coordinates[1] - Header.YOffset) / Header.YScaleFactor);
            Point.Z = MyDefs.QuantizeInt32((coordinates[2] - Header.ZOffset) / Header.ZScaleFactor);

            return 0;
        }

        public int GetPointCoordinates(double[] coordinates)
        {
            if (coordinates == null)
            {
                throw new InvalidOperationException("laszip_F64 coordinates pointer is zero");
            }

            // get the coordinates
            coordinates[0] = Header.XScaleFactor * Point.X + Header.XOffset;
            coordinates[1] = Header.YScaleFactor * Point.Y + Header.YOffset;
            coordinates[2] = Header.ZScaleFactor * Point.Z + Header.ZOffset;

            return 0;
        }

        public unsafe int SetGeokeys(UInt16 number, LasGeoKeyEntry[] key_entries)
        {
            if (number == 0)
            {
                throw new InvalidOperationException("number of key_entries is zero");
            }

            if (key_entries == null)
            {
                throw new InvalidOperationException("key_entries pointer is zero");
            }

            if (reader != null)
            {
                throw new InvalidOperationException("cannot set geokeys after reader was opened");
            }

            if (writer != null)
            {
                throw new InvalidOperationException("cannot set geokeys after writer was opened");
            }

            // create the geokey directory
            // TODO: move serialization to LasGeokey
            byte[] buffer = new byte[sizeof(LasGeoKeyEntry) * (number + 1)];

            fixed (byte* pBuffer = buffer)
            {
                LasGeoKeyEntry* key_entries_plus_one = (LasGeoKeyEntry*)pBuffer;

                key_entries_plus_one[0].KeyID = 1;            // aka key_directory_version
                key_entries_plus_one[0].TiffTagLocation = 1; // aka key_revision
                key_entries_plus_one[0].Count = 0;             // aka minor_revision
                key_entries_plus_one[0].ValueOffset = number; // aka number_of_keys
                for (int i = 0; i < number; i++) key_entries_plus_one[i + 1] = key_entries[i];
            }

            // fill a VLR
            LasVariableLengthRecord vlr = new()
            {
                Reserved = 0xAABB
            };
            byte[] user_id = Encoding.ASCII.GetBytes("LASF_Projection");
            Array.Copy(user_id, vlr.UserID, Math.Min(user_id.Length, 16));
            vlr.RecordID = 34735;
            vlr.RecordLengthAfterHeader = (UInt16)(8 + number * 8);

            // description field must be a null-terminate string, so we don't copy more than 31 characters
            byte[] v = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
            Array.Copy(v, vlr.Description, Math.Min(v.Length, 31));

            vlr.Data = buffer;

            // add the VLR
            if (AddVlr(vlr) != 0)
            {
                throw new InvalidOperationException(String.Format("setting {0} geokeys", number));
            }

            return 0;
        }

        public int SetGeodoubleParams(UInt16 number, double[] geodoubleParams)
        {
            if (number == 0)
            {
                throw new InvalidOperationException("number of geodouble_params is zero");
            }
            if (geodoubleParams == null)
            {
                throw new InvalidOperationException("geodouble_params pointer is zero");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot set geodouble_params after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot set geodouble_params after writer was opened");
            }

            // fill a VLR
            // TODO: move serialization into LasGeoKeyEntry
            LasVariableLengthRecord vlr = new()
            {
                Reserved = 0xAABB
            };
            byte[] user_id = Encoding.ASCII.GetBytes("LASF_Projection");
            Array.Copy(user_id, vlr.UserID, Math.Min(user_id.Length, 16));
            vlr.RecordID = 34736;
            vlr.RecordLengthAfterHeader = (UInt16)(number * 8);

            // description field must be a null-terminate string, so we don't copy more than 31 characters
            byte[] v = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
            Array.Copy(v, vlr.Description, Math.Min(v.Length, 31));

            byte[] buffer = new byte[number * 8];
            Buffer.BlockCopy(geodoubleParams, 0, buffer, 0, number * 8);
            vlr.Data = buffer;

            // add the VLR
            if (AddVlr(vlr) != 0)
            {
                throw new InvalidOperationException(String.Format("setting {0} geodouble_params", number));
            }

            return 0;
        }

        public int SetGeoAsciiParams(UInt16 number, byte[] geoasciiParams)
        {
            if (number == 0)
            {
                throw new InvalidOperationException("number of geoascii_params is zero");
            }
            if (geoasciiParams == null)
            {
                throw new InvalidOperationException("geoascii_params pointer is zero");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot set geoascii_params after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot set geoascii_params after writer was opened");
            }

            // fill a VLR
            LasVariableLengthRecord vlr = new()
            {
                Reserved = 0xAABB
            };
            byte[] user_id = Encoding.ASCII.GetBytes("LASF_Projection");
            Array.Copy(user_id, vlr.UserID, Math.Min(user_id.Length, 16));
            vlr.RecordID = 34737;
            vlr.RecordLengthAfterHeader = number;

            // description field must be a null-terminate string, so we don't copy more than 31 characters
            byte[] v = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
            Array.Copy(v, vlr.Description, Math.Min(v.Length, 31));

            vlr.Data = geoasciiParams;

            // add the VLR
            if (AddVlr(vlr) != 0)
            {
                throw new InvalidOperationException(String.Format("setting {0} geoascii_params", number));
            }

            return 0;
        }

        private static bool ArrayCompare(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            int i = 0;
            for (; i < len; i++)
            {
                if (a[i] != b[i]) return false;
                if (a[i] == 0) break;
            }

            if (i < len - 1) return true;
            return a.Length == b.Length;
        }

        public int AddVlr(LasVariableLengthRecord vlr)
        {
            if (vlr == null)
            {
                throw new InvalidOperationException("laszip_vlr_struct vlr pointer is zero");
            }
            if ((vlr.RecordLengthAfterHeader > 0) && (vlr.Data == null))
            {
                throw new InvalidOperationException(String.Format("VLR has record_length_after_header of {0} but VLR data pointer is zero", vlr.RecordLengthAfterHeader));
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot add vlr after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot add vlr after writer was opened");
            }

            if (Header.Vlrs.Count > 0)
            {
                // overwrite existing VLR ?
                for (int i = (int)Header.NumberOfVariableLengthRecords - 1; i >= 0; i--)
                {
                    if (Header.Vlrs[i].RecordID == vlr.RecordID && !ArrayCompare(Header.Vlrs[i].UserID, vlr.UserID))
                    {
                        if (Header.Vlrs[i].RecordLengthAfterHeader != 0)
                            Header.OffsetToPointData -= Header.Vlrs[i].RecordLengthAfterHeader;

                        Header.Vlrs.RemoveAt(i);
                    }
                }
            }

            Header.Vlrs.Add(vlr);
            Header.NumberOfVariableLengthRecords = (UInt32)Header.Vlrs.Count;
            Header.OffsetToPointData += 54;

            // copy the VLR
            Header.OffsetToPointData += vlr.RecordLengthAfterHeader;

            return 0;
        }

        private static void CheckHeaderAndSetup(LasHeader header, bool compress, out LasZip lasZip, ref LasPoint point, out UInt32 laszipVlrPayloadSize)
        {
            #region check header and prepare point
            UInt32 vlrsSize = 0;
            if (header.VersionMajor != 1)
            {
                throw new InvalidOperationException(String.Format("unknown LAS version {0}.{1}", header.VersionMajor, header.VersionMinor));
            }
            if (compress && (header.PointDataFormat > 5))
            {
                throw new InvalidOperationException(String.Format("compressor does not yet support point data format {0}", header.PointDataFormat));
            }
            if (header.NumberOfVariableLengthRecords != 0)
            {
                if (header.Vlrs == null)
                {
                    throw new InvalidOperationException(String.Format("number_of_variable_length_records is {0} but vlrs pointer is zero", header.NumberOfVariableLengthRecords));
                }

                for (int i = 0; i < header.NumberOfVariableLengthRecords; i++)
                {
                    vlrsSize += 54;
                    if (header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        vlrsSize += header.Vlrs[i].RecordLengthAfterHeader;
                    }
                }
            }

            if ((vlrsSize + header.HeaderSize + header.UserDataAfterHeaderSize) != header.OffsetToPointData)
            {
                throw new InvalidOperationException(String.Format("header_size ({0}) plus vlrs_size ({1}) plus user_data_after_header_size ({2}) does not equal offset_to_point_data ({3})", header.HeaderSize, vlrsSize, header.UserDataAfterHeaderSize, header.OffsetToPointData));
            }

            lasZip = new();
            if (!lasZip.Setup(header.PointDataFormat, header.PointDataRecordLength, LasZip.CompressorNone))
            {
                throw new InvalidOperationException(String.Format("invalid combination of point_data_format {0} and point_data_record_length {1}", header.PointDataFormat, header.PointDataRecordLength));
            }

            for (UInt32 itemIndex = 0; itemIndex < lasZip.NumItems; itemIndex++)
            {
                switch (lasZip.Items[itemIndex].Type)
                {
                    case LasItemType.Point14:
                    case LasItemType.Point10:
                    case LasItemType.Gpstime11:
                    case LasItemType.RgbNir14:
                    case LasItemType.Rgb12:
                    case LasItemType.Wavepacket13: break;
                    case LasItemType.Byte:
                        point.NumExtraBytes = lasZip.Items[itemIndex].Size;
                        point.ExtraBytes = new byte[point.NumExtraBytes];
                        break;
                    default:
                        throw new InvalidOperationException(String.Format("unknown LASitem type {0}", lasZip.Items[itemIndex].Type));
                }
            }

            if (compress)
            {
                if (!lasZip.Setup(header.PointDataFormat, header.PointDataRecordLength, LasZip.CompressorDefault))
                {
                    throw new InvalidOperationException(String.Format("cannot compress point_data_format {0} with point_data_record_length {1}", header.PointDataFormat, header.PointDataRecordLength));
                }
                lasZip.RequestVersion(2);
                laszipVlrPayloadSize = 34u + 6u * lasZip.NumItems;
            }
            else
            {
                lasZip.RequestVersion(0);
                laszipVlrPayloadSize = 0;
            }
            #endregion
        }

        public int OpenWriter(Stream streamOut, bool compress, bool leaveOpen = false)
        {
            if (!streamOut.CanWrite)
            {
                throw new InvalidOperationException("can not write output stream");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            LasZipDll.CheckHeaderAndSetup(this.Header, compress, out LasZip? lasZip, ref this.Point, out UInt32 vlrPayloadSize);

            this.streamOut = streamOut;
            leaveStreamOutOpen = leaveOpen;

            return this.OpenWriterStream(compress, lasZip, vlrPayloadSize);
        }

        public int OpenWriter(string filePath, bool compress)
        {
            if (String.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentOutOfRangeException(nameof(filePath), "string file_name pointer is zero");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            LasZipDll.CheckHeaderAndSetup(this.Header, compress, out LasZip? lasZip, ref this.Point, out UInt32 laszipVlrPayloadSize);

            streamOut = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            leaveStreamOutOpen = false;

            return this.OpenWriterStream(compress, lasZip, laszipVlrPayloadSize);
        }

        private int OpenWriterStream(bool compress, LasZip laszip, UInt32 laszipVlrPayloadSize)
        {
            #region write the header variable after variable
            streamOut.WriteByte((byte)'L');
            streamOut.WriteByte((byte)'A');
            streamOut.WriteByte((byte)'S');
            streamOut.WriteByte((byte)'F');

            streamOut.Write(BitConverter.GetBytes(Header.FileSourceID), 0, 2);
            streamOut.Write(BitConverter.GetBytes(Header.GlobalEncoding), 0, 2);
            streamOut.Write(BitConverter.GetBytes(Header.ProjectIDGuidData1), 0, 4);
            streamOut.Write(BitConverter.GetBytes(Header.ProjectIDGuidData2), 0, 2);
            streamOut.Write(BitConverter.GetBytes(Header.ProjectIDGuidData3), 0, 2);
            streamOut.Write(Header.ProjectIDGuidData4, 0, 8);
            streamOut.WriteByte(Header.VersionMajor);
            streamOut.WriteByte(Header.VersionMinor);
            streamOut.Write(Header.SystemIdentifier, 0, 32);

            if (Header.GeneratingSoftware == null || Header.GeneratingSoftware.Length != 32)
            {
                byte[] generatingSoftware = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
                Array.Copy(generatingSoftware, Header.GeneratingSoftware, Math.Min(generatingSoftware.Length, 32));
            }

            streamOut.Write(Header.GeneratingSoftware, 0, 32);
            streamOut.Write(BitConverter.GetBytes(Header.FileCreationDay), 0, 2);
            streamOut.Write(BitConverter.GetBytes(Header.FileCreationYear), 0, 2);
            streamOut.Write(BitConverter.GetBytes(Header.HeaderSize), 0, 2);

            if (compress) { Header.OffsetToPointData += (54 + laszipVlrPayloadSize); }

            streamOut.Write(BitConverter.GetBytes(Header.OffsetToPointData), 0, 4);
            if (compress)
            {
                Header.OffsetToPointData -= (54 + laszipVlrPayloadSize);
                Header.NumberOfVariableLengthRecords += 1;
            }

            streamOut.Write(BitConverter.GetBytes(Header.NumberOfVariableLengthRecords), 0, 4);
            if (compress)
            {
                Header.NumberOfVariableLengthRecords -= 1;
                Header.PointDataFormat |= 128;
            }

            streamOut.WriteByte(Header.PointDataFormat);
            if (compress) { Header.PointDataFormat &= 127; }

            streamOut.Write(BitConverter.GetBytes(Header.PointDataRecordLength), 0, 2);
            streamOut.Write(BitConverter.GetBytes(Header.NumberOfPointRecords), 0, 4);
            for (UInt32 i = 0; i < 5; i++)
            {
                streamOut.Write(BitConverter.GetBytes(Header.NumberOfPointsByReturn[i]), 0, 4);
            }

            streamOut.Write(BitConverter.GetBytes(Header.XScaleFactor), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.YScaleFactor), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.ZScaleFactor), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.XOffset), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.YOffset), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.ZOffset), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.MaxX), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.MinX), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.MaxY), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.MinY), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.MaxZ), 0, 8);
            streamOut.Write(BitConverter.GetBytes(Header.MinZ), 0, 8);

            #region special handling for LAS 1.3+
            if (Header.VersionMajor == 1 && Header.VersionMinor >= 3)
            {
                if (Header.HeaderSize < 235)
                {
                    throw new InvalidOperationException(String.Format("for LAS 1.{0} header_size should at least be 235 but it is only {1}", Header.VersionMinor, Header.HeaderSize));
                }

                streamOut.Write(BitConverter.GetBytes(Header.StartOfWaveformDataPacketRecord), 0, 8);
                Header.UserDataInHeaderSize = Header.HeaderSize - 235u;
            }
            else Header.UserDataInHeaderSize = Header.HeaderSize - 227u;
            #endregion

            #region special handling for LAS 1.4+
            if (Header.VersionMajor == 1 && Header.VersionMinor >= 4)
            {
                if (Header.HeaderSize < 375)
                {
                    throw new InvalidOperationException(String.Format("for LAS 1.{0} header_size should at least be 375 but it is only {1}", Header.VersionMinor, Header.HeaderSize));
                }

                streamOut.Write(BitConverter.GetBytes(Header.StartOfFirstExtendedVariableLengthRecord), 0, 8);
                streamOut.Write(BitConverter.GetBytes(Header.NumberOfExtendedVariableLengthRecords), 0, 4);
                streamOut.Write(BitConverter.GetBytes(Header.ExtendedNumberOfPointRecords), 0, 8);
                for (UInt32 i = 0; i < 15; i++)
                {
                    streamOut.Write(BitConverter.GetBytes(Header.ExtendedNumberOfPointsByReturn[i]), 0, 8);
                }

                Header.UserDataInHeaderSize = Header.HeaderSize - 375u;
            }
            #endregion

            #region write any number of user-defined bytes that might have been added to the header
            if (Header.UserDataInHeaderSize != 0)
            {
                streamOut.Write(Header.UserDataInHeader, 0, (int)Header.UserDataInHeaderSize);
            }
            #endregion

            #region write variable length records into the header
            if (Header.NumberOfVariableLengthRecords != 0)
            {
                for (int i = 0; i < Header.NumberOfVariableLengthRecords; i++)
                {
                    // write variable length records variable after variable (to avoid alignment issues)
                    streamOut.Write(BitConverter.GetBytes(Header.Vlrs[i].Reserved), 0, 2);
                    streamOut.Write(Header.Vlrs[i].UserID, 0, 16);
                    streamOut.Write(BitConverter.GetBytes(Header.Vlrs[i].RecordID), 0, 2);
                    streamOut.Write(BitConverter.GetBytes(Header.Vlrs[i].RecordLengthAfterHeader), 0, 2);
                    streamOut.Write(Header.Vlrs[i].Description, 0, 32);
                   
                    // write data following the header of the variable length record
                    if (Header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        streamOut.Write(Header.Vlrs[i].Data, 0, Header.Vlrs[i].RecordLengthAfterHeader);
                    }
                }
            }

            if (compress)
            {
                #region write the LASzip VLR header
                UInt32 i = Header.NumberOfVariableLengthRecords;

                UInt16 reserved = 0xAABB; // TODO: write 0 in LAS 1.5
                streamOut.Write(BitConverter.GetBytes(reserved), 0, 2);

                byte[] user_id1 = Encoding.ASCII.GetBytes("laszip encoded");
                byte[] user_id = new byte[16];
                Array.Copy(user_id1, user_id, Math.Min(16, user_id1.Length));
                streamOut.Write(user_id, 0, 16);

                UInt16 record_id = 22204;
                streamOut.Write(BitConverter.GetBytes(record_id), 0, 2);

                UInt16 record_length_after_header = (UInt16)laszipVlrPayloadSize;
                streamOut.Write(BitConverter.GetBytes(record_length_after_header), 0, 2);

                // description field must be a null-terminate string, so we don't copy more than 31 characters
                byte[] description1 = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
                byte[] description = new byte[32];
                Array.Copy(description1, description, Math.Min(31, description1.Length));

                streamOut.Write(description, 0, 32);

                // write the LASzip VLR payload

                //     U16  compressor                2 bytes
                //     U32  coder                     2 bytes
                //     U8   version_major             1 byte
                //     U8   version_minor             1 byte
                //     U16  version_revision          2 bytes
                //     U32  options                   4 bytes
                //     I32  chunk_size                4 bytes
                //     I64  number_of_special_evlrs   8 bytes
                //     I64  offset_to_special_evlrs   8 bytes
                //     U16  num_items                 2 bytes
                //        U16 type                2 bytes * num_items
                //        U16 size                2 bytes * num_items
                //        U16 version             2 bytes * num_items
                // which totals 34+6*num_items

                streamOut.Write(BitConverter.GetBytes(laszip.Compressor), 0, 2);
                streamOut.Write(BitConverter.GetBytes(laszip.Coder), 0, 2);
                streamOut.WriteByte(laszip.VersionMajor);
                streamOut.WriteByte(laszip.VersionMinor);
                streamOut.Write(BitConverter.GetBytes(laszip.VersionRevision), 0, 2);
                streamOut.Write(BitConverter.GetBytes(laszip.Options), 0, 4);
                streamOut.Write(BitConverter.GetBytes(laszip.ChunkSize), 0, 4);
                streamOut.Write(BitConverter.GetBytes(laszip.NumberOfSpecialEvlrs), 0, 8);
                streamOut.Write(BitConverter.GetBytes(laszip.OffsetToSpecialEvlrs), 0, 8);
                streamOut.Write(BitConverter.GetBytes(laszip.NumItems), 0, 2);
                for (UInt32 j = 0; j < laszip.NumItems; j++)
                {
                    UInt16 type = (UInt16)laszip.Items[j].Type;
                    streamOut.Write(BitConverter.GetBytes(type), 0, 2);
                    streamOut.Write(BitConverter.GetBytes(laszip.Items[j].Size), 0, 2);
                    streamOut.Write(BitConverter.GetBytes(laszip.Items[j].Version), 0, 2);
                }
                #endregion
            }
            #endregion

            #region write any number of user-defined bytes that might have been added after the header
            if (Header.UserDataAfterHeaderSize != 0)
            {
                streamOut.Write(Header.UserDataAfterHeader, 0, (int)Header.UserDataAfterHeaderSize);
            }
            #endregion

            #endregion

            #region create the point writer
            writer = new LasWritePoint();
            if (!writer.Setup(laszip))
            {
                throw new InvalidOperationException("setup of LASwritePoint failed");
            }

            if (!writer.Init(streamOut))
            {
                throw new InvalidOperationException("init of LASwritePoint failed");
            }
            #endregion

            // set the point number and point count
            nPoints = Header.NumberOfPointRecords;
            currentPointIndex = 0;

            return 0;
        }

        public int WritePoint()
        {
            if (writer == null)
            {
                throw new InvalidOperationException("writing points before writer was opened");
            }

            // write the point
            if (!writer.Write(Point))
            {
                throw new InvalidOperationException(String.Format("writing point with index {0} of {1} total points", currentPointIndex, nPoints));
            }

            currentPointIndex++;

            return 0;
        }

        public int CloseWriter()
        {
            if (writer == null)
            {
                throw new InvalidOperationException("closing writer before it was opened");
            }
            if (writer.Done() == false)
            {
                throw new InvalidOperationException("done of LASwritePoint failed");
            }

            this.writer = null;
            if ((leaveStreamOutOpen == false) && (this.streamOut != null))
            { 
                this.streamOut.Close();
                streamOut = null;
            }

            return 0;
        }

        public void OpenReader(Stream streamIn, ref bool isCompressed, bool leaveOpen = false)
        {
            if (!streamIn.CanRead)
            {
                throw new InvalidOperationException("can not read input stream");
            }
            if (streamIn.Length <= 0)
            {
                throw new InvalidOperationException("input stream is empty : nothing to read");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }

            this.streamIn = streamIn;
            leaveStreamInOpen = leaveOpen;

            this.OpenReaderStream(ref isCompressed);
        }

        public void OpenReader(string filePath, ref bool isCompressed)
        {
            if (filePath == null || filePath.Length == 0)
            {
                throw new InvalidOperationException("file_name pointer is zero");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }

            // open the file
            this.streamIn = File.OpenRead(filePath);
            this.leaveStreamInOpen = false;

            this.OpenReaderStream(ref isCompressed);
        }

        private void OpenReaderStream(ref bool isCompressed)
        {
            Debug.Assert(this.streamIn != null);

            #region read the header variable after variable
            byte[] buffer = new byte[32];
            if (streamIn.Read(buffer, 0, 4) != 4)
            {
                throw new InvalidOperationException("reading header.file_signature");
            }

            if (buffer[0] != 'L' && buffer[1] != 'A' && buffer[2] != 'S' && buffer[3] != 'F')
            {
                throw new InvalidOperationException("wrong file_signature. not a LAS/LAZ file.");
            }

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.file_source_ID");
            }
            Header.FileSourceID = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.global_encoding");
            }
            Header.GlobalEncoding = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 4) != 4)
            {
                throw new InvalidOperationException("reading header.project_ID_GUID_data_1");
            }
            Header.ProjectIDGuidData1 = BitConverter.ToUInt32(buffer, 0);

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.project_ID_GUID_data_2");
            }
            Header.ProjectIDGuidData2 = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.project_ID_GUID_data_3");
            }
            Header.ProjectIDGuidData3 = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(Header.ProjectIDGuidData4, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.project_ID_GUID_data_4");
            }

            if (streamIn.Read(buffer, 0, 1) != 1)
            {
                throw new InvalidOperationException("reading header.version_major");
            }
            Header.VersionMajor = buffer[0];

            if (streamIn.Read(buffer, 0, 1) != 1)
            {
                throw new InvalidOperationException("reading header.version_minor");
            }
            Header.VersionMinor = buffer[0];

            if (streamIn.Read(Header.SystemIdentifier, 0, 32) != 32)
            {
                throw new InvalidOperationException("reading header.system_identifier");
            }

            if (streamIn.Read(Header.GeneratingSoftware, 0, 32) != 32)
            {
                throw new InvalidOperationException("reading header.generating_software");
            }

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.file_creation_day");
            }
            Header.FileCreationDay = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.file_creation_year");
            }
            Header.FileCreationYear = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.header_size");
            }
            Header.HeaderSize = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 4) != 4)
            {
                throw new InvalidOperationException("reading header.offset_to_point_data");
            }
            Header.OffsetToPointData = BitConverter.ToUInt32(buffer, 0);

            if (streamIn.Read(buffer, 0, 4) != 4)
            {
                throw new InvalidOperationException("reading header.number_of_variable_length_records");
            }
            Header.NumberOfVariableLengthRecords = BitConverter.ToUInt32(buffer, 0);

            if (streamIn.Read(buffer, 0, 1) != 1)
            {
                throw new InvalidOperationException("reading header.point_data_format");
            }
            Header.PointDataFormat = buffer[0];

            if (streamIn.Read(buffer, 0, 2) != 2)
            {
                throw new InvalidOperationException("reading header.point_data_record_length");
            }
            Header.PointDataRecordLength = BitConverter.ToUInt16(buffer, 0);

            if (streamIn.Read(buffer, 0, 4) != 4)
            {
                throw new InvalidOperationException("reading header.number_of_point_records");
            }
            Header.NumberOfPointRecords = BitConverter.ToUInt32(buffer, 0);

            for (int i = 0; i < 5; i++)
            {
                if (streamIn.Read(buffer, 0, 4) != 4)
                {
                    throw new InvalidOperationException(String.Format("reading header.number_of_points_by_return {0}", i));
                }
                Header.NumberOfPointsByReturn[i] = BitConverter.ToUInt32(buffer, 0);
            }

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.x_scale_factor");
            }
            Header.XScaleFactor = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.y_scale_factor");
            }
            Header.YScaleFactor = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.z_scale_factor");
            }
            Header.ZScaleFactor = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.x_offset");
            }
            Header.XOffset = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.y_offset");
            }
            Header.YOffset = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.z_offset");
            }
            Header.ZOffset = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.max_x");
            }
            Header.MaxX = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.min_x");
            }
            Header.MinX = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.max_y");
            }
            Header.MaxY = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.min_y");
            }
            Header.MinY = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.max_z");
            }
            Header.MaxZ = BitConverter.ToDouble(buffer, 0);

            if (streamIn.Read(buffer, 0, 8) != 8)
            {
                throw new InvalidOperationException("reading header.min_z");
            }
            Header.MinZ = BitConverter.ToDouble(buffer, 0);

            // special handling for LAS 1.3
            if ((Header.VersionMajor == 1) && (Header.VersionMinor >= 3))
            {
                if (Header.HeaderSize < 235)
                {
                    throw new InvalidOperationException(String.Format("for LAS 1.{0} header_size should at least be 235 but it is only {1}", Header.VersionMinor, Header.HeaderSize));
                }
                else
                {
                    if (streamIn.Read(buffer, 0, 8) != 8)
                    {
                        throw new InvalidOperationException("reading header.start_of_waveform_data_packet_record");
                    }
                    Header.StartOfWaveformDataPacketRecord = BitConverter.ToUInt64(buffer, 0);
                    Header.UserDataInHeaderSize = (UInt32)Header.HeaderSize - 235;
                }
            }
            else
            {
                Header.UserDataInHeaderSize = (UInt32)Header.HeaderSize - 227;
            }

            // special handling for LAS 1.4
            if ((Header.VersionMajor == 1) && (Header.VersionMinor >= 4))
            {
                if (Header.HeaderSize < 375)
                {
                    throw new InvalidOperationException(String.Format("for LAS 1.{0} header_size should at least be 375 but it is only {1}", Header.VersionMinor, Header.HeaderSize));
                }
                else
                {
                    if (streamIn.Read(buffer, 0, 8) != 8)
                    {
                        throw new InvalidOperationException("reading header.start_of_first_extended_variable_length_record");
                    }
                    Header.StartOfFirstExtendedVariableLengthRecord = BitConverter.ToUInt64(buffer, 0);

                    if (streamIn.Read(buffer, 0, 4) != 4)
                    {
                        throw new InvalidOperationException("reading header.number_of_extended_variable_length_records");
                    }
                    Header.NumberOfExtendedVariableLengthRecords = BitConverter.ToUInt32(buffer, 0);

                    if (streamIn.Read(buffer, 0, 8) != 8)
                    {
                        throw new InvalidOperationException("reading header.extended_number_of_point_records");
                    }
                    Header.ExtendedNumberOfPointRecords = BitConverter.ToUInt64(buffer, 0);

                    for (int i = 0; i < 15; i++)
                    {
                        if (streamIn.Read(buffer, 0, 8) != 8)
                        {
                            throw new InvalidOperationException(String.Format("reading header.extended_number_of_points_by_return[{0}]", i));
                        }
                        Header.ExtendedNumberOfPointsByReturn[i] = BitConverter.ToUInt64(buffer, 0);
                    }
                    Header.UserDataInHeaderSize = (UInt32)Header.HeaderSize - 375;
                }
            }

            // load any number of user-defined bytes that might have been added to the header
            if (Header.UserDataInHeaderSize != 0)
            {
                Header.UserDataInHeader = new byte[Header.UserDataInHeaderSize];

                if (streamIn.Read(Header.UserDataInHeader, 0, (int)Header.UserDataInHeaderSize) != Header.UserDataInHeaderSize)
                {
                    throw new InvalidOperationException(String.Format("reading {0} bytes of data into header.user_data_in_header", Header.UserDataInHeaderSize));
                }
            }
            #endregion

            #region read variable length records into the header
            UInt32 vlrsSize = 0;
            LasZip? lasZip = null;

            if (Header.NumberOfVariableLengthRecords != 0)
            {
                for (int vlrIndex = 0; vlrIndex < Header.NumberOfVariableLengthRecords; vlrIndex++)
                {
                    Header.Vlrs.Add(new LasVariableLengthRecord());

                    // make sure there are enough bytes left to read a variable length record before the point block starts
                    if (((int)Header.OffsetToPointData - vlrsSize - Header.HeaderSize) < 54)
                    {
                        warning = String.Format("only {0} bytes until point block after reading {1} of {2} vlrs. skipping remaining vlrs ...", (int)Header.OffsetToPointData - vlrsSize - Header.HeaderSize, vlrIndex, Header.NumberOfVariableLengthRecords);
                        Header.NumberOfVariableLengthRecords = (UInt32)vlrIndex;
                        break;
                    }

                    // read variable length records variable after variable (to avoid alignment issues)
                    if (streamIn.Read(buffer, 0, 2) != 2)
                    {
                        throw new InvalidOperationException(String.Format("reading header.vlrs[{0}].reserved", vlrIndex));
                    }
                    Header.Vlrs[vlrIndex].Reserved = BitConverter.ToUInt16(buffer, 0);

                    if (streamIn.Read(Header.Vlrs[vlrIndex].UserID, 0, 16) != 16)
                    {
                        throw new InvalidOperationException(String.Format("reading header.vlrs[{0}].user_id", vlrIndex));
                    }

                    if (streamIn.Read(buffer, 0, 2) != 2)
                    {
                        throw new InvalidOperationException(String.Format("reading header.vlrs[{0}].record_id", vlrIndex));
                    }
                    Header.Vlrs[vlrIndex].RecordID = BitConverter.ToUInt16(buffer, 0);

                    if (streamIn.Read(buffer, 0, 2) != 2)
                    {
                        throw new InvalidOperationException(String.Format("reading header.vlrs[{0}].record_length_after_header", vlrIndex));
                    }
                    Header.Vlrs[vlrIndex].RecordLengthAfterHeader = BitConverter.ToUInt16(buffer, 0);

                    if (streamIn.Read(Header.Vlrs[vlrIndex].Description, 0, 32) != 32)
                    {
                        throw new InvalidOperationException(String.Format("reading header.vlrs[{0}].description", vlrIndex));
                    }

                    // keep track on the number of bytes we have read so far
                    vlrsSize += 54;

                    // check variable length record contents
                    // VLRs defined in LAS 1.0 with a record signature of 0xAABB. LAS 1.1 removed the record signature, changing the
                    // field to reserved with an unconstrained value and LAS 1.4 introduced a requirement the reserved field have a
                    // value of zero.
                    // TODO: hoist the read of VersionMajor and VersionMinor below so this check can be implemented correctly
                    // For now, approximate correct behavior by accepting either
                    if ((Header.Vlrs[vlrIndex].Reserved != 0) && (Header.Vlrs[vlrIndex].Reserved != 0xAABB))
                    {
                        warning = String.Format("wrong header.vlrs[" + vlrIndex + "].reserved: " + Header.Vlrs[vlrIndex].Reserved + " != 0xAABB");
                    }

                    // make sure there are enough bytes left to read the data of the variable length record before the point block starts
                    if (((int)Header.OffsetToPointData - vlrsSize - Header.HeaderSize) < Header.Vlrs[vlrIndex].RecordLengthAfterHeader)
                    {
                        warning = String.Format("only {0} bytes until point block when trying to read {1} bytes into header.vlrs[{2}].data", (int)Header.OffsetToPointData - vlrsSize - Header.HeaderSize, Header.Vlrs[vlrIndex].RecordLengthAfterHeader, vlrIndex);
                        Header.Vlrs[vlrIndex].RecordLengthAfterHeader = (UInt16)(Header.OffsetToPointData - vlrsSize - Header.HeaderSize);
                    }

                    string userid = "";
                    for (int a = 0; a < Header.Vlrs[vlrIndex].UserID.Length; a++)
                    {
                        if (Header.Vlrs[vlrIndex].UserID[a] == 0) break;
                        userid += (char)Header.Vlrs[vlrIndex].UserID[a];
                    }

                    // load data following the header of the variable length record
                    if (Header.Vlrs[vlrIndex].RecordLengthAfterHeader != 0)
                    {
                        if (userid == "laszip encoded")
                        {
                            lasZip = new LasZip();

                            // read the LASzip VLR payload

                            //     U16  compressor                2 bytes 
                            //     U32  coder                     2 bytes 
                            //     U8   version_major             1 byte 
                            //     U8   version_minor             1 byte
                            //     U16  version_revision          2 bytes
                            //     U32  options                   4 bytes 
                            //     I32  chunk_size                4 bytes
                            //     I64  number_of_special_evlrs   8 bytes
                            //     I64  offset_to_special_evlrs   8 bytes
                            //     U16  num_items                 2 bytes
                            //        U16 type                2 bytes * num_items
                            //        U16 size                2 bytes * num_items
                            //        U16 version             2 bytes * num_items
                            // which totals 34+6*num_items

                            if (streamIn.Read(buffer, 0, 2) != 2)
                            {
                                throw new InvalidOperationException("reading compressor");
                            }
                            lasZip.Compressor = BitConverter.ToUInt16(buffer, 0);

                            if (streamIn.Read(buffer, 0, 2) != 2)
                            {
                                throw new InvalidOperationException("reading coder");
                            }
                            lasZip.Coder = BitConverter.ToUInt16(buffer, 0);

                            if (streamIn.Read(buffer, 0, 1) != 1)
                            {
                                throw new InvalidOperationException("reading version_major");
                            }
                            lasZip.VersionMajor = buffer[0];

                            if (streamIn.Read(buffer, 0, 1) != 1)
                            {
                                throw new InvalidOperationException("reading version_minor");
                            }
                            lasZip.VersionMinor = buffer[0];

                            if (streamIn.Read(buffer, 0, 2) != 2)
                            {
                                throw new InvalidOperationException("reading version_revision");
                            }
                            lasZip.VersionRevision = BitConverter.ToUInt16(buffer, 0);

                            if (streamIn.Read(buffer, 0, 4) != 4)
                            {
                                throw new InvalidOperationException("reading options");
                            }
                            lasZip.Options = BitConverter.ToUInt32(buffer, 0);

                            if (streamIn.Read(buffer, 0, 4) != 4)
                            {
                                throw new InvalidOperationException("reading chunk_size");
                            }
                            lasZip.ChunkSize = BitConverter.ToUInt32(buffer, 0);

                            if (streamIn.Read(buffer, 0, 8) != 8)
                            {
                                throw new InvalidOperationException("reading number_of_special_evlrs");
                            }
                            lasZip.NumberOfSpecialEvlrs = BitConverter.ToInt64(buffer, 0);

                            if (streamIn.Read(buffer, 0, 8) != 8)
                            {
                                throw new InvalidOperationException("reading offset_to_special_evlrs");
                            }
                            lasZip.OffsetToSpecialEvlrs = BitConverter.ToInt64(buffer, 0);

                            if (streamIn.Read(buffer, 0, 2) != 2)
                            {
                                throw new InvalidOperationException("reading num_items");
                            }
                            lasZip.NumItems = BitConverter.ToUInt16(buffer, 0);

                            lasZip.Items = new LasItem[lasZip.NumItems];
                            for (int j = 0; j < lasZip.NumItems; j++)
                            {
                                lasZip.Items[j] = new LasItem();

                                if (streamIn.Read(buffer, 0, 2) != 2)
                                {
                                    throw new InvalidOperationException(String.Format("reading type of item {0}", j));
                                }
                                lasZip.Items[j].Type = (LasItemType)BitConverter.ToUInt16(buffer, 0);

                                if (streamIn.Read(buffer, 0, 2) != 2)
                                {
                                    throw new InvalidOperationException(String.Format("reading size of item {0}", j));
                                }
                                lasZip.Items[j].Size = BitConverter.ToUInt16(buffer, 0);

                                if (streamIn.Read(buffer, 0, 2) != 2)
                                {
                                    throw new InvalidOperationException(String.Format("reading version of item {0}", j));
                                }
                                lasZip.Items[j].Version = BitConverter.ToUInt16(buffer, 0);
                            }
                        }
                        else
                        {
                            Header.Vlrs[vlrIndex].Data = new byte[Header.Vlrs[vlrIndex].RecordLengthAfterHeader];
                            if (streamIn.Read(Header.Vlrs[vlrIndex].Data, 0, Header.Vlrs[vlrIndex].RecordLengthAfterHeader) != Header.Vlrs[vlrIndex].RecordLengthAfterHeader)
                            {
                                throw new InvalidOperationException(String.Format("reading {0} bytes of data into header.vlrs[{1}].data", Header.Vlrs[vlrIndex].RecordLengthAfterHeader, vlrIndex));
                            }
                        }
                    }
                    else
                    {
                        Header.Vlrs[vlrIndex].Data = null;
                    }

                    // keep track on the number of bytes we have read so far
                    vlrsSize += Header.Vlrs[vlrIndex].RecordLengthAfterHeader;

                    // special handling for LASzip VLR
                    if (userid == "laszip encoded")
                    {
                        // we take our the VLR for LASzip away
                        Header.OffsetToPointData -= (UInt32)(54 + Header.Vlrs[vlrIndex].RecordLengthAfterHeader);
                        vlrsSize -= (UInt32)(54 + Header.Vlrs[vlrIndex].RecordLengthAfterHeader);
                        Header.Vlrs.RemoveAt(vlrIndex);
                        vlrIndex--;
                        Header.NumberOfVariableLengthRecords--;
                    }
                }
            }
            #endregion

            // load any number of user-defined bytes that might have been added after the header
            Header.UserDataAfterHeaderSize = Header.OffsetToPointData - vlrsSize - Header.HeaderSize;
            if (Header.UserDataAfterHeaderSize != 0)
            {
                Header.UserDataAfterHeader = new byte[Header.UserDataAfterHeaderSize];

                if (streamIn.Read(Header.UserDataAfterHeader, 0, (int)Header.UserDataAfterHeaderSize) != Header.UserDataAfterHeaderSize)
                {
                    throw new InvalidOperationException(String.Format("reading {0} bytes of data into header.user_data_after_header", Header.UserDataAfterHeaderSize));
                }
            }

            // remove extra bits in point data type
            if ((Header.PointDataFormat & 128) != 0 || (Header.PointDataFormat & 64) != 0)
            {
                if (lasZip == null)
                {
                    throw new InvalidOperationException("this file was compressed with an experimental version of LASzip. contact 'martin.isenburg@rapidlasso.com' for assistance");
                }
                Header.PointDataFormat &= 127;
            }

            // check if file is compressed
            if (lasZip != null)
            {
                // yes. check the compressor state
                isCompressed = true;
                if (!lasZip.Check())
                {
                    throw new InvalidOperationException(String.Format("{0} upgrade to the latest release of LAStools (with LASzip) or contact 'martin.isenburg@rapidlasso.com' for assistance", lasZip.GetError()));
                }
            }
            else
            {
                // no. setup an un-compressed read
                isCompressed = false;
                lasZip = new LasZip();
                if (!lasZip.Setup(Header.PointDataFormat, Header.PointDataRecordLength, LasZip.CompressorNone))
                {
                    throw new InvalidOperationException(String.Format("invalid combination of point_data_format {0} and point_data_record_length {1}", Header.PointDataFormat, Header.PointDataRecordLength));
                }
            }

            // create point's item pointers
            for (int i = 0; i < lasZip.NumItems; i++)
            {
                switch (lasZip.Items[i].Type)
                {
                    case LasItemType.Point14:
                    case LasItemType.Point10:
                    case LasItemType.Gpstime11:
                    case LasItemType.RgbNir14:
                    case LasItemType.Rgb12:
                    case LasItemType.Wavepacket13:
                        break;
                    case LasItemType.Byte:
                        Point.NumExtraBytes = lasZip.Items[i].Size;
                        Point.ExtraBytes = new byte[Point.NumExtraBytes];
                        break;
                    default:
                        throw new InvalidOperationException(String.Format("unknown LASitem type {0}", lasZip.Items[i].Type));
                }
            }

            // create the point reader
            reader = new LasReadPoint();
            if (!reader.Setup(lasZip))
            {
                throw new InvalidOperationException("setup of LASreadPoint failed");
            }

            if (!reader.Init(streamIn))
            {
                throw new InvalidOperationException("init of LASreadPoint failed");
            }

            // set the point number and point count
            nPoints = Header.NumberOfPointRecords;
            currentPointIndex = 0;
        }

        public int SeekToPoint(long index)
        {
            // seek to the point
            if (!this.reader.Seek((UInt32)currentPointIndex, (UInt32)index))
            {
                throw new InvalidOperationException(String.Format("seeking from index {0} to index {1} for file with {2} points", currentPointIndex, index, nPoints));
            }
            currentPointIndex = index;

            return 0;
        }

        public bool TryReadPoint()
        {
            if (reader == null)
            {
                throw new InvalidOperationException("reading points before reader was opened");
            }

            // read the point
            if (reader.TryRead(this.Point) == false)
            {
                return false;
            }

            currentPointIndex++;
            return true;
        }

        public int CloseReader()
        {
            if (reader == null)
            {
                throw new InvalidOperationException("closing reader before it was opened");
            }
            if (reader.Done() == false)
            {
                throw new InvalidOperationException("done of LASreadPoint failed");
            }

            reader = null;
            if ((leaveStreamInOpen == false) && (streamIn != null))
            { 
                streamIn.Close();
                streamIn = null;
            }

            return 0;
        }
    }
}
