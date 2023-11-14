// laszip_dll.cpp
using LasZip.Extensions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
        private byte[] point_items;

        private Stream? streamIn;
        private bool leaveStreamInOpen;
        private LasReadPoint? reader;

        private Stream? streamOut;
        private LasWritePoint? writer;
        private LasAttributer? attributer;

        private string warning;

        public LasHeader Header { get; private set; }
        public LasPoint Point { get; private set; }

        private LasIndex? laxIndex;
        private double laxRminX;
        private double laxRminY;
        private double laxRmaxX;
        private double laxRmaxY;
        private string? laxFileName;
        private LasZipDllInventory? inventory;

        private LasZipDecompressSelective las14decompressSelective;
        private bool request_native_extension;
        private bool request_compatibility_mode;
        private bool compatibility_mode;
        private UInt32 set_chunk_size;
        private Int32 start_scan_angle;
        private Int32 start_extended_returns;
        private Int32 start_classification;
        private Int32 start_flags_and_channel;
        private Int32 start_NIR_band;
        private readonly List<byte[]> buffers;

        public bool LaxAppend { get; private set; }
        public bool LaxCreate { get; private set; }
        public bool LaxExploit { get; private set; } // TODO: rename to UseLaxIndex
        public bool PreserveGeneratingSoftware { get; private set; }

        public LasZipDll()
        {
            this.attributer = null;
            this.buffers = new();
            this.point_items = Array.Empty<byte>();
            this.reader = null;
            this.streamIn = null;
            this.streamOut = null;
            this.writer = null;
            this.warning = String.Empty;

            this.Header = new();
            this.Point = new();
        }

        public string GetLastWarning()
        {
            return this.warning;
        }

        public void Clear()
        {
            if (this.reader != null)
            {
                this.CloseReader();
            }
            if (this.writer != null)
            {
                this.CloseWriter();
            }

            // zero everything
            this.Header.FileSourceID = 0;
            this.Header.GlobalEncoding = 0;
            this.Header.ProjectIDGuidData1 = 0;
            this.Header.ProjectIDGuidData2 = 0;
            this.Header.ProjectIDGuidData3 = 0;
            Array.Clear(this.Header.ProjectIDGuidData4, 0, this.Header.ProjectIDGuidData4.Length);
            this.Header.VersionMajor = 0;
            this.Header.VersionMinor = 0;
            Array.Clear(this.Header.SystemIdentifier, 0, this.Header.SystemIdentifier.Length);
            Array.Clear(this.Header.GeneratingSoftware, 0, this.Header.GeneratingSoftware.Length);
            this.Header.FileCreationDay = 0;
            this.Header.FileCreationYear = 0;
            this.Header.HeaderSize = 0;
            this.Header.OffsetToPointData = 0;
            this.Header.NumberOfVariableLengthRecords = 0;
            this.Header.PointDataFormat = 0;
            this.Header.PointDataRecordLength = 0;
            this.Header.NumberOfPointRecords = 0;
            Array.Clear(this.Header.NumberOfPointsByReturn, 0, this.Header.NumberOfPointsByReturn.Length);
            this.Header.XScaleFactor = 0;
            this.Header.YScaleFactor = 0;
            this.Header.ZScaleFactor = 0;
            this.Header.XOffset = 0;
            this.Header.YOffset = 0;
            this.Header.ZOffset = 0;
            this.Header.MaxX = 0;
            this.Header.MinX = 0;
            this.Header.MaxY = 0;
            this.Header.MinY = 0;
            this.Header.MaxZ = 0;
            this.Header.MinZ = 0;
            this.Header.StartOfWaveformDataPacketRecord = 0;
            this.Header.StartOfFirstExtendedVariableLengthRecord = 0;
            this.Header.NumberOfExtendedVariableLengthRecords = 0;
            this.Header.ExtendedNumberOfPointRecords = 0;
            Array.Clear(this.Header.ExtendedNumberOfPointsByReturn, 0, this.Header.ExtendedNumberOfPointsByReturn.Length);
            this.Header.UserDataInHeaderSize = 0;
            this.Header.UserDataInHeader = null;
            this.Header.Vlrs.Clear();
            this.Header.UserDataAfterHeaderSize = 0;
            this.Header.UserDataAfterHeader = null;

            this.currentPointIndex = 0;
            this.nPoints = 0;
            this.point_items = Array.Empty<byte>();

            this.Point.X = 0;
            this.Point.Y = 0;
            this.Point.Z = 0;
            this.Point.Intensity = 0;
            this.Point.ReturnNumber = 0;// : 3;
            this.Point.NumberOfReturnsOfGivenPulse = 0;// : 3;
            this.Point.ScanDirectionFlag = 0;// : 1;
            this.Point.EdgeOfFlightLine = 0;// : 1;
            this.Point.ClassificationAndFlags = 0;
            this.Point.ScanAngleRank = 0;
            this.Point.UserData = 0;
            this.Point.PointSourceID = 0;
            this.Point.Gpstime = 0;
            this.Point.Rgb = new UInt16[4];
            this.Point.Wavepacket = new byte[29];
            this.Point.ExtendedPointType = 0;// : 2;
            this.Point.ExtendedScannerChannel = 0;// : 2;
            this.Point.ExtendedClassificationFlags = 0;// : 4;
            this.Point.ExtendedClassification = 0;
            this.Point.ExtendedReturnNumber = 0;// : 4;
            this.Point.ExtendedNumberOfReturnsOfGivenPulse = 0;// : 4;
            this.Point.ExtendedScanAngle = 0;
            this.Point.NumExtraBytes = 0;
            this.Point.ExtraBytes = null;

            this.streamIn = null;
            this.reader = null;

            this.streamOut = null;
            this.writer = null;

            this.attributer = null;
            this.warning = String.Empty;
            this.laxIndex = null;
            this.laxRminX = 0.0;
            this.laxRminY = 0.0;
            this.laxRmaxX = 0.0;
            this.laxRmaxY = 0.0;
            this.laxFileName = null;
            this.LaxCreate = false;
            this.LaxAppend = false;
            this.LaxExploit = false;

            this.las14decompressSelective = LasZipDecompressSelective.ChannelReturnsXY;
            this.PreserveGeneratingSoftware = false;
            this.request_native_extension = false;
            this.request_compatibility_mode = false;
            this.compatibility_mode = false;

            this.set_chunk_size = 0;
            this.start_scan_angle = 0;
            this.start_extended_returns = 0;
            this.start_classification = 0;
            this.start_flags_and_channel = 0;
            this.start_NIR_band = 0;

            this.inventory = null;

            // create default header
            byte[] generatingSoftware = Encoding.ASCII.GetBytes(LasZip.GetAssemblyVersionString());
            Array.Copy(generatingSoftware, Header.GeneratingSoftware, Math.Min(generatingSoftware.Length, 32));
            this.Header.VersionMajor = 1;
            this.Header.VersionMinor = 2;
            this.Header.HeaderSize = 227;
            this.Header.OffsetToPointData = 227;
            this.Header.PointDataFormat = 1;
            this.Header.PointDataRecordLength = 28;
            this.Header.XScaleFactor = 0.01;
            this.Header.YScaleFactor = 0.01;
            this.Header.ZScaleFactor = 0.01;

            this.set_chunk_size = LasZip.ChunkSizeDefault;
            this.request_native_extension = true;
            this.las14decompressSelective = LasZipDecompressSelective.All;
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
            if ((this.reader == null) && (this.writer == null))
            {
                throw new InvalidOperationException("getting count before reader or writer was opened");
            }

            pointCount = this.nPoints;
        }

        public int SetHeader(LasHeader header)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("cannot set header after reader was opened");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("cannot set header after writer was opened");
            }

            this.attributer = null;

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
            for (int i = 0; i < 5; i++)
            {
                this.Header.NumberOfPointsByReturn[i] = header.NumberOfPointsByReturn[i];
            }
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
                if ((header.UserDataInHeader == null) || (header.UserDataAfterHeaderSize != header.UserDataInHeader.Length))
                {
                    throw new ArgumentOutOfRangeException(nameof(header));
                }

                this.Header.UserDataInHeader = new byte[header.UserDataInHeaderSize];
                Array.Copy(header.UserDataInHeader, this.Header.UserDataInHeader, header.UserDataInHeaderSize);
            }
            // else { ignore header.UserDataInHeader even it's not null or empty }

            if (header.NumberOfVariableLengthRecords != 0)
            {
                for (int i = 0; i < header.NumberOfVariableLengthRecords; i++)
                {
                    this.Header.Vlrs.Add(new());
                    this.Header.Vlrs[i].Reserved = header.Vlrs[i].Reserved;
                    Array.Copy(header.Vlrs[i].UserID, this.Header.Vlrs[i].UserID, 16);
                    this.Header.Vlrs[i].RecordID = header.Vlrs[i].RecordID;
                    this.Header.Vlrs[i].RecordLengthAfterHeader = header.Vlrs[i].RecordLengthAfterHeader;
                    Array.Copy(header.Vlrs[i].Description, this.Header.Vlrs[i].Description, 32);
                    if (header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        if (header.Vlrs[i].Data == null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(header), "Variable length record " + i + " has record length after header of " + header.Vlrs[i].RecordLengthAfterHeader + " bytes but its data array is null.");
                        }

                        this.Header.Vlrs[i].Data = new byte[header.Vlrs[i].RecordLengthAfterHeader];
                        Array.Copy(header.Vlrs[i].Data, this.Header.Vlrs[i].Data, header.Vlrs[i].RecordLengthAfterHeader); // ignore any extra bytes in data array
                    }
                    else
                    {
                        this.Header.Vlrs[i].Data = null;
                    }

                    // populate the attributer if needed
                    if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), "LASF_Spec", StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordID == 4))
                    {
                        this.attributer ??= new();
                        this.attributer.init_attributes((UInt32)(this.Header.Vlrs[i].RecordLengthAfterHeader / LasAttribute.SerializedSizeInBytes), this.Header.Vlrs[i].Data);
                    }
                }
            }
            // else { ignore header.Vlrs even it's not null or empty }

            this.Header.UserDataAfterHeaderSize = header.UserDataAfterHeaderSize;
            this.Header.UserDataAfterHeader = null;
            if (header.UserDataAfterHeaderSize != 0)
            {
                if (header.UserDataAfterHeader == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(header), "Header is followed by " + header.UserDataAfterHeaderSize + " bytes of user data but the user data array is null.");
                }

                this.Header.UserDataAfterHeader = new byte[header.UserDataAfterHeaderSize];
                Array.Copy(header.UserDataAfterHeader, this.Header.UserDataAfterHeader, header.UserDataAfterHeaderSize);
            }

            return 0;
        }

        private void SetPointTypeAndSize(LasZip lasZip, byte point_type, UInt16 point_size)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("cannot set point format and point size after reader was opened");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("cannot set point format and point size after writer was opened");
            }

            // check if point type and type are supported
            if (lasZip.Setup(point_type, point_size, LasZip.CompressorNone) == false)
            {
                throw new ArgumentException("invalid combination of " + nameof(point_type) + " " + point_type + " and " + nameof(point_size) + " " + point_size + ".");
            }

            // set point type and point size
            this.Header.PointDataFormat = point_type;
            this.Header.PointDataRecordLength = point_size;
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
            if (reader != null)
            {
                throw new InvalidOperationException("cannot set point for reader");
            }

            this.Point.ClassificationAndFlags = point.ClassificationAndFlags;
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
                        throw new InvalidOperationException("target point has " + this.Point.NumExtraBytes + " extra bytes but source point has " + point.NumExtraBytes + ".");
                    }
                }
                else if (this.compatibility_mode == false)
                {
                    throw new InvalidOperationException("target point has extra bytes but source point does not");
                }
            }
            //else
            //{
            //    if (point.ExtraBytes != null)
            //    {
            //        throw new InvalidOperationException("source point has extra bytes but target point does not");
            //    }
            //}

            return 0;
        }

        public void SetPointCoordinates(double[] coordinates)
        {
            this.Point.X = MyDefs.QuantizeInt32((coordinates[0] - this.Header.XOffset) / this.Header.XScaleFactor);
            this.Point.Y = MyDefs.QuantizeInt32((coordinates[1] - this.Header.YOffset) / this.Header.YScaleFactor);
            this.Point.Z = MyDefs.QuantizeInt32((coordinates[2] - this.Header.ZOffset) / this.Header.ZScaleFactor);
        }

        public void GetPointCoordinates(double[] coordinates)
        {
            coordinates[0] = this.Header.XScaleFactor * Point.X + this.Header.XOffset;
            coordinates[1] = this.Header.YScaleFactor * Point.Y + this.Header.YOffset;
            coordinates[2] = this.Header.ZScaleFactor * Point.Z + this.Header.ZOffset;
        }

        public int SetGeokeys(UInt16 number, LasGeoKeyEntry[] key_entries)
        {
            if (number == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "number of key_entries is zero");
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
            LasGeoKeyEntry[] key_entries_plus_one = new LasGeoKeyEntry[number + 1];
            key_entries_plus_one[0].KeyID = 1;            // aka key_directory_version
            key_entries_plus_one[0].TiffTagLocation = 1; // aka key_revision
            key_entries_plus_one[0].Count = 0;             // aka minor_revision
            key_entries_plus_one[0].ValueOffset = number; // aka number_of_keys
            for (int i = 0; i < number; i++)
            {
                key_entries_plus_one[i + 1] = key_entries[i];
            }

            // add the VLR
            if (this.AddVlr("LASF_Projection", 34735, (UInt16)(8 + 8 * number), null, MemoryMarshal.AsBytes(key_entries_plus_one.AsSpan())) != 0)
            {
                throw new InvalidOperationException(String.Format("setting {0} geokeys", number));
            }

            return 0;
        }

        public int SetGeodoubleParams(UInt16 number, double[] geodoubleParams)
        {
            if (number == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "number of geodouble_params is zero");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot set geodouble_params after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot set geodouble_params after writer was opened");
            }

            // add the VLR
            if (this.AddVlr("LASF_Projection", 34736, (UInt16)(8 * number), null, MemoryMarshal.AsBytes(geodoubleParams.AsSpan())) != 0)
            {
                throw new InvalidOperationException("setting " + number + " geodouble_params");
            }

            return 0;
        }

        public int SetGeoAsciiParams(UInt16 number, byte[] geoasciiParams)
        {
            if (number == 0)
            {
                throw new InvalidOperationException("number of geoascii_params is zero");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot set geoascii_params after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot set geoascii_params after writer was opened");
            }

            // add the VLR
            if (this.AddVlr("LASF_Projection", 34737, number, null, geoasciiParams) != 0)
            {
                throw new InvalidOperationException("setting " + number + " geoascii_params");
            }

            return 0;
        }

        private int AddAttribute(UInt32 type, string name, string description, double scale, double offset)
        {
            if (type > LasAttribute.LAS_ATTRIBUTE_F64)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot add attribute after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot add attribute after writer was opened");
            }

            LasAttribute lasattribute = new((byte)type, name, description);
            lasattribute.set_scale(scale);
            lasattribute.set_offset(offset);

            if (this.attributer == null)
            {
                this.attributer = new();
            }
            if (this.attributer.add_attribute(lasattribute) == -1)
            {
                throw new InvalidOperationException("cannot add attribute '" + name + "' to attributer");
            }
            byte[] vlrData = this.attributer.get_bytes();

            if (this.AddVlr("LASF_Spec\0\0\0\0\0\0", 4, (UInt16)(this.attributer.number_attributes * LasAttribute.SerializedSizeInBytes), null, vlrData) != 0)
            {
                throw new InvalidOperationException("adding the new extra bytes VLR with the additional attribute '" + name + "'");
            }

            return 0;
        }

        private int AddVlr(string user_id, UInt16 record_id, UInt16 record_length_after_header, string? description, ReadOnlySpan<byte> data)
        {
            if (record_length_after_header != data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(record_length_after_header), "record_length_after_header of VLR is " + record_length_after_header + " but data pointer is null");
            }
            if (reader != null)
            {
                throw new InvalidOperationException("cannot add vlr after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot add vlr after writer was opened");
            }

            int i = 0;
            if (this.Header.Vlrs.Count > 0)
            {
                // overwrite existing VLR ?
                for (i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                {
                    if ((this.Header.Vlrs[i].RecordID == record_id) && String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), user_id, StringComparison.Ordinal))
                    {
                        if (this.Header.Vlrs[i].RecordLengthAfterHeader != 0)
                        {
                            this.Header.OffsetToPointData -= this.Header.Vlrs[i].RecordLengthAfterHeader;
                            this.Header.Vlrs[i].RecordLengthAfterHeader = 0;
                            this.Header.Vlrs[i].Data = null;
                        }
                        break;
                    }
                }

                // create new VLR
                if (i == this.Header.NumberOfVariableLengthRecords)
                {
                    this.Header.NumberOfVariableLengthRecords++;
                    this.Header.OffsetToPointData += 54;
                }
            }
            else
            {
                this.Header.NumberOfVariableLengthRecords = 1;
                this.Header.OffsetToPointData += 54;
            }

            // create the VLR
            this.Header.Vlrs.Add(new()
            {
                Reserved = 0x0,
                RecordID = record_id
            });

            byte[] encodedUserID = Encoding.UTF8.GetBytes(user_id);
            encodedUserID.CopyTo(this.Header.Vlrs[i].UserID, 0);

            this.Header.Vlrs[i].RecordLengthAfterHeader = record_length_after_header;

            if (description != null)
            {
                description = LasZip.GetAssemblyVersionString();
            }
            byte[] encodedDesciption = Encoding.UTF8.GetBytes(description);
            encodedDesciption.CopyTo(this.Header.Vlrs[i].Description, 0);

            if (record_length_after_header != 0)
            {
                this.Header.OffsetToPointData += record_length_after_header;
                this.Header.Vlrs[i].Data = new byte[record_length_after_header];
                data.CopyTo(this.Header.Vlrs[i].Data);
            }

            return 0;
        }

        private int RemoveVlr(string user_id, UInt16 record_id)
        {
            if (reader != null)
            {
                throw new InvalidOperationException("cannot remove vlr after reader was opened");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("cannot remove vlr after writer was opened");
            }

            if (this.Header.Vlrs.Count > 0)
            {
                for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                {
                    if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), user_id, StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordLengthAfterHeader == record_id))
                    {
                        if (this.Header.Vlrs[i].RecordLengthAfterHeader > 0)
                        {
                            this.Header.OffsetToPointData -= (UInt32)(54 + this.Header.Vlrs[i].RecordLengthAfterHeader);
                        }

                        this.Header.NumberOfVariableLengthRecords--;
                        this.Header.Vlrs.RemoveAt(i);
                        return 0;
                    }
                }
                throw new InvalidOperationException("cannot find VLR with user_id '" + user_id + "' and record_id " + record_id + " among the " + this.Header.NumberOfVariableLengthRecords + " VLRs in the header");
            }
            else
            {
                throw new InvalidOperationException("cannot remove VLR with user_id '" + user_id + "' and record_id " + record_id + " because header has no VLRs");
            }
        }

        public int RequestNativeExtension(bool request)
        {
            if (reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.request_native_extension = request;

            if (request) // only one should be on
            {
                this.request_compatibility_mode = false;
            }

            return 0;
        }

        public int RequestCompatibilityMode(bool request)
        {
            if (reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.request_compatibility_mode = request;
            if (request) // only one should be on
            {
                this.request_native_extension = false;
            }

            return 0;
        }

        public int SetChunkSize(UInt32 chunk_size)
        {
            if (reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.set_chunk_size = chunk_size;

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

            if (this.Header.Vlrs.Count > 0)
            {
                // overwrite existing VLR ?
                int i;
                for (i = (int)this.Header.NumberOfVariableLengthRecords - 1; i >= 0; i--)
                {
                    if ((this.Header.Vlrs[i].RecordID == vlr.RecordID) && (LasZipDll.ArrayCompare(Header.Vlrs[i].UserID, vlr.UserID) == false))
                    {
                        if (this.Header.Vlrs[i].RecordLengthAfterHeader != 0)
                        {
                            this.Header.OffsetToPointData -= Header.Vlrs[i].RecordLengthAfterHeader;
                        }

                        this.Header.Vlrs.RemoveAt(i);
                        break;
                    }
                }

                // create new VLR
                if (i == this.Header.NumberOfVariableLengthRecords)
                {
                    this.Header.NumberOfVariableLengthRecords++;
                    this.Header.OffsetToPointData += 54;
                    this.Header.Vlrs.Add(vlr);
                }
            }
            else
            {
                this.Header.Vlrs.Add(vlr);
                this.Header.NumberOfVariableLengthRecords = (UInt32)this.Header.Vlrs.Count;
                this.Header.OffsetToPointData += 54;
            }

            // copy the VLR
            this.Header.OffsetToPointData += vlr.RecordLengthAfterHeader;

            return 0;
        }

        private static void CheckHeaderAndSetup(LasHeader header, bool compress, out LasZip lasZip, LasPoint point, out UInt32 laszipVlrPayloadSize)
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

        public void OpenReader(string filePath, out bool isCompressed)
        {
            if (String.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentOutOfRangeException(nameof(filePath), "File path is empty or whitespace.");
            }
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            // open the file
            this.streamIn = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024);
            this.leaveStreamInOpen = false;

            this.ReadHeader(out isCompressed);

            // should we try to exploit existing spatial indexing information
            if (this.LaxExploit)
            {
                this.laxIndex = new();
                this.laxIndex.Read(filePath);
            }
        }

        private int CreateSpatialIndex(bool create, bool append)
        {
            if (append)
            {
                throw new NotImplementedException("appending of spatial index not (yet) supported in this version");
            }
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.LaxCreate = create;
            this.LaxAppend = append;

            return 0;
        }

        private int PrepareHeaderForWrite()
        {
            if ((this.Header.VersionMajor != 1) || (this.Header.VersionMinor > 4))
            {
                throw new NotSupportedException("unknown LAS version " + this.Header.VersionMajor + "." + this.Header.VersionMinor);
            }

            // check counters
            UInt32 i;

            if (this.Header.PointDataFormat > 5)
            {
                // legacy counters are zero for new point types
                this.Header.NumberOfPointRecords = 0;
                for (i = 0; i < 5; i++)
                {
                    this.Header.NumberOfPointsByReturn[i] = 0;
                }
            }
            else if (this.Header.VersionMinor > 3)
            {
                // legacy counters must be zero or consistent for old point types
                if (this.Header.NumberOfPointRecords != this.Header.ExtendedNumberOfPointRecords)
                {
                    if (this.Header.NumberOfPointRecords != 0)
                    {
                        throw new InvalidOperationException("inconsistent number_of_point_records " + this.Header.NumberOfPointRecords + " and extended_number_of_point_records " + this.Header.ExtendedNumberOfPointRecords);
                    }
                    else if (this.Header.ExtendedNumberOfPointRecords <= UInt32.MaxValue)
                    {
                        this.Header.NumberOfPointRecords = (UInt32)this.Header.ExtendedNumberOfPointRecords;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                for (i = 0; i < 5; i++)
                {
                    if (this.Header.NumberOfPointsByReturn[i] != this.Header.ExtendedNumberOfPointsByReturn[i])
                    {
                        if (this.Header.NumberOfPointsByReturn[i] != 0)
                        {
                            throw new InvalidOperationException("inconsistent number_of_points_by_return[" + i + "] " + this.Header.NumberOfPointsByReturn[i] + " and extended_number_of_points_by_return[" + i + "] " + this.Header.ExtendedNumberOfPointsByReturn[i]);
                        }
                        else if (this.Header.ExtendedNumberOfPointsByReturn[i] <= UInt32.MaxValue)
                        {
                            this.Header.NumberOfPointsByReturn[i] = (UInt32)this.Header.ExtendedNumberOfPointsByReturn[i];
                        }
                    }
                }
            }

            return 0;
        }

        private int PreparePointForWrite(bool compress)
        {
            if (this.Header.PointDataFormat > 5)
            {
                // must be set for the new point types 6 or higher ...
                this.Point.ExtendedPointType = 1;

                if (this.request_native_extension)
                {
                    // we are *not* operating in compatibility mode
                    this.compatibility_mode = false;
                }
                else if (this.request_compatibility_mode)
                {
                    // we are *not* using the native extension
                    this.request_native_extension = false;

                    // make sure there are no more than UInt32.MaxValue points
                    if (this.Header.ExtendedNumberOfPointRecords > UInt32.MaxValue)
                    {
                        throw new InvalidOperationException("extended_number_of_point_records of " + this.Header.ExtendedNumberOfPointRecords + " is too much for 32-bit counters of compatibility mode");
                    }

                    // copy 64-bit extended counters back into 32-bit legacy counters
                    this.Header.NumberOfPointRecords = (UInt32)(this.Header.ExtendedNumberOfPointRecords);
                    for (int i = 0; i < 5; i++)
                    {
                        this.Header.NumberOfPointsByReturn[i] = (UInt32)(this.Header.ExtendedNumberOfPointsByReturn[i]);
                    }

                    // are there any "extra bytes" already ... ?
                    Int32 number_of_existing_extrabytes = 0;
                    switch (this.Header.PointDataFormat)
                    {
                        case 6:
                            number_of_existing_extrabytes = this.Header.PointDataRecordLength - 30;
                            break;
                        case 7:
                            number_of_existing_extrabytes = this.Header.PointDataRecordLength - 36;
                            break;
                        case 8:
                            number_of_existing_extrabytes = this.Header.PointDataRecordLength - 38;
                            break;
                        case 9:
                            number_of_existing_extrabytes = this.Header.PointDataRecordLength - 59;
                            break;
                        case 10:
                            number_of_existing_extrabytes = this.Header.PointDataRecordLength - 67;
                            break;
                        default:
                            throw new NotSupportedException("unknown point_data_format " + this.Header.PointDataFormat);
                    }

                    if (number_of_existing_extrabytes < 0)
                    {
                        throw new InvalidOperationException("bad point_data_format " + this.Header.PointDataFormat + " point_data_record_length " + this.Header.PointDataRecordLength + " combination");
                    }

                    // downgrade to LAS 1.2 or LAS 1.3
                    if (this.Header.PointDataFormat <= 8)
                    {
                        this.Header.VersionMinor = 2;
                        // LAS 1.2 header is 148 bytes less than LAS 1.4+ header
                        this.Header.HeaderSize -= 148;
                        this.Header.OffsetToPointData -= 148;
                    }
                    else
                    {
                        this.Header.VersionMinor = 3;
                        // LAS 1.3 header is 140 bytes less than LAS 1.4+ header
                        this.Header.HeaderSize -= 140;
                        this.Header.OffsetToPointData -= 140;
                    }
                    // turn off the bit indicating the presence of the OGC WKT
                    this.Header.GlobalEncoding &= 0xffef;

                    // old point type is two bytes shorter
                    this.Header.PointDataRecordLength -= 2;
                    // but we add 5 bytes of attributes
                    this.Header.PointDataRecordLength += 5;

                    // create 2+2+4+148 bytes payload for compatibility VLR
                    Stream outStream = new MemoryStream(new byte[this.Header.PointDataRecordLength]);

                    // write control info
                    UInt16 laszip_version = (UInt16)LasZip.Version.Build;
                    outStream.WriteLittleEndian(laszip_version);
                    UInt16 compatible_version = 3;
                    outStream.WriteLittleEndian(compatible_version);
                    UInt32 unused = 0;
                    outStream.WriteLittleEndian(unused);
                    // write the 148 bytes of the extended LAS 1.4 header
                    UInt64 start_of_waveform_data_packet_record = this.Header.StartOfWaveformDataPacketRecord;
                    if (start_of_waveform_data_packet_record != 0)
                    {
                        throw new NotSupportedException("header->start_of_waveform_data_packet_record is " + start_of_waveform_data_packet_record + ".");
                    }
                    outStream.WriteLittleEndian(start_of_waveform_data_packet_record);
                    UInt64 start_of_first_extended_variable_length_record = this.Header.StartOfFirstExtendedVariableLengthRecord;
                    if (start_of_first_extended_variable_length_record != 0)
                    {
                        throw new NotSupportedException("EVLRs not supported. header->start_of_first_extended_variable_length_record is " + start_of_first_extended_variable_length_record + ".");
                    }
                    outStream.WriteLittleEndian(start_of_first_extended_variable_length_record);
                    UInt32 number_of_extended_variable_length_records = this.Header.NumberOfExtendedVariableLengthRecords;
                    if (number_of_extended_variable_length_records != 0)
                    {
                        throw new NotSupportedException("EVLRs not supported. header->number_of_extended_variable_length_records is " + number_of_extended_variable_length_records + "..");
                    }
                    outStream.WriteLittleEndian(number_of_extended_variable_length_records);
                    UInt64 extended_number_of_point_records;
                    if (this.Header.NumberOfPointRecords != 0)
                        extended_number_of_point_records = this.Header.NumberOfPointRecords;
                    else
                        extended_number_of_point_records = this.Header.ExtendedNumberOfPointRecords;
                    outStream.WriteLittleEndian(extended_number_of_point_records);
                    UInt64 extended_number_of_points_by_return;
                    for (int i = 0; i < 15; i++)
                    {
                        if ((i < 5) && (this.Header.NumberOfPointsByReturn[i] != 0))
                            extended_number_of_points_by_return = this.Header.NumberOfPointsByReturn[i];
                        else
                            extended_number_of_points_by_return = this.Header.ExtendedNumberOfPointsByReturn[i];
                        outStream.WriteLittleEndian(extended_number_of_points_by_return);
                    }

                    // add the compatibility VLR
                    UInt16 lasVlrRecordLengthAfterHeader = 2 + 2 + 4 + 148;
                    byte[] lasVlrData = new byte[lasVlrRecordLengthAfterHeader];
                    outStream.ReadExactly(lasVlrData, 0, lasVlrData.Length);
                    if (this.AddVlr("lascompatible\0\0", 22204, lasVlrRecordLengthAfterHeader, null, lasVlrData) != 0)
                    {
                        throw new InvalidOperationException("adding the compatibility VLR");
                    }

                    // if needed create an attributer to describe the "extra bytes"
                    if (this.attributer == null)
                    {
                        this.attributer = new();
                    }

                    // were there any pre-existing extra bytes
                    if (number_of_existing_extrabytes > 0)
                    {
                        // make sure the existing "extra bytes" are documented
                        if (this.attributer.get_attributes_size() > number_of_existing_extrabytes)
                        {
                            throw new InvalidOperationException("bad \"extra bytes\" VLR describes " + (this.attributer.get_attributes_size() - number_of_existing_extrabytes) + " bytes more than points actually have");
                        }
                        else if (this.attributer.get_attributes_size() < number_of_existing_extrabytes)
                        {
                            // maybe the existing "extra bytes" are documented in a VLR
                            for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                            {
                                if (String.Equals(this.Header.Vlrs[i].UserID, "LASF_Spec") && (this.Header.Vlrs[i].RecordID == 4))
                                {
                                    this.attributer.init_attributes((UInt32)(this.Header.Vlrs[i].RecordLengthAfterHeader / LasAttribute.SerializedSizeInBytes), this.Header.Vlrs[i].Data);
                                }
                            }

                            // describe any undocumented "extra bytes" as "unknown" byte  attributes
                            for (int i = (Int32)(this.attributer.get_attributes_size()); i < number_of_existing_extrabytes; i++)
                            {
                                string unknown_name = "unknown " + i;
                                LasAttribute lasattribute_unknown = new(LasAttribute.LAS_ATTRIBUTE_U8, unknown_name, unknown_name);
                                if (this.attributer.add_attribute(lasattribute_unknown) == -1)
                                {
                                    throw new InvalidOperationException("cannot add unknown byte attribute '" + unknown_name + "' of " + number_of_existing_extrabytes + " to attributer");
                                }
                            }
                        }
                    }

                    // create the "extra bytes" that store the newer LAS 1.4 point attributes

                    // scan_angle (difference or remainder) is stored as a Int16
                    LasAttribute lasattribute_scan_angle = new(LasAttribute.LAS_ATTRIBUTE_I16, "LAS 1.4 scan angle", "additional attributes");
                    lasattribute_scan_angle.set_scale(0.006);
                    Int32 index_scan_angle = this.attributer.add_attribute(lasattribute_scan_angle);
                    this.start_scan_angle = this.attributer.get_attribute_start(index_scan_angle);
                    // extended returns stored as a byte
                    LasAttribute lasattribute_extended_returns = new(LasAttribute.LAS_ATTRIBUTE_U8, "LAS 1.4 extended returns", "additional attributes");
                    Int32 index_extended_returns = this.attributer.add_attribute(lasattribute_extended_returns);
                    this.start_extended_returns = this.attributer.get_attribute_start(index_extended_returns);
                    // classification stored as a byte
                    LasAttribute lasattribute_classification = new(LasAttribute.LAS_ATTRIBUTE_U8, "LAS 1.4 classification", "additional attributes");
                    Int32 index_classification = this.attributer.add_attribute(lasattribute_classification);
                    this.start_classification = this.attributer.get_attribute_start(index_classification);
                    // flags and channel stored as a byte
                    LasAttribute lasattribute_flags_and_channel = new(LasAttribute.LAS_ATTRIBUTE_U8, "LAS 1.4 flags and channel", "additional attributes");
                    Int32 index_flags_and_channel = this.attributer.add_attribute(lasattribute_flags_and_channel);
                    this.start_flags_and_channel = this.attributer.get_attribute_start(index_flags_and_channel);
                    // maybe store the NIR band as a UInt16
                    if (this.Header.PointDataFormat == 8 || this.Header.PointDataFormat == 10)
                    {
                        // the NIR band is stored as a UInt16
                        LasAttribute lasattribute_NIR_band = new(LasAttribute.LAS_ATTRIBUTE_U16, "LAS 1.4 NIR band", "additional attributes");
                        Int32 index_NIR_band = this.attributer.add_attribute(lasattribute_NIR_band);
                        this.start_NIR_band = this.attributer.get_attribute_start(index_NIR_band);
                    }
                    else
                    {
                        this.start_NIR_band = -1;
                    }

                    // add the extra bytes VLR with the additional attributes
                    byte[] vlrData = this.attributer.get_bytes();
                    if (this.AddVlr("LASF_Spec\0\0\0\0\0\0", 4, (UInt16)(this.attributer.number_attributes * LasAttribute.SerializedSizeInBytes), null, vlrData) != 0)
                    {
                        throw new InvalidOperationException("adding the extra bytes VLR with the additional attributes");
                    }

                    // update point type
                    if (this.Header.PointDataFormat == 6)
                    {
                        this.Header.PointDataFormat = 1;
                    }
                    else if (this.Header.PointDataFormat <= 8)
                    {
                        this.Header.PointDataFormat = 3;
                    }
                    else // 9->4 and 10->5
                    {
                        this.Header.PointDataFormat -= 5;
                    }

                    // we are operating in compatibility mode
                    this.compatibility_mode = true;
                }
                else if (compress)
                {
                    throw new InvalidOperationException(LasZip.GetAssemblyVersionString() + ": cannot compress point data format " + this.Header.PointDataFormat + " without requesting 'compatibility mode'");
                }
            }
            else
            {
                // must *not* be set for the old point type 5 or lower
                this.Point.ExtendedPointType = 0;

                // we are *not* operating in compatibility mode
                this.compatibility_mode = false;
            }

            return 0;
        }

        private int PrepareVlrsForWrite()
        {
            if (this.Header.Vlrs.Count != this.Header.NumberOfVariableLengthRecords)
            {
                throw new InvalidOperationException(nameof(this.Header.NumberOfVariableLengthRecords) + " is " + this.Header.NumberOfVariableLengthRecords + " but " + this.Header.Vlrs.Count + " VLRs are present.");
            }

            int vlrs_size = 0;
            if (this.Header.NumberOfVariableLengthRecords != 0)
            {
                for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                {
                    vlrs_size += 54;
                    if (this.Header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        vlrs_size += this.Header.Vlrs[i].RecordLengthAfterHeader;
                    }
                }
            }

            if ((vlrs_size + this.Header.HeaderSize + this.Header.UserDataAfterHeaderSize) != this.Header.OffsetToPointData)
            {
                throw new InvalidOperationException("header_size (" + this.Header.HeaderSize + ") plus vlrs_size (" + vlrs_size + ") plus user_data_after_header_size (" + this.Header.UserDataAfterHeaderSize + ") does not equal offset_to_point_data (" + this.Header.OffsetToPointData + ")");
            }

            return 0;
        }

        private static UInt32 GetVlrPayloadSize(LasZip lasZip)
        {
            return 34U + (6U * lasZip.NumItems);
        }

        private int WriteLasZipVlrHeader(LasZip lasZip, Stream outStream)
        {
            // write the LasZip VLR header
            UInt16 reserved = 0x0;
            outStream.WriteLittleEndian(reserved);
            Span<byte> userIDBytes = stackalloc byte[16];
            Encoding.UTF8.GetBytes("laszip encoded", userIDBytes);
            outStream.Write(userIDBytes);
            UInt16 record_id = 22204;
            outStream.WriteLittleEndian(record_id);
            UInt16 record_length_after_header = (UInt16)LasZipDll.GetVlrPayloadSize(lasZip);
            outStream.WriteLittleEndian(record_length_after_header);
            Span<byte> versionStringBytes = stackalloc byte[32];
            Encoding.UTF8.GetBytes(LasZip.GetAssemblyVersionString());
            outStream.Write(versionStringBytes);

            return 0;
        }

        private static int WriteLasZipVlrPayload(LasZip lasZip, Stream outStream)
        {
            // write the LasZip VLR payload
            //     UInt16  compressor                2 bytes
            //     UInt32  coder                     2 bytes
            //     byte   version_major             1 byte
            //     byte   version_minor             1 byte
            //     UInt16  version_revision          2 bytes
            //     UInt32  options                   4 bytes
            //     Int32  chunk_size                4 bytes
            //     I64  number_of_special_evlrs   8 bytes
            //     I64  offset_to_special_evlrs   8 bytes
            //     UInt16  num_items                 2 bytes
            //        UInt16 type                2 bytes * num_items
            //        UInt16 size                2 bytes * num_items
            //        UInt16 version             2 bytes * num_items
            // which totals 34+6*num_items
            outStream.WriteLittleEndian(lasZip.Compressor);
            outStream.WriteLittleEndian(lasZip.Coder);
            outStream.WriteByte(lasZip.VersionMajor);
            outStream.WriteByte(lasZip.VersionMinor);
            outStream.WriteLittleEndian(lasZip.VersionRevision);
            outStream.WriteLittleEndian(lasZip.Options);
            outStream.WriteLittleEndian(lasZip.ChunkSize);
            outStream.WriteLittleEndian(lasZip.NumberOfSpecialEvlrs);
            outStream.WriteLittleEndian(lasZip.OffsetToSpecialEvlrs);
            outStream.WriteLittleEndian(lasZip.NumItems);

            for (int j = 0; j < lasZip.NumItems; j++)
            {
                UInt16 type = (UInt16)lasZip.Items[j].Type;
                outStream.WriteLittleEndian(type);
                outStream.WriteLittleEndian(lasZip.Items[j].Size);
                outStream.WriteLittleEndian(lasZip.Items[j].Version);
            }

            return 0;
        }

        private int WriteHeader(LasZip lasZip, bool compress)
        {
            this.streamOut.Write(Encoding.UTF8.GetBytes("LASF"));
            this.streamOut.WriteLittleEndian((int)this.Header.FileSourceID);
            this.streamOut.WriteLittleEndian((ulong)this.Header.GlobalEncoding);
            this.streamOut.WriteLittleEndian(this.Header.ProjectIDGuidData1);
            this.streamOut.WriteLittleEndian(this.Header.ProjectIDGuidData2);
            this.streamOut.WriteLittleEndian(this.Header.ProjectIDGuidData3);
            this.streamOut.Write(this.Header.ProjectIDGuidData4);
            this.streamOut.WriteByte(this.Header.VersionMajor);
            this.streamOut.WriteByte(this.Header.VersionMinor);
            this.streamOut.Write(this.Header.SystemIdentifier);
            if (this.PreserveGeneratingSoftware == false)
            {
                int bytesEncoded = Encoding.UTF8.GetBytes(LasZip.GetAssemblyVersionString(), this.Header.GeneratingSoftware);
                if (bytesEncoded < this.Header.GeneratingSoftware.Length)
                {
                    Array.Clear(this.Header.GeneratingSoftware, bytesEncoded, this.Header.GeneratingSoftware.Length - bytesEncoded);
                }
            }
            this.streamOut.Write(this.Header.GeneratingSoftware, 0, 32);
            this.streamOut.WriteLittleEndian(this.Header.FileCreationDay);
            this.streamOut.WriteLittleEndian(this.Header.FileCreationYear);
            this.streamOut.WriteLittleEndian(this.Header.HeaderSize);
            if (compress)
            {
                this.Header.OffsetToPointData += 54 + LasZipDll.GetVlrPayloadSize(lasZip);
            }
            this.streamOut.WriteLittleEndian(this.Header.OffsetToPointData);
            if (compress)
            {
                this.Header.OffsetToPointData -= 54 + LasZipDll.GetVlrPayloadSize(lasZip);
                this.Header.NumberOfVariableLengthRecords += 1;
            }
            this.streamOut.WriteLittleEndian(this.Header.NumberOfVariableLengthRecords);
            if (compress)
            {
                this.Header.NumberOfVariableLengthRecords -= 1;
                this.Header.PointDataFormat |= 128;
            }
            this.streamOut.WriteByte(this.Header.PointDataFormat);
            if (compress)
            {
                this.Header.PointDataFormat &= 127;
            }
            this.streamOut.WriteLittleEndian(this.Header.PointDataRecordLength);
            this.streamOut.WriteLittleEndian(this.Header.NumberOfPointRecords);
            for (int i = 0; i < 5; i++)
            {
                this.streamOut.WriteLittleEndian(this.Header.NumberOfPointsByReturn[i]);
            }
            this.streamOut.WriteLittleEndian(this.Header.XScaleFactor);
            this.streamOut.WriteLittleEndian(this.Header.YScaleFactor);
            this.streamOut.WriteLittleEndian(this.Header.ZScaleFactor);
            this.streamOut.WriteLittleEndian(this.Header.XOffset);
            this.streamOut.WriteLittleEndian(this.Header.YOffset);
            this.streamOut.WriteLittleEndian(this.Header.ZOffset);
            this.streamOut.WriteLittleEndian(this.Header.MaxX);
            this.streamOut.WriteLittleEndian(this.Header.MinX);
            this.streamOut.WriteLittleEndian(this.Header.MaxY);
            this.streamOut.WriteLittleEndian(this.Header.MinY);
            this.streamOut.WriteLittleEndian(this.Header.MaxZ);
            this.streamOut.WriteLittleEndian(this.Header.MinZ);

            // special handling for LAS 1.3
            if ((this.Header.VersionMajor == 1) && (this.Header.VersionMinor >= 3))
            {
                if (this.Header.HeaderSize < 235)
                {
                    throw new InvalidOperationException("For LAS 1." + this.Header.VersionMinor + " header_size should at least be 235 bytes but it is only " + this.Header.HeaderSize + ".");
                }
                else
                {
                    if (this.Header.StartOfWaveformDataPacketRecord != 0)
                    {
                        throw new NotSupportedException("header.start_of_waveform_data_packet_record is " + this.Header.StartOfWaveformDataPacketRecord);
                    }
                    this.streamOut.WriteLittleEndian(this.Header.StartOfWaveformDataPacketRecord);
                    this.Header.UserDataInHeaderSize = (UInt32)(this.Header.HeaderSize - 235);
                }
            }
            else
            {
                this.Header.UserDataInHeaderSize = (UInt32)(this.Header.HeaderSize - 227);
            }

            // special handling for LAS 1.4
            if ((this.Header.VersionMajor == 1) && (this.Header.VersionMinor >= 4))
            {
                if (this.Header.HeaderSize < 375)
                {
                    throw new InvalidOperationException("For LAS 1." + this.Header.VersionMinor + " header_size should at least be 375 bytes but it is only " + this.Header.HeaderSize + ".");
                }
                else
                {
                    this.streamOut.WriteLittleEndian(this.Header.StartOfFirstExtendedVariableLengthRecord);
                    this.streamOut.WriteLittleEndian(this.Header.NumberOfExtendedVariableLengthRecords);
                    this.streamOut.WriteLittleEndian(this.Header.ExtendedNumberOfPointRecords);
                    for (int i = 0; i < 15; i++)
                    {
                        this.streamOut.WriteLittleEndian(this.Header.ExtendedNumberOfPointsByReturn[i]);
                    }
                }
                this.Header.UserDataInHeaderSize = (UInt32)(this.Header.HeaderSize - 375);
            }

            // write any number of user-defined bytes that might have been added to the header
            if (this.Header.UserDataInHeaderSize != 0)
            {
                this.streamOut.Write(this.Header.UserDataInHeader, 0, (int)this.Header.UserDataInHeaderSize);
            }

            // write variable length records into the header
            if (this.Header.NumberOfVariableLengthRecords != 0)
            {
                for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                {
                    // write variable length records variable after variable (to avoid alignment issues)
                    this.streamOut.WriteLittleEndian(this.Header.Vlrs[i].Reserved);
                    this.streamOut.Write(this.Header.Vlrs[i].UserID, 0, 16);
                    this.streamOut.WriteLittleEndian(this.Header.Vlrs[i].RecordID);
                    this.streamOut.WriteLittleEndian(this.Header.Vlrs[i].RecordLengthAfterHeader);
                    this.streamOut.Write(this.Header.Vlrs[i].Description, 0, 32);

                    // write data following the header of the variable length record
                    if (this.Header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        this.streamOut.Write(this.Header.Vlrs[i].Data, 0, this.Header.Vlrs[i].RecordLengthAfterHeader);
                    }
                }
            }

            if (compress)
            {
                // write the LasZip VLR header
                if (this.WriteLasZipVlrHeader(lasZip, this.streamOut) != 0)
                {
                    return 1;
                }

                // write the LasZip VLR payload
                if (LasZipDll.WriteLasZipVlrPayload(lasZip, this.streamOut) != 0)
                {
                    return 1;
                }
            }

            // write any number of user-defined bytes that might have been added after the header
            if (this.Header.UserDataAfterHeaderSize != 0)
            {
                this.streamOut.Write(this.Header.UserDataAfterHeader, 0, (int)this.Header.UserDataAfterHeaderSize);
            }

            return 0;
        }

        private int CreatePointWriter(LasZip laszip)
        {
            // create the point writer
            this.writer = new();

            if (this.writer.Setup(laszip) == false)
            {
                throw new InvalidOperationException("setup of LASwritePoint failed");
            }

            if (this.writer.Init(this.streamOut) == false)
            {
                throw new InvalidOperationException("init of LASwritePoint failed");
            }

            return 0;
        }

        private int SetupLasZipItems(LasZip lasZip, bool compress)
        {
            byte point_type = this.Header.PointDataFormat;
            UInt16 point_size = this.Header.PointDataRecordLength;

            int gpsTimeOffset = 20; // bytes
            if (point_type > 5)
            {
                if (this.request_compatibility_mode && (lasZip.RequestCompatibilityMode(1) == false))
                {
                    throw new InvalidOperationException("requesting 'compatibility mode' has failed");
                }

                gpsTimeOffset = 22;
            }

            // create point items in the LasZip structure from point format and size
            if (lasZip.Setup(point_type, point_size, LasZip.CompressorNone) == false)
            {
                throw new InvalidOperationException("invalid combination of point_type " + point_type + " and point_size " + point_size);
            }

            // compute offsets (or points item pointers) for data transfer from the point items
            this.point_items = new byte[point_size];
            for (int item = 0; item < lasZip.NumItems; item++)
            {
                switch (lasZip.Items[item].Type)
                {
                    case LasItemType.Byte:
                    case LasItemType.Byte14:
                        this.Point.NumExtraBytes = lasZip.Items[item].Size;
                        this.Point.ExtraBytes = new byte[this.Point.NumExtraBytes];
                        break;
                    case LasItemType.Point10:
                    case LasItemType.Point14:
                    case LasItemType.Gpstime11:
                    case LasItemType.Rgb12:
                    case LasItemType.Rgb14:
                    case LasItemType.RgbNir14:
                    case LasItemType.Wavepacket13:
                    case LasItemType.Wavepacket14:
                        // nothing to do since built into point
                        break;
                    default:
                        throw new NotSupportedException("unknown LASitem type " + lasZip.Items[item].Type);
                }
            }

            if (compress)
            {
                if ((point_type > 5) && this.request_native_extension)
                {
                    if (!lasZip.Setup(point_type, point_size, LasZip.CompressorLayeredChunked))
                    {
                        throw new InvalidOperationException("cannot compress point_type " + point_type + " with point_size " + point_size + " using native");
                    }
                }
                else
                {
                    if (!lasZip.Setup(point_type, point_size, LasZip.CompressorDefault))
                    {
                        throw new InvalidOperationException("cannot compress point_type " + point_type + " with point_size " + point_size);
                    }
                }

                // request version (old point types only, new point types always use version 3)
                lasZip.RequestVersion(2);

                // maybe we should change the chunk size
                if (this.set_chunk_size != LasZip.ChunkSizeDefault)
                {
                    if (!lasZip.SetChunkSize(this.set_chunk_size))
                    {
                        throw new InvalidOperationException("setting chunk size " + this.set_chunk_size + " has failed");
                    }
                }
            }
            else
            {
                lasZip.RequestVersion(0);
            }
            return 0;
        }

        public void OpenWriter(string file_name, bool compress)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            // create the outstream
            this.streamOut = new FileStream(file_name, FileMode.Create, FileAccess.Write, FileShare.None, 256 * 1024);

            // setup the items that make up the point
            LasZip laszip = new();
            if (this.SetupLasZipItems(laszip, compress) != 0)
            {
                throw new InvalidOperationException(nameof(this.SetupLasZipItems) + "() failed.");
            }

            // prepare header

            if (this.PrepareHeaderForWrite() != 0)
            {
                throw new InvalidOperationException(nameof(this.PrepareHeaderForWrite) + "() failed.");
            }

            // prepare point
            if (this.PreparePointForWrite(compress) != 0)
            {
                throw new InvalidOperationException(nameof(this.PreparePointForWrite) + "() failed.");
            }

            // prepare VLRs
            if (this.PrepareVlrsForWrite() != 0)
            {
                throw new InvalidOperationException(nameof(this.PrepareVlrsForWrite) + "() failed.");
            }

            // write header variable after variable
            if (this.WriteHeader(laszip, compress) != 0)
            {
                throw new InvalidOperationException(nameof(this.WriteHeader) + "() failed.");
            }

            // create the point writer
            if (this.CreatePointWriter(laszip) != 0)
            {
                throw new InvalidOperationException(nameof(this.CreatePointWriter) + "() failed.");
            }

            if (this.LaxCreate)
            {
                // create spatial indexing information using cell_size = 100.0F and threshold = 1000
                LasQuadTree lasquadtree = new();
                lasquadtree.Setup(this.Header.MinX, this.Header.MaxX, this.Header.MinY, this.Header.MaxY, 100.0F);

                this.laxIndex = new();
                this.laxIndex.Prepare(lasquadtree, 1000);

                // copy the file name for later
                this.laxFileName = file_name;
            }

            // set the point number and point count
            this.nPoints = (Int64)(this.Header.NumberOfPointRecords != 0 ? this.Header.NumberOfPointRecords : this.Header.ExtendedNumberOfPointRecords);
            this.currentPointIndex = 0;
        }

        //public void WritePoint()
        //{
        //    if (writer == null)
        //    {
        //        throw new InvalidOperationException("writing points before writer was opened");
        //    }

        //    // write the point
        //    if (writer.Write(this.Point) == false)
        //    {
        //        throw new InvalidOperationException(String.Format("writing point with index {0} of {1} total points", currentPointIndex, nPoints));
        //    }

        //    currentPointIndex++;
        //}

        public void WritePoint()
        {
            // temporary fix to avoid corrupt LAZ files
            if (this.Point.ExtendedPointType != 0)
            {
                // make sure legacy flags and extended flags are identical
                if ((this.Point.ExtendedClassificationFlags & 0x7) != (this.Point.ClassificationAndFlags >> 5))
                {
                    throw new InvalidOperationException("legacy flags and extended flags are not identical");
                }

                // make sure legacy classification is zero or identical to extended classification
                if (this.Point.ClassificationAndFlags != 0)
                {
                    if (this.Point.ClassificationAndFlags != this.Point.ExtendedClassification)
                    {
                        throw new InvalidOperationException("legacy classification " + this.Point.ClassificationAndFlags + " and extended classification " + this.Point.ExtendedClassification + " are not consistent");
                    }
                }
            }

            // special recoding of points (in compatibility mode only)
            if (this.compatibility_mode)
            {
                Int32 scan_angle_remainder;
                Int32 number_of_returns_increment;
                Int32 return_number_increment;
                Int32 return_count_difference;
                Int32 overlap_bit;
                Int32 scanner_channel;

                // distill extended attributes
                LasPoint point = this.Point;
                point.ScanAngleRank = MyDefs.ClampInt8(MyDefs.QuantizeInt16(0.006F * point.ExtendedScanAngle));
                scan_angle_remainder = point.ExtendedScanAngle - MyDefs.QuantizeInt16(((float)point.ScanAngleRank) / 0.006f);
                if (point.ExtendedNumberOfReturnsOfGivenPulse <= 7)
                {
                    point.NumberOfReturnsOfGivenPulse = point.ExtendedNumberOfReturnsOfGivenPulse;
                    if (point.ExtendedReturnNumber <= 7)
                    {
                        point.ReturnNumber = point.ExtendedReturnNumber;
                    }
                    else
                    {
                        point.ReturnNumber = 7;
                    }
                }
                else
                {
                    point.NumberOfReturnsOfGivenPulse = 7;
                    if (point.ExtendedNumberOfReturnsOfGivenPulse <= 4)
                    {
                        point.ReturnNumber = point.ExtendedNumberOfReturnsOfGivenPulse;
                    }
                    else
                    {
                        return_count_difference = point.ExtendedReturnNumber - point.ExtendedNumberOfReturnsOfGivenPulse;
                        if (return_count_difference <= 0)
                        {
                            point.ReturnNumber = 7;
                        }
                        else if (return_count_difference >= 3)
                        {
                            point.ReturnNumber = 4;
                        }
                        else
                        {
                            point.ReturnNumber = (byte)(7 - return_count_difference);
                        }
                    }
                }
                return_number_increment = point.ExtendedNumberOfReturnsOfGivenPulse - point.ReturnNumber;
                number_of_returns_increment = point.ExtendedReturnNumber - point.NumberOfReturnsOfGivenPulse;
                if (point.ExtendedClassification > 31)
                {
                    point.ClassificationAndFlags = 0;
                }
                else
                {
                    point.ExtendedClassification = 0;
                }
                scanner_channel = point.ExtendedScannerChannel;
                overlap_bit = point.ExtendedClassificationFlags >> 3;

                // write distilled extended attributes into extra bytes
                BitConverter.TryWriteBytes(point.ExtraBytes[this.start_scan_angle..], (Int16)scan_angle_remainder);
                point.ExtraBytes[this.start_extended_returns] = (byte)((return_number_increment << 4) | number_of_returns_increment);
                point.ExtraBytes[this.start_classification] = point.ExtendedClassification;
                point.ExtraBytes[this.start_flags_and_channel] = (byte)((scanner_channel << 1) | overlap_bit);
                if (this.start_NIR_band != -1)
                {
                    BitConverter.TryWriteBytes(point.ExtraBytes[this.start_NIR_band..], point.Rgb[3]);
                }
            }

            // write the point
            if (this.writer.Write(this.point_items) == false)
            {
                throw new InvalidOperationException("writing point " + this.currentPointIndex + " of " + this.nPoints + " total points");
            }

            ++this.currentPointIndex;
        }

        private int WriteIndexedPoint()
        {
            // write the point
            if (this.writer.Write(this.point_items) == false)
            {
                throw new InvalidOperationException("writing point " + this.currentPointIndex + " of " + this.nPoints + " total points");
            }
            // index the point
            double x = this.Header.XScaleFactor * this.Point.X + this.Header.XOffset;
            double y = this.Header.YScaleFactor * this.Point.Y + this.Header.YOffset;
            this.laxIndex.Add(x, y, (UInt32)this.currentPointIndex);
            this.currentPointIndex++;
            return 0;
        }

        public int UpdateInventory()
        {
            this.inventory ??= new();
            this.inventory.Add(this.Point);
            return 0;
        }

        public int CloseWriter()
        {
            if (this.writer == null)
            {
                throw new InvalidOperationException("closing writer before it was opened");
            }

            if (this.writer.Done() == false)
            {
                throw new InvalidOperationException("done of LASwritePoint failed");
            }

            this.writer = null;
            this.point_items = Array.Empty<byte>();

            // maybe update the header
            if (this.inventory != null)
            {
                if (this.Header.PointDataFormat <= 5) // only update legacy counters for old point types
                {
                    this.streamOut.Seek(107, SeekOrigin.Begin);

                    // Because number of point records is now UInt64, this function only works with little endian machines
                    this.streamOut.WriteLittleEndian(this.inventory.NumberOfPointRecords);
                    for (Int32 i = 0; i < 5; i++)
                    {
                        this.streamOut.WriteLittleEndian(this.inventory.NumberOfPointsByReturn[i + 1]);
                    }
                }
                this.streamOut.Seek(179, SeekOrigin.Begin);
                double value = this.Header.XScaleFactor * this.inventory.MaxX + this.Header.XOffset;
                this.streamOut.WriteLittleEndian(value);
                value = this.Header.XScaleFactor * this.inventory.MinX + this.Header.XOffset;
                this.streamOut.WriteLittleEndian(value);
                value = this.Header.YScaleFactor * this.inventory.MaxY + this.Header.YOffset;
                this.streamOut.WriteLittleEndian(value);
                value = this.Header.YScaleFactor * this.inventory.MinY + this.Header.YOffset;
                this.streamOut.WriteLittleEndian(value);
                value = this.Header.ZScaleFactor * this.inventory.MaxZ + this.Header.ZOffset;
                this.streamOut.WriteLittleEndian(value);
                value = this.Header.ZScaleFactor * this.inventory.MinZ + this.Header.ZOffset;
                this.streamOut.WriteLittleEndian(value);
                if (this.Header.VersionMinor >= 4) // only update extended counters for LAS 1.4
                {
                    this.streamOut.Seek(247, SeekOrigin.Begin);
                    UInt64 number = this.inventory.NumberOfPointRecords;
                    this.streamOut.WriteLittleEndian(number);
                    for (int i = 0; i < 15; i++)
                    {
                        number = this.inventory.NumberOfPointsByReturn[i + 1];
                        this.streamOut.WriteLittleEndian(number);
                    }
                }
            }
            this.inventory = null;
            this.streamOut.Seek(0, SeekOrigin.End);

            if (this.laxIndex != null)
            {
                this.laxIndex.Complete(100000, -20);
                this.laxIndex.Write(this.laxFileName);

                this.laxFileName = null;
                this.laxIndex = null;
            }

            this.streamOut.Dispose();
            this.streamOut = null;
            return 0;
        }

        public void SetExploitSpatialIndex(bool exploit)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.LaxExploit = exploit;
        }

        public void SetDecompressSelective(LasZipDecompressSelective decompress_selective)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.las14decompressSelective = decompress_selective;
        }

        private int ReadHeader(out bool is_compressed)
        {
            // read the header variable after variable
            Span<byte> readBuffer = stackalloc byte[4];
            this.streamIn.ReadExactly(readBuffer);
            string fileSignature = Encoding.UTF8.GetString(readBuffer);
            if (String.Equals(fileSignature, "LASF", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new FileLoadException("wrong file_signature. not a LAS/LAZ file.");
            }
            this.Header.FileSourceID = this.streamIn.ReadUInt16LittleEndian();
            this.Header.GlobalEncoding = this.streamIn.ReadUInt16LittleEndian();
            this.Header.ProjectIDGuidData1 = this.streamIn.ReadUInt32LittleEndian();
            this.Header.ProjectIDGuidData2 = this.streamIn.ReadUInt16LittleEndian();
            this.Header.ProjectIDGuidData3 = this.streamIn.ReadUInt16LittleEndian();
            this.streamIn.ReadExactly(this.Header.ProjectIDGuidData4);
            this.Header.VersionMajor = (byte)this.streamIn.ReadByte();
            this.Header.VersionMinor = (byte)this.streamIn.ReadByte();
            this.streamIn.ReadExactly(this.Header.SystemIdentifier);
            this.streamIn.ReadExactly(this.Header.GeneratingSoftware);
            this.Header.FileCreationDay = this.streamIn.ReadUInt16LittleEndian();
            this.Header.FileCreationYear = this.streamIn.ReadUInt16LittleEndian();
            this.Header.HeaderSize = this.streamIn.ReadUInt16LittleEndian();
            this.Header.OffsetToPointData = this.streamIn.ReadUInt32LittleEndian();
            this.Header.NumberOfVariableLengthRecords = this.streamIn.ReadUInt32LittleEndian();
            this.Header.PointDataFormat = (byte)this.streamIn.ReadByte();
            this.Header.HeaderSize = this.streamIn.ReadUInt16LittleEndian();
            this.Header.OffsetToPointData = this.streamIn.ReadUInt32LittleEndian();
            this.Header.PointDataRecordLength = this.streamIn.ReadUInt16LittleEndian();
            this.Header.NumberOfPointRecords = this.streamIn.ReadUInt32LittleEndian();
            for (int i = 0; i < 5; i++)
            {
                this.Header.NumberOfPointsByReturn[i] = this.streamIn.ReadUInt32LittleEndian();
            }
            this.Header.XScaleFactor = this.streamIn.ReadDoubleLittleEndian();
            this.Header.YScaleFactor = this.streamIn.ReadDoubleLittleEndian();
            this.Header.ZScaleFactor = this.streamIn.ReadDoubleLittleEndian();
            this.Header.XOffset = this.streamIn.ReadDoubleLittleEndian();
            this.Header.YOffset = this.streamIn.ReadDoubleLittleEndian();
            this.Header.ZOffset = this.streamIn.ReadDoubleLittleEndian();
            this.Header.MaxX = this.streamIn.ReadDoubleLittleEndian();
            this.Header.MinX = this.streamIn.ReadDoubleLittleEndian();
            this.Header.MaxY = this.streamIn.ReadDoubleLittleEndian();
            this.Header.MinY = this.streamIn.ReadDoubleLittleEndian();
            this.Header.MaxZ = this.streamIn.ReadDoubleLittleEndian();
            this.Header.MinZ = this.streamIn.ReadDoubleLittleEndian();

            // special handling for LAS 1.3
            if ((this.Header.VersionMajor == 1) && (this.Header.VersionMinor >= 3))
            {
                if (this.Header.HeaderSize < 235)
                {
                    throw new FileLoadException("for LAS 1." + this.Header.VersionMinor + " header_size should at least be 235 but it is only " + this.Header.HeaderSize);
                }
                else
                {
                    this.Header.StartOfWaveformDataPacketRecord = this.streamIn.ReadUInt64LittleEndian();
                    this.Header.UserDataInHeaderSize = this.Header.HeaderSize - 235U;
                }
            }
            else
            {
                this.Header.UserDataInHeaderSize = this.Header.HeaderSize - 227U;
            }

            // special handling for LAS 1.4
            if ((this.Header.VersionMajor == 1) && (this.Header.VersionMinor >= 4))
            {
                if (this.Header.HeaderSize < 375)
                {
                    throw new FileLoadException("for LAS 1." + this.Header.VersionMinor + " header_size should at least be 375 but it is only " + this.Header.HeaderSize);
                }
                else
                {
                    this.Header.StartOfFirstExtendedVariableLengthRecord = this.streamIn.ReadUInt64LittleEndian();
                    this.Header.NumberOfExtendedVariableLengthRecords = this.streamIn.ReadUInt32LittleEndian();
                    this.Header.ExtendedNumberOfPointRecords = this.streamIn.ReadUInt64LittleEndian();
                    for (int i = 0; i < 15; i++)
                    {
                        this.Header.ExtendedNumberOfPointsByReturn[i] = this.streamIn.ReadUInt64LittleEndian();
                    }
                    this.Header.UserDataInHeaderSize = this.Header.HeaderSize - 375U;
                }
            }

            // load any number of user-defined bytes that might have been added to the header
            if (this.Header.UserDataInHeaderSize != 0)
            {
                this.Header.UserDataInHeader = new byte[this.Header.UserDataInHeaderSize];
                this.streamIn.ReadExactly(this.Header.UserDataInHeader, 0, (int)this.Header.UserDataInHeaderSize);
            }

            // read variable length records into the header
            UInt32 vlrs_size = 0;
            LasZip? laszip = null;
            if (this.Header.NumberOfVariableLengthRecords != 0)
            {
                this.Header.Vlrs.Capacity = (int)this.Header.NumberOfVariableLengthRecords;
                for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                {
                    // make sure there are enough bytes left to read a variable length record before the point block starts
                    if (((int)this.Header.OffsetToPointData - vlrs_size - this.Header.HeaderSize) < 54)
                    {
                        throw new FileLoadException("only " + (this.Header.OffsetToPointData - vlrs_size - this.Header.HeaderSize) + " bytes until point block after reading " + i + " of " + this.Header.NumberOfVariableLengthRecords + " vlrs. skipping remaining vlrs ...");
                    }

                    // read variable length records variable after variable (to avoid alignment issues)
                    this.Header.Vlrs[i].Reserved = this.streamIn.ReadUInt16LittleEndian();
                    this.streamIn.ReadExactly(this.Header.Vlrs[i].UserID, 0, 16);
                    this.Header.Vlrs[i].RecordID = this.streamIn.ReadUInt16LittleEndian();
                    this.Header.Vlrs[i].RecordLengthAfterHeader = this.streamIn.ReadUInt16LittleEndian();
                    this.streamIn.ReadExactly(this.Header.Vlrs[i].Description, 0, 32);

                    // keep track on the number of bytes we have read so far
                    vlrs_size += 54;

                    // check variable length record contents
                    if ((this.Header.Vlrs[i].Reserved != 0xAABB) && (this.Header.Vlrs[i].Reserved != 0x0))
                    {
                        throw new FileLoadException("wrong header.vlrs[" + i + "].reserved: " + this.Header.Vlrs[i].Reserved + " != 0xAABB or 0x0000");
                    }

                    // make sure there are enough bytes left to read the data of the variable length record before the point block starts

                    if (((int)this.Header.OffsetToPointData - vlrs_size - this.Header.HeaderSize) < this.Header.Vlrs[i].RecordLengthAfterHeader)
                    {
                        throw new FileLoadException("only " + (this.Header.OffsetToPointData - vlrs_size - this.Header.HeaderSize) + " bytes until point block when trying to read " + this.Header.Vlrs[i].RecordLengthAfterHeader + " bytes into header.vlrs[" + i + "].data");
                    }

                    // load data following the header of the variable length record
                    if (this.Header.Vlrs[i].RecordLengthAfterHeader != 0)
                    {
                        if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), "laszip encoded", StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordID == 22204))
                        {
                            laszip ??= new();

                            // read the LasZip VLR payload
                            //     UInt16  compressor                2 bytes
                            //     UInt32  coder                     2 bytes
                            //     byte   version_major             1 byte
                            //     byte   version_minor             1 byte
                            //     UInt16  version_revision          2 bytes
                            //     UInt32  options                   4 bytes
                            //     Int32  chunk_size                4 bytes
                            //     I64  number_of_special_evlrs   8 bytes
                            //     I64  offset_to_special_evlrs   8 bytes
                            //     UInt16  num_items                 2 bytes
                            //        UInt16 type                2 bytes * num_items
                            //        UInt16 size                2 bytes * num_items
                            //        UInt16 version             2 bytes * num_items
                            // which totals 34+6*num_items

                            laszip.Compressor = this.streamIn.ReadUInt16LittleEndian();
                            laszip.Coder = this.streamIn.ReadUInt16LittleEndian();
                            laszip.VersionMajor = (byte)this.streamIn.ReadByte();
                            laszip.VersionMinor = (byte)this.streamIn.ReadByte();
                            laszip.VersionRevision = this.streamIn.ReadUInt16LittleEndian();
                            laszip.Options = this.streamIn.ReadUInt32LittleEndian();
                            laszip.ChunkSize = this.streamIn.ReadUInt32LittleEndian();
                            laszip.NumberOfSpecialEvlrs = (Int64)this.streamIn.ReadUInt64LittleEndian();
                            laszip.OffsetToSpecialEvlrs = (Int64)this.streamIn.ReadUInt64LittleEndian();
                            laszip.NumItems = this.streamIn.ReadUInt16LittleEndian();

                            laszip.Items = new LasItem[laszip.NumItems];
                            for (int j = 0; j < laszip.NumItems; j++)
                            {
                                laszip.Items[j].Type = (LasItemType)this.streamIn.ReadUInt16LittleEndian();
                                laszip.Items[j].Size = this.streamIn.ReadUInt16LittleEndian();
                                laszip.Items[j].Version = this.streamIn.ReadUInt16LittleEndian();
                            }
                        }
                        else
                        {
                            this.Header.Vlrs[i].Data = new byte[this.Header.Vlrs[i].RecordLengthAfterHeader];
                            this.streamIn.ReadExactly(this.Header.Vlrs[i].Data, 0, this.Header.Vlrs[i].RecordLengthAfterHeader);
                        }
                    }
                    else
                    {
                        this.Header.Vlrs[i].Data = null;
                    }

                    // keep track on the number of bytes we have read so far
                    vlrs_size += this.Header.Vlrs[i].RecordLengthAfterHeader;

                    // special handling for LasZip VLR
                    if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), "laszip encoded", StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordID == 22204))
                    {
                        // we take our the VLR for LasZip away
                        this.Header.OffsetToPointData -= (UInt32)(54 + this.Header.Vlrs[i].RecordLengthAfterHeader);
                        vlrs_size -= (UInt32)(54 + this.Header.Vlrs[i].RecordLengthAfterHeader);
                        i--;
                        this.Header.NumberOfVariableLengthRecords--;
                        // free or resize the VLR array
                        if (this.Header.NumberOfVariableLengthRecords == 0)
                        {
                            this.Header.Vlrs.Clear();
                        }
                        else
                        {
                            this.Header.Vlrs.Capacity = (int)this.Header.NumberOfVariableLengthRecords;
                        }
                    }
                }
            }

            // load any number of user-defined bytes that might have been added after the header
            this.Header.UserDataAfterHeaderSize = this.Header.OffsetToPointData - vlrs_size - this.Header.HeaderSize;
            if (this.Header.UserDataAfterHeaderSize != 0)
            {
                this.Header.UserDataAfterHeader = new byte[this.Header.UserDataAfterHeaderSize];
                this.streamIn.ReadExactly(this.Header.UserDataAfterHeader, 0, (int)this.Header.UserDataAfterHeaderSize);
            }

            // remove extra bits in point data type
            if (((this.Header.PointDataFormat & 128) != 0) || ((this.Header.PointDataFormat & 64) != 0))
            {
                if (laszip == null)
                {
                    throw new FileLoadException("this file was compressed with an experimental version of LasZip. contact 'info@rapidlasso.de' for assistance");
                }
                this.Header.PointDataFormat &= 127;
            }

            // check if file is compressed
            if (laszip != null)
            {
                // yes. check the compressor state
                is_compressed = true;
                if (!laszip.Check(this.Header.PointDataRecordLength))
                {
                    throw new FileLoadException("upgrade to the latest release of LasZip or contact 'info@rapidlasso.de' for assistance");
                }
            }
            else
            {
                // no. setup an un-compressed read
                is_compressed = false;
                laszip = new();
                if (!laszip.Setup(this.Header.PointDataFormat, this.Header.PointDataRecordLength, LasZip.CompressorNone))
                {
                    throw new FileLoadException("invalid combination of point_data_format " + this.Header.PointDataFormat + " and point_data_record_length " + this.Header.PointDataRecordLength);
                }
            }

            int gpstimeOffset = 20;
            if (this.Header.PointDataFormat > 5)
            {
                gpstimeOffset = 22;
            }

            // create point's item pointers
            // this.point_items = new byte[this.Header.PointDataRecordLength];
            for (int i = 0; i < laszip.NumItems; i++)
            {
                switch (laszip.Items[i].Type)
                {
                    case LasItemType.Point10:
                    case LasItemType.Point14:
                        this.Point.X = BinaryPrimitives.ReadInt32LittleEndian(this.point_items);
                        this.Point.Y = BinaryPrimitives.ReadInt32LittleEndian(this.point_items[4..]);
                        this.Point.Z = BinaryPrimitives.ReadInt32LittleEndian(this.point_items[8..]);
                        break;
                    case LasItemType.Gpstime11:
                        this.Point.Gpstime = BinaryPrimitives.ReadDoubleLittleEndian(this.point_items[gpstimeOffset..]);
                        break;
                    case LasItemType.Rgb12:
                        this.Point.Rgb[0] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[28..]);
                        this.Point.Rgb[1] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[30..]);
                        this.Point.Rgb[2] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[32..]);
                        break;
                    case LasItemType.Rgb14:
                        this.Point.Rgb[0] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[30..]);
                        this.Point.Rgb[1] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[32..]);
                        this.Point.Rgb[2] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[34..]);
                        break;
                    case LasItemType.RgbNir14:
                        this.Point.Rgb[0] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[30..]);
                        this.Point.Rgb[1] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[32..]);
                        this.Point.Rgb[2] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[34..]);
                        this.Point.Rgb[3] = BinaryPrimitives.ReadUInt16LittleEndian(this.point_items[36..]);
                        break;
                    case LasItemType.Byte:
                    case LasItemType.Byte14:
                        Array.Copy(this.point_items, this.Header.PointDataRecordLength - this.Point.NumExtraBytes, this.Point.ExtraBytes, 0, this.Point.NumExtraBytes);
                        break;
                    case LasItemType.Wavepacket13:
                        Array.Copy(this.point_items, 34, this.Point.Wavepacket, 0, this.Point.Wavepacket.Length);
                        break;
                    case LasItemType.Wavepacket14:
                        Array.Copy(this.point_items, 38, this.Point.Wavepacket, 0, this.Point.Wavepacket.Length);
                        break;
                    default:
                        throw new NotSupportedException("unknown LASitem type " + laszip.Items[i].Type);
                }
            }

            // did the user request to recode the compatibility mode points?
            this.compatibility_mode = false;
            if (this.request_compatibility_mode && (this.Header.VersionMinor < 4))
            {
                // does this file contain compatibility mode recoded LAS 1.4 content
                LasVariableLengthRecord? compatibility_VLR = null;
                if (this.Header.PointDataFormat == 1 || this.Header.PointDataFormat == 3 || this.Header.PointDataFormat == 4 || this.Header.PointDataFormat == 5)
                {
                    // if we find the compatibility VLR
                    for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                    {
                        if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), "lascompatible\0\0", StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordID == 22204))
                        {
                            if (this.Header.Vlrs[i].RecordLengthAfterHeader == 2 + 2 + 4 + 148)
                            {
                                compatibility_VLR = (LasVariableLengthRecord?)this.Header.Vlrs[i];
                                break;
                            }
                        }
                    }

                    if (compatibility_VLR != null)
                    {
                        // and we also find the extra bytes VLR with the right attributes
                        LasAttributer attributer = new();
                        for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                        {
                            if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), "LASF_Spec\0\0\0\0\0\0", StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordID == 4))
                            {
                                this.attributer.init_attributes(this.Header.Vlrs[i].RecordLengthAfterHeader / 192U, this.Header.Vlrs[i].Data);
                                this.start_scan_angle = attributer.get_attribute_start("LAS 1.4 scan angle");
                                this.start_extended_returns = attributer.get_attribute_start("LAS 1.4 extended returns");
                                this.start_classification = attributer.get_attribute_start("LAS 1.4 classification");
                                this.start_flags_and_channel = attributer.get_attribute_start("LAS 1.4 flags and channel");
                                this.start_NIR_band = attributer.get_attribute_start("LAS 1.4 NIR band");
                                break;
                            }
                        }

                        // can we do it ... ?
                        if ((this.start_scan_angle != -1) && (this.start_extended_returns != -1) && (this.start_classification != -1) && (this.start_flags_and_channel != -1))
                        {
                            // yes ... so let's fix the header (using the content from the compatibility VLR)
                            MemoryStream vlrData = new(compatibility_VLR.Data, 0, compatibility_VLR.RecordLengthAfterHeader);
                            // read control info
                            UInt16 laszip_version = vlrData.ReadUInt16LittleEndian();
                            UInt16 compatible_version = vlrData.ReadUInt16LittleEndian();
                            UInt32 unused = vlrData.ReadUInt16LittleEndian();

                            // read the 148 bytes of the extended LAS 1.4 header
                            UInt64 start_of_waveform_data_packet_record = vlrData.ReadUInt64LittleEndian();
                            if (start_of_waveform_data_packet_record != 0)
                            {
                                throw new FileLoadException("start_of_waveform_data_packet_record is " + start_of_waveform_data_packet_record);
                            }
                            this.Header.StartOfWaveformDataPacketRecord = 0;
                            UInt64 start_of_first_extended_variable_length_record = vlrData.ReadUInt64LittleEndian();
                            if (start_of_first_extended_variable_length_record != 0)
                            {
                                throw new NotSupportedException("EVLRs not supported. start_of_first_extended_variable_length_record is " + start_of_first_extended_variable_length_record);
                            }
                            this.Header.StartOfFirstExtendedVariableLengthRecord = 0;
                            UInt32 number_of_extended_variable_length_records = vlrData.ReadUInt32LittleEndian();
                            if (number_of_extended_variable_length_records != 0)
                            {
                                throw new NotSupportedException("EVLRs not supported. number_of_extended_variable_length_records is " + number_of_extended_variable_length_records);
                            }
                            this.Header.NumberOfExtendedVariableLengthRecords = 0;
                            UInt64 extended_number_of_point_records = vlrData.ReadUInt64LittleEndian();
                            if (this.Header.NumberOfPointRecords != 0 && ((UInt64)(this.Header.NumberOfPointRecords)) != extended_number_of_point_records)
                            {
                                throw new FileLoadException("number_of_point_records is " + this.Header.NumberOfPointRecords + ". but extended_number_of_point_records is " + extended_number_of_point_records);
                            }
                            this.Header.ExtendedNumberOfPointRecords = extended_number_of_point_records;
                            UInt64 extended_number_of_points_by_return;
                            for (UInt32 r = 0; r < 15; r++)
                            {
                                extended_number_of_points_by_return = vlrData.ReadUInt64LittleEndian();
                                if ((r < 5) && this.Header.NumberOfPointsByReturn[r] != 0 && ((UInt64)(this.Header.NumberOfPointsByReturn[r])) != extended_number_of_points_by_return)
                                {
                                    throw new FileLoadException("number_of_points_by_return[" + r + "] is " + extended_number_of_points_by_return + " but extended_number_of_points_by_return[" + r + "] is " + this.Header.NumberOfPointsByReturn[r]);
                                }
                                this.Header.ExtendedNumberOfPointsByReturn[r] = extended_number_of_points_by_return;
                            }

                            // remove the compatibility VLR
                            if (this.RemoveVlr("lascompatible\0\0", 22204) != 0)
                            {
                                throw new FileLoadException("removing the compatibility VLR");
                            }

                            // remove the LAS 1.4 attributes from the "extra bytes" description
                            if (this.start_NIR_band != -1)
                                attributer.remove_attribute("LAS 1.4 NIR band");
                            attributer.remove_attribute("LAS 1.4 flags and channel");
                            attributer.remove_attribute("LAS 1.4 classification");
                            attributer.remove_attribute("LAS 1.4 extended returns");
                            attributer.remove_attribute("LAS 1.4 scan angle");

                            // either rewrite or remove the "extra bytes" VLR
                            if (attributer.number_attributes != 0)
                            {
                                byte[] vlrDataBytes = this.attributer.get_bytes();
                                if (this.AddVlr("LASF_Spec\0\0\0\0\0\0", 4, (UInt16)(attributer.number_attributes * LasAttribute.SerializedSizeInBytes), null, vlrDataBytes) != 0)
                                {
                                    throw new FileLoadException("rewriting the extra bytes VLR without 'LAS 1.4 compatibility mode' attributes");
                                }
                            }
                            else
                            {
                                if (this.RemoveVlr("LASF_Spec\0\0\0\0\0\0", 4) != 0)
                                {
                                    throw new FileLoadException("removing the LAS 1.4 attribute VLR");
                                }
                            }

                            // upgrade to LAS 1.4
                            if (this.Header.VersionMinor < 3)
                            {
                                // LAS 1.2 header is 148 bytes less than LAS 1.4+ header
                                this.Header.HeaderSize += 148;
                                this.Header.OffsetToPointData += 148;
                            }
                            else
                            {
                                // LAS 1.3 header is 140 bytes less than LAS 1.4+ header
                                this.Header.HeaderSize += 140;
                                this.Header.OffsetToPointData += 140;
                            }
                            this.Header.VersionMinor = 4;

                            // maybe turn on the bit indicating the presence of the OGC WKT
                            for (int i = 0; i < this.Header.NumberOfVariableLengthRecords; i++)
                            {
                                if (String.Equals(Encoding.UTF8.GetString(this.Header.Vlrs[i].UserID), "LASF_Projection", StringComparison.Ordinal) && (this.Header.Vlrs[i].RecordID == 2112))
                                {
                                    this.Header.GlobalEncoding |= (1 << 4);
                                    break;
                                }
                            }

                            // update point type and size
                            this.Point.ExtendedPointType = 1;

                            if (this.Header.PointDataFormat == 1)
                            {
                                this.Header.PointDataFormat = 6;
                                this.Header.PointDataRecordLength -= (5 - 2); // record is 2 bytes larger but minus 5 extra bytes
                            }
                            else if (this.Header.PointDataFormat == 3)
                            {
                                if (this.start_NIR_band == -1)
                                {
                                    this.Header.PointDataFormat = 7;
                                    this.Header.PointDataRecordLength -= (5 - 2); // record is 2 bytes larger but minus 5 extra bytes
                                }
                                else
                                {
                                    this.Header.PointDataFormat = 8;
                                    this.Header.PointDataRecordLength -= (7 - 4); // record is 4 bytes larger but minus 7 extra bytes
                                }
                            }
                            else
                            {
                                if (this.start_NIR_band == -1)
                                {
                                    this.Header.PointDataFormat = 9;
                                    this.Header.PointDataRecordLength -= (5 - 2);
                                }
                                else
                                {
                                    this.Header.PointDataFormat = 10;
                                    this.Header.PointDataRecordLength -= (7 - 4);
                                }
                            }

                            // we are operating in compatibility mode
                            this.compatibility_mode = true;
                        }
                    }
                }
            }
            else if (this.Header.PointDataFormat > 5)
            {
                this.Point.ExtendedPointType = 1;
            }

            // create the point reader
            this.reader = new(this.las14decompressSelective);
            if (this.reader.Setup(laszip) == false)
            {
                throw new InvalidOperationException("setup of LASreadPoint failed");
            }

            if (this.reader.Init(this.streamIn) == false)
            {
                throw new InvalidOperationException("init of LASreadPoint failed");
            }

            // set the point number and point count
            this.nPoints = (Int64)(this.Header.NumberOfPointRecords != 0 ? this.Header.NumberOfPointRecords : this.Header.ExtendedNumberOfPointRecords);
            this.currentPointIndex = 0;
            return 0;
        }

        public void HasSpatialIndex(ref bool is_indexed, ref bool is_appended)
        {
            if (this.reader == null)
            {
                throw new InvalidOperationException("reader is not open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            if (this.LaxExploit == false)
            {
                throw new InvalidOperationException("exploiting of spatial indexing not enabled before opening reader");
            }

            // check if reader found spatial indexing information when opening file

            is_indexed = this.laxIndex != null;

            // optional: inform whether spatial index is appended to LAZ file or in separate LAX file
            is_appended = false;
        }

        public bool IsInsideRectangle(double r_min_x, double r_min_y, double r_max_x, double r_max_y)
        {
            if (this.reader == null)
            {
                throw new InvalidOperationException("reader is not open");
            }
            if (this.LaxExploit == false)
            {
                throw new InvalidOperationException("exploiting of spatial indexing not enabled before opening reader");
            }

            this.laxRminX = r_min_x;
            this.laxRminY = r_min_y;
            this.laxRmaxX = r_max_x;
            this.laxRmaxY = r_max_y;

            if (this.laxIndex != null)
            {
                return this.laxIndex.IntersectRectangle(r_min_x, r_min_y, r_max_x, r_max_y) == false;
            }

            return (this.Header.MinX > r_max_x) || (this.Header.MinY > r_max_y) || (this.Header.MaxX < r_min_x) || (this.Header.MaxY < r_min_y);
        }

        private int ExploitSpatialIndex(bool exploit)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.LaxExploit = exploit;
            return 0;
        }

        private int DecompressSelective(LasZipDecompressSelective decompress_selective)
        {
            if (this.reader != null)
            {
                throw new InvalidOperationException("reader is already open");
            }
            if (this.writer != null)
            {
                throw new InvalidOperationException("writer is already open");
            }

            this.las14decompressSelective = decompress_selective;
            return 0;
        }

        //private int ReadHeader(out bool is_compressed)
        //{
        //    UInt32 i;

        //    // read the header variable after variable
        //    Span<byte> file_signature = stackalloc byte[4];
        //    this.streamIn.ReadExactly(file_signature);
        //    if (String.Equals(Encoding.UTF8.GetString(file_signature), "LASF", StringComparison.Ordinal) == false)
        //    {
        //        throw new FileLoadException("wrong file_signature. not a LAS/LAZ file.");
        //    }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.file_source_ID)); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.global_encoding)); }
        //    try { this.streamIn.get32bitsLE((byte*)&(this.Header.project_ID_GUID_data_1)); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.project_ID_GUID_data_2)); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.project_ID_GUID_data_3)); }
        //    try { this.streamIn.getBytes((byte*)this.Header.project_ID_GUID_data_4, 8); }
        //    try { this.streamIn.getBytes((byte*)&(this.Header.version_major), 1); }
        //    try { this.streamIn.getBytes((byte*)&(this.Header.version_minor), 1); }
        //    try { this.streamIn.getBytes((byte*)this.Header.system_identifier, 32); }
        //    try { this.streamIn.getBytes((byte*)this.Header.generating_software, 32); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.file_creation_day)); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.file_creation_year)); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.header_size)); }
        //    try { this.streamIn.get32bitsLE((byte*)&(this.Header.offset_to_point_data)); }
        //    try { this.streamIn.get32bitsLE((byte*)&(this.Header.number_of_variable_length_records)); }
        //    try { this.streamIn.getBytes((byte*)&(this.Header.point_data_format), 1); }
        //    try { this.streamIn.get16bitsLE((byte*)&(this.Header.point_data_record_length)); }
        //    try { this.streamIn.get32bitsLE((byte*)&(this.Header.number_of_point_records)); }
        //    for (i = 0; i < 5; i++)
        //    {
        //        try { this.streamIn.get32bitsLE((byte*)&(this.Header.number_of_points_by_return[i])); }
        //    }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.x_scale_factor)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.y_scale_factor)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.z_scale_factor)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.x_offset)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.y_offset)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.z_offset)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.max_x)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.min_x)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.max_y)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.min_y)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.max_z)); }
        //    try { this.streamIn.get64bitsLE((byte*)&(this.Header.min_z)); }

        //    // special handling for LAS 1.3
        //    if ((this.Header.version_major == 1) && (this.Header.version_minor >= 3))
        //    {
        //        if (this.Header.header_size < 235)
        //        {
        //            sprintf(this.error, "for LAS 1.%d header_size should at least be 235 but it is only %d", this.Header.version_minor, this.Header.header_size);
        //            return 1;
        //        }
        //        else
        //        {
        //            try { this.streamIn.get64bitsLE((byte*)&(this.Header.start_of_waveform_data_packet_record)); }
        //            this.Header.user_data_in_header_size = this.Header.header_size - 235;
        //        }
        //    }
        //    else
        //    {
        //        this.Header.user_data_in_header_size = this.Header.header_size - 227;
        //    }

        //    // special handling for LAS 1.4
        //    if ((this.Header.version_major == 1) && (this.Header.version_minor >= 4))
        //    {
        //        if (this.Header.header_size < 375)
        //        {
        //            sprintf(this.error, "for LAS 1.%d header_size should at least be 375 but it is only %d", this.Header.version_minor, this.Header.header_size);
        //            return 1;
        //        }
        //        else
        //        {
        //            try { this.streamIn.get64bitsLE((byte*)&(this.Header.start_of_first_extended_variable_length_record)); }
        //            try { this.streamIn.get32bitsLE((byte*)&(this.Header.number_of_extended_variable_length_records)); }
        //            try { this.streamIn.get64bitsLE((byte*)&(this.Header.extended_number_of_point_records)); }
        //            for (i = 0; i < 15; i++)
        //            {
        //                try { this.streamIn.get64bitsLE((byte*)&(this.Header.extended_number_of_points_by_return[i])); }
        //                return 1;
        //            }
        //            this.Header.user_data_in_header_size = this.Header.header_size - 375;
        //        }
        //    }

        //    // load any number of user-defined bytes that might have been added to the header
        //    if (this.Header.user_data_in_header_size)
        //    {
        //        this.Header.user_data_in_header = new byte[this.Header.user_data_in_header_size];
        //        try { this.streamIn.getBytes((byte*)this.Header.user_data_in_header, this.Header.user_data_in_header_size); }
        //    }

        //    // read variable length records into the header
        //    UInt32 vlrs_size = 0;
        //    LasZip laszip = 0;

        //    if (this.Header.number_of_variable_length_records)
        //    {
        //        UInt32 i;

        //        this.Header.vlrs = (laszip_vlr*)malloc(sizeof(laszip_vlr) * this.Header.number_of_variable_length_records);

        //        if (this.Header.vlrs == 0)
        //        {
        //            sprintf(this.error, "allocating %u VLRs", this.Header.number_of_variable_length_records);
        //            return 1;
        //        }

        //        for (i = 0; i < this.Header.number_of_variable_length_records; i++)
        //        {
        //            // make sure there are enough bytes left to read a variable length record before the point block starts

        //            if (((int)this.Header.offset_to_point_data - vlrs_size - this.Header.header_size) < 54)
        //            {
        //                sprintf(this.warning, "only %d bytes until point block after reading %d of %d vlrs. skipping remaining vlrs ...", (int)this.Header.offset_to_point_data - vlrs_size - this.Header.header_size, i, this.Header.number_of_variable_length_records);
        //                this.Header.number_of_variable_length_records = i;
        //                break;
        //            }

        //            // read variable length records variable after variable (to avoid alignment issues)
        //            try { this.streamIn.get16bitsLE((byte*)&(this.Header.vlrs[i].reserved)); }
        //            try { this.streamIn.getBytes((byte*)this.Header.vlrs[i].user_id, 16); }
        //            try { this.streamIn.get16bitsLE((byte*)&(this.Header.vlrs[i].record_id)); }
        //            try { this.streamIn.get16bitsLE((byte*)&(this.Header.vlrs[i].record_length_after_header)); }
        //            try { this.streamIn.getBytes((byte*)this.Header.vlrs[i].description, 32); }

        //            // keep track on the number of bytes we have read so far
        //            vlrs_size += 54;

        //            // check variable length record contents
        //            if ((this.Header.vlrs[i].reserved != 0xAABB) && (this.Header.vlrs[i].reserved != 0x0))
        //            {
        //                sprintf(this.warning, "wrong header.vlrs[%d].reserved: %d != 0xAABB and %d != 0x0", i, this.Header.vlrs[i].reserved, this.Header.vlrs[i].reserved);
        //            }

        //            // make sure there are enough bytes left to read the data of the variable length record before the point block starts
        //            if (((int)this.Header.offset_to_point_data - vlrs_size - this.Header.header_size) < this.Header.vlrs[i].record_length_after_header)
        //            {
        //                sprintf(this.warning, "only %d bytes until point block when trying to read %d bytes into header.vlrs[%d].data", (int)this.Header.offset_to_point_data - vlrs_size - this.Header.header_size, this.Header.vlrs[i].record_length_after_header, i);
        //                this.Header.vlrs[i].record_length_after_header = (int)this.Header.offset_to_point_data - vlrs_size - this.Header.header_size;
        //            }

        //            // load data following the header of the variable length record
        //            if (this.Header.vlrs[i].record_length_after_header)
        //            {
        //                if ((strcmp(this.Header.vlrs[i].user_id, "laszip encoded") == 0) && (this.Header.vlrs[i].record_id == 22204))
        //                {
        //                    if (laszip)
        //                    {
        //                        delete laszip;
        //                    }

        //                    laszip = new LasZip();

        //                    if (laszip == 0)
        //                    {
        //                        sprintf(this.error, "could not alloc LasZip");
        //                        return 1;
        //                    }

        //                    // read the LasZip VLR payload

        //                    //     UInt16  compressor                2 bytes
        //                    //     UInt32  coder                     2 bytes
        //                    //     byte   version_major             1 byte
        //                    //     byte   version_minor             1 byte
        //                    //     UInt16  version_revision          2 bytes
        //                    //     UInt32  options                   4 bytes
        //                    //     Int32  chunk_size                4 bytes
        //                    //     I64  number_of_special_evlrs   8 bytes
        //                    //     I64  offset_to_special_evlrs   8 bytes
        //                    //     UInt16  num_items                 2 bytes
        //                    //        UInt16 type                2 bytes * num_items
        //                    //        UInt16 size                2 bytes * num_items
        //                    //        UInt16 version             2 bytes * num_items
        //                    // which totals 34+6*num_items
        //                    try { this.streamIn.get16bitsLE((byte*)&(laszip->compressor)); }
        //                    try { this.streamIn.get16bitsLE((byte*)&(laszip->coder)); }
        //                    try { this.streamIn.getBytes((byte*)&(laszip->version_major), 1); }
        //                    try { this.streamIn.getBytes((byte*)&(laszip->version_minor), 1); }
        //                    try { this.streamIn.get16bitsLE((byte*)&(laszip->version_revision)); }
        //                    try { this.streamIn.get32bitsLE((byte*)&(laszip->options)); }
        //                    try { this.streamIn.get32bitsLE((byte*)&(laszip->chunk_size)); }
        //                    try { this.streamIn.get64bitsLE((byte*)&(laszip->number_of_special_evlrs)); }
        //                    try { this.streamIn.get64bitsLE((byte*)&(laszip->offset_to_special_evlrs)); }
        //                    try { this.streamIn.get16bitsLE((byte*)&(laszip->num_items)); }

        //                    laszip->items = new LASitem[laszip->num_items];
        //                    UInt32 j;
        //                    for (j = 0; j < laszip->num_items; j++)
        //                    {
        //                        UInt16 type;
        //                        try { this.streamIn.get16bitsLE((byte*)&type); }
        //                        laszip->items[j].type = (LASitem::Type)type;
        //                        try { this.streamIn.get16bitsLE((byte*)&(laszip->items[j].size)); }
        //                        try { this.streamIn.get16bitsLE((byte*)&(laszip->items[j].version)); }
        //                    }
        //                }
        //                else
        //                {
        //                    this.Header.vlrs[i].data = new byte[this.Header.vlrs[i].record_length_after_header];

        //                    try { this.streamIn.getBytes(this.Header.vlrs[i].data, this.Header.vlrs[i].record_length_after_header); }
        //                }
        //            }
        //            else
        //            {
        //                this.Header.vlrs[i].data = 0;
        //            }

        //            // keep track on the number of bytes we have read so far
        //            vlrs_size += this.Header.vlrs[i].record_length_after_header;

        //            // special handling for LasZip VLR
        //            if ((strcmp(this.Header.vlrs[i].user_id, "laszip encoded") == 0) && (this.Header.vlrs[i].record_id == 22204))
        //            {
        //                // we take our the VLR for LasZip away
        //                this.Header.offset_to_point_data -= (54 + this.Header.vlrs[i].record_length_after_header);
        //                vlrs_size -= (54 + this.Header.vlrs[i].record_length_after_header);
        //                i--;
        //                this.Header.number_of_variable_length_records--;
        //                // free or resize the VLR array
        //                if (this.Header.number_of_variable_length_records == 0)
        //                {
        //                    free(this.Header.vlrs);
        //                    this.Header.vlrs = 0;
        //                }
        //                else
        //                {
        //                    this.Header.vlrs = (laszip_vlr*)realloc(this.Header.vlrs, sizeof(laszip_vlr) * this.Header.number_of_variable_length_records);
        //                }
        //            }
        //        }
        //    }

        //    // load any number of user-defined bytes that might have been added after the header
        //    this.Header.user_data_after_header_size = (Int32)this.Header.offset_to_point_data - vlrs_size - this.Header.header_size;
        //    if (this.Header.user_data_after_header_size)
        //    {
        //        if (this.Header.user_data_after_header)
        //        {
        //            delete[] this.Header.user_data_after_header;
        //        }
        //        this.Header.user_data_after_header = new byte[this.Header.user_data_after_header_size];

        //        try { this.streamIn.getBytes((byte*)this.Header.user_data_after_header, this.Header.user_data_after_header_size); }
        //    }

        //    // remove extra bits in point data type
        //    if ((this.Header.point_data_format & 128) || (this.Header.point_data_format & 64))
        //    {
        //        if (!laszip)
        //        {
        //            sprintf(this.error, "this file was compressed with an experimental version of LasZip. contact 'info@rapidlasso.de' for assistance");
        //            return 1;
        //        }
        //        this.Header.point_data_format &= 127;
        //    }

        //    // check if file is compressed

        //    if (laszip)
        //    {
        //        // yes. check the compressor state
        //        *is_compressed = 1;
        //        if (!laszip->check(this.Header.point_data_record_length))
        //        {
        //            sprintf(this.error, "%s upgrade to the latest release of LasZip or contact 'info@rapidlasso.de' for assistance", laszip->get_error());
        //            return 1;
        //        }
        //    }
        //    else
        //    {
        //        // no. setup an un-compressed read
        //        *is_compressed = 0;
        //        laszip = new LasZip;
        //        if (laszip == 0)
        //        {
        //            sprintf(this.error, "could not alloc LasZip");
        //            return 1;
        //        }
        //        if (!laszip->setup(this.Header.point_data_format, this.Header.point_data_record_length, LASZIP_COMPRESSOR_NONE))
        //        {
        //            sprintf(this.error, "invalid combination of point_data_format %d and point_data_record_length %d", (Int32)this.Header.point_data_format, (Int32)this.Header.point_data_record_length);
        //            return 1;
        //        }
        //    }

        //    // create point's item pointers
        //    this.point_items = new byte*[laszip->num_items];
        //    for (i = 0; i < laszip->num_items; i++)
        //    {
        //        switch (laszip->items[i].type)
        //        {
        //            case LASitem::POINT10:
        //            case LASitem::POINT14:
        //                this.point_items[i] = (byte*)&(this.Point.X);
        //                break;
        //            case LASitem::GPSTIME11:
        //                this.point_items[i] = (byte*)&(this.Point.gps_time);
        //                break;
        //            case LASitem::RGB12:
        //            case LASitem::RGB14:
        //            case LASitem::RGBNIR14:
        //                this.point_items[i] = (byte*)this.Point.Rgb;
        //                break;
        //            case LASitem::BYTE:
        //            case LASitem::BYTE14:
        //                this.Point.num_extra_bytes = laszip->items[i].size;
        //                if (this.Point.ExtraBytes) delete[] this.Point.ExtraBytes;
        //                this.Point.ExtraBytes = new byte[this.Point.num_extra_bytes];
        //                this.point_items[i] = this.Point.ExtraBytes;
        //                break;
        //            case LASitem::WAVEPACKET13:
        //            case LASitem::WAVEPACKET14:
        //                this.point_items[i] = (byte*)&(this.Point.wave_packet);
        //                break;
        //            default:
        //                sprintf(this.error, "unknown LASitem type %d", (Int32)laszip->items[i].type);
        //                return 1;
        //        }
        //    }

        //    // did the user request to recode the compatibility mode points?
        //    this.compatibility_mode = false;

        //    if (this.request_compatibility_mode && (this.Header.version_minor < 4))
        //    {
        //        // does this file contain compatibility mode recoded LAS 1.4 content
        //        LasZipVariableLengthRecord? compatibility_VLR = null;
        //        if (this.Header.point_data_format == 1 || this.Header.point_data_format == 3 || this.Header.point_data_format == 4 || this.Header.point_data_format == 5)
        //        {
        //            // if we find the compatibility VLR
        //            for (i = 0; i < this.Header.number_of_variable_length_records; i++)
        //            {
        //                if ((strncmp(this.Header.vlrs[i].user_id, "lascompatible\0\0", 16) == 0) && (this.Header.vlrs[i].record_id == 22204))
        //                {
        //                    if (this.Header.vlrs[i].record_length_after_header == 2 + 2 + 4 + 148)
        //                    {
        //                        compatibility_VLR = &(this.Header.vlrs[i]);
        //                        break;
        //                    }
        //                }
        //            }

        //            if (compatibility_VLR)
        //            {
        //                // and we also find the extra bytes VLR with the right attributes
        //                LasAttributer attributer = new();
        //                for (i = 0; i < this.Header.number_of_variable_length_records; i++)
        //                {
        //                    if ((strncmp(this.Header.vlrs[i].user_id, "LASF_Spec\0\0\0\0\0\0", 16) == 0) && (this.Header.vlrs[i].record_id == 4))
        //                    {
        //                        attributer.init_attributes(this.Header.vlrs[i].record_length_after_header / 192, (LASattribute*)this.Header.vlrs[i].data);
        //                        this.start_scan_angle = attributer.get_attribute_start("LAS 1.4 scan angle");
        //                        this.start_extended_returns = attributer.get_attribute_start("LAS 1.4 extended returns");
        //                        this.start_classification = attributer.get_attribute_start("LAS 1.4 classification");
        //                        this.start_flags_and_channel = attributer.get_attribute_start("LAS 1.4 flags and channel");
        //                        this.start_NIR_band = attributer.get_attribute_start("LAS 1.4 NIR band");
        //                        break;
        //                    }
        //                }

        //                // can we do it ... ?
        //                if ((this.start_scan_angle != -1) && (this.start_extended_returns != -1) && (this.start_classification != -1) && (this.start_flags_and_channel != -1))
        //                {
        //                    // yes ... so let's fix the header (using the content from the compatibility VLR)
        //                    ByteStreamInArray vlrStream;
        //                    if (IS_LITTLE_ENDIAN())
        //                        vlrStream = new ByteStreamInArrayLE(compatibility_VLR->data, compatibility_VLR->record_length_after_header);
        //                    else
        //                        vlrStream = new ByteStreamInArrayBE(compatibility_VLR->data, compatibility_VLR->record_length_after_header);
        //                    // read control info
        //                    UInt16 laszip_version;
        //                    vlrStream->get16bitsLE((byte*)&laszip_version);
        //                    UInt16 compatible_version;
        //                    vlrStream->get16bitsLE((byte*)&compatible_version);
        //                    UInt32 unused;
        //                    vlrStream->get32bitsLE((byte*)&unused);
        //                    // read the 148 bytes of the extended LAS 1.4 header
        //                    UInt64 start_of_waveform_data_packet_record;
        //                    vlrStream->get64bitsLE((byte*)&start_of_waveform_data_packet_record);
        //                    if (start_of_waveform_data_packet_record != 0)
        //                    {
        //                        fprintf(stderr, "WARNING: start_of_waveform_data_packet_record is %I64d. reading 0 instead.\n", start_of_waveform_data_packet_record);
        //                    }
        //                    this.Header.start_of_waveform_data_packet_record = 0;
        //                    UInt64 start_of_first_extended_variable_length_record;
        //                    vlrStream->get64bitsLE((byte*)&start_of_first_extended_variable_length_record);
        //                    if (start_of_first_extended_variable_length_record != 0)
        //                    {
        //                        fprintf(stderr, "WARNING: EVLRs not supported. start_of_first_extended_variable_length_record is %I64d. reading 0 instead.\n", start_of_first_extended_variable_length_record);
        //                    }
        //                    this.Header.start_of_first_extended_variable_length_record = 0;
        //                    UInt32 number_of_extended_variable_length_records;
        //                    vlrStream->get32bitsLE((byte*)&number_of_extended_variable_length_records);
        //                    if (number_of_extended_variable_length_records != 0)
        //                    {
        //                        fprintf(stderr, "WARNING: EVLRs not supported. number_of_extended_variable_length_records is %u. reading 0 instead.\n", number_of_extended_variable_length_records);
        //                    }
        //                    this.Header.number_of_extended_variable_length_records = 0;
        //                    UInt64 extended_number_of_point_records = 0;
        //                    vlrStream->get64bitsLE((byte*)&extended_number_of_point_records);
        //                    if (this.Header.number_of_point_records != 0 && ((UInt64)(this.Header.number_of_point_records)) != extended_number_of_point_records)
        //                    {
        //                        fprintf(stderr, "WARNING: number_of_point_records is %u. but extended_number_of_point_records is %I64u.\n", this.Header.number_of_point_records, extended_number_of_point_records);
        //                    }
        //                    this.Header.extended_number_of_point_records = extended_number_of_point_records;
        //                    UInt64 extended_number_of_points_by_return;
        //                    for (UInt32 r = 0; r < 15; r++)
        //                    {
        //                        vlrStream->get64bitsLE((byte*)&extended_number_of_points_by_return);
        //                        if ((r < 5) && this.Header.number_of_points_by_return[r] != 0 && ((UInt64)(this.Header.number_of_points_by_return[r])) != extended_number_of_points_by_return)
        //                        {
        //                            fprintf(stderr, "WARNING: number_of_points_by_return[%u] is %u. but extended_number_of_points_by_return[%u] is %I64u.\n", r, this.Header.number_of_points_by_return[r], r, extended_number_of_points_by_return);
        //                        }
        //                        this.Header.extended_number_of_points_by_return[r] = extended_number_of_points_by_return;
        //                    }

        //                    // remove the compatibility VLR

        //                    if (laszip_remove_vlr(laszip_dll, "lascompatible\0\0", 22204))
        //                    {
        //                        sprintf(this.error, "removing the compatibility VLR");
        //                        return 1;
        //                    }

        //                    // remove the LAS 1.4 attributes from the "extra bytes" description
        //                    if (this.start_NIR_band != -1)
        //                        attributer.remove_attribute("LAS 1.4 NIR band");
        //                    attributer.remove_attribute("LAS 1.4 flags and channel");
        //                    attributer.remove_attribute("LAS 1.4 classification");
        //                    attributer.remove_attribute("LAS 1.4 extended returns");
        //                    attributer.remove_attribute("LAS 1.4 scan angle");

        //                    // either rewrite or remove the "extra bytes" VLR
        //                    if (attributer.number_attributes)
        //                    {
        //                        if (laszip_add_vlr(laszip_dll, "LASF_Spec\0\0\0\0\0\0", 4, (laszip_U16)(attributer.number_attributes * sizeof(LASattribute)), 0, (laszip_U8*)attributer.attributes))
        //                        {
        //                            sprintf(this.error, "rewriting the extra bytes VLR without 'LAS 1.4 compatibility mode' attributes");
        //                            return 1;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        if (laszip_remove_vlr(laszip_dll, "LASF_Spec\0\0\0\0\0\0", 4))
        //                        {
        //                            sprintf(this.error, "removing the LAS 1.4 attribute VLR");
        //                            return 1;
        //                        }
        //                    }

        //                    // upgrade to LAS 1.4
        //                    if (this.Header.version_minor < 3)
        //                    {
        //                        // LAS 1.2 header is 148 bytes less than LAS 1.4+ header
        //                        this.Header.header_size += 148;
        //                        this.Header.offset_to_point_data += 148;
        //                    }
        //                    else
        //                    {
        //                        // LAS 1.3 header is 140 bytes less than LAS 1.4+ header
        //                        this.Header.header_size += 140;
        //                        this.Header.offset_to_point_data += 140;
        //                    }
        //                    this.Header.version_minor = 4;

        //                    // maybe turn on the bit indicating the presence of the OGC WKT
        //                    for (i = 0; i < this.Header.number_of_variable_length_records; i++)
        //                    {
        //                        if ((strncmp(this.Header.vlrs[i].user_id, "LASF_Projection", 16) == 0) && (this.Header.vlrs[i].record_id == 2112))
        //                        {
        //                            this.Header.global_encoding |= (1 << 4);
        //                            break;
        //                        }
        //                    }

        //                    // update point type and size
        //                    this.Point.extended_point_type = 1;
        //                    if (this.Header.point_data_format == 1)
        //                    {
        //                        this.Header.point_data_format = 6;
        //                        this.Header.point_data_record_length += (2 - 5); // record is 2 bytes larger but minus 5 extra bytes
        //                    }
        //                    else if (this.Header.point_data_format == 3)
        //                    {
        //                        if (this.start_NIR_band == -1)
        //                        {
        //                            this.Header.point_data_format = 7;
        //                            this.Header.point_data_record_length += (2 - 5); // record is 2 bytes larger but minus 5 extra bytes
        //                        }
        //                        else
        //                        {
        //                            this.Header.point_data_format = 8;
        //                            this.Header.point_data_record_length += (4 - 7); // record is 4 bytes larger but minus 7 extra bytes
        //                        }
        //                    }
        //                    else
        //                    {
        //                        if (this.start_NIR_band == -1)
        //                        {
        //                            this.Header.point_data_format = 9;
        //                            this.Header.point_data_record_length += (2 - 5);
        //                        }
        //                        else
        //                        {
        //                            this.Header.point_data_format = 10;
        //                            this.Header.point_data_record_length += (4 - 7);
        //                        }
        //                    }

        //                    // we are operating in compatibility mode
        //                    this.compatibility_mode = true;
        //                }
        //            }
        //        }
        //    }
        //    else if (this.Header.point_data_format > 5)
        //    {
        //        this.Point.extended_point_type = 1;
        //    }

        //    // create the point reader
        //    this.reader = new LASreadPoint(this.las14_decompress_selective);
        //    if (!this.reader.setup(laszip->num_items, laszip->items, laszip))
        //    {
        //        sprintf(this.error, "setup of LASreadPoint failed");
        //        return 1;
        //    }

        //    if (!this.reader.init(this.streamIn))
        //    {
        //        sprintf(this.error, "init of LASreadPoint failed");
        //        return 1;
        //    }

        //    // set the point number and point count
        //    this.nPoints = (this.Header.number_of_point_records ? this.Header.number_of_point_records : this.Header.extended_number_of_point_records);
        //    this.currentPointIndex = 0;

        //    return 0;
        //}

        //private int OpenReader(string file_name, out bool is_compressed)
        //{
        //    if (file_name == 0)
        //    {
        //        sprintf(this.error, "laszip_CHAR pointer 'file_name' is zero");
        //        return 1;
        //    }

        //    if (is_compressed == 0)
        //    {
        //        sprintf(this.error, "laszip_BOOL pointer 'is_compressed' is zero");
        //        return 1;
        //    }

        //    if (this.writer)
        //    {
        //        sprintf(this.error, "writer is already open");
        //        return 1;
        //    }

        //    if (this.reader)
        //    {
        //        sprintf(this.error, "reader is already open");
        //        return 1;
        //    }

        //    // open the file
        //    this.file = fopen(file_name, "rb");
        //    if (IS_LITTLE_ENDIAN())
        //        this.streamIn = new ByteStreamInFileLE(this.file);
        //    else
        //        this.streamIn = new ByteStreamInFileBE(this.file);

        //    // read the header variable after variable
        //    if (laszip_read_header(laszip_dll, is_compressed))
        //    {
        //        return 1;
        //    }

        //    // should we try to exploit existing spatial indexing information

        //    if (this.lax_exploit)
        //    {
        //        this.laxIndex = new LASindex();

        //        if (!this.laxIndex.read(file_name))
        //        {
        //            delete this.laxIndex;
        //            this.laxIndex = 0;
        //        }
        //    }

        //    return 0;
        //}

        //private int HasSpatialIndex(out bool is_indexed, bool is_appended)
        //{
        //    if (this.reader == 0)
        //    {
        //        sprintf(this.error, "reader is not open");
        //        return 1;
        //    }

        //    if (this.writer)
        //    {
        //        sprintf(this.error, "writer is already open");
        //        return 1;
        //    }

        //    if (this.lax_exploit == 0)
        //    {
        //        sprintf(this.error, "exploiting of spatial indexing not enabled before opening reader");
        //        return 1;
        //    }

        //    // check if reader found spatial indexing information when opening file
        //    if (this.laxIndex)
        //    {
        //        is_indexed = 1;
        //    }
        //    else
        //    {
        //        is_indexed = 0;
        //    }

        //    // optional: inform whether spatial index is appended to LAZ file or in separate LAX file

        //    if (is_appended)
        //    {
        //        is_appended = 0;
        //    }

        //    return 0;
        //}

        //private int IsInsideRectangle(double r_min_x, double r_min_y, double r_max_x, double r_max_y, out bool is_empty)
        //{
        //    if (this.reader == 0)
        //    {
        //        sprintf(this.error, "reader is not open");
        //        return 1;
        //    }

        //    if (is_empty == 0)
        //    {
        //        sprintf(this.error, "laszip_BOOL pointer 'is_empty' is zero");
        //        return 1;
        //    }

        //    if (this.lax_exploit == false)
        //    {
        //        sprintf(this.error, "exploiting of spatial indexing not enabled before opening reader");
        //        return 1;
        //    }

        //    this.lax_r_min_x = r_min_x;
        //    this.lax_r_min_y = r_min_y;
        //    this.lax_r_max_x = r_max_x;
        //    this.lax_r_max_y = r_max_y;

        //    if (this.laxIndex)
        //    {
        //        if (this.laxIndex.intersect_rectangle(r_min_x, r_min_y, r_max_x, r_max_y))
        //        {
        //            *is_empty = 0;
        //        }
        //        else
        //        {
        //            // no overlap between spatial indexing cells and query reactangle
        //            *is_empty = 1;
        //        }
        //    }
        //    else
        //    {
        //        if ((this.Header.min_x > r_max_x) || (this.Header.min_y > r_max_y) || (this.Header.max_x < r_min_x) || (this.Header.max_y < r_min_y))
        //        {
        //            // no overlap between header bouding box and query reactangle
        //            *is_empty = 1;
        //        }
        //        else
        //        {
        //            *is_empty = 0;
        //        }
        //    }

        //    return 0;
        //}

        public int SeekToPoint(long index)
        {
            // seek to the point
            if (this.reader.Seek((UInt32)this.currentPointIndex, (UInt32)index) == false)
            {
                throw new ArgumentOutOfRangeException("seeking from index " + this.currentPointIndex + " to index " + index + " for file with " + this.nPoints + " points");
            }

            this.currentPointIndex = index;
            return 0;
        }

        public void ReadPoint()
        {
            // read the point
            if (this.reader.TryRead(this.point_items) == false)
            {
                throw new FileLoadException("reading point " + this.currentPointIndex + " of " + this.nPoints + " total points");
            }

            // special recoding of points (in compatibility mode only)
            if (this.compatibility_mode)
            {
                Int16 scan_angle_remainder;
                byte extended_returns;
                byte classification;
                byte flags_and_channel;
                Int32 return_number_increment;
                Int32 number_of_returns_increment;
                Int32 overlap_bit;
                Int32 scanner_channel;

                // instill extended attributes
                LasPoint point = this.Point;

                // get extended attributes from extra bytes
                scan_angle_remainder = BinaryPrimitives.ReadInt16LittleEndian(point.ExtraBytes[this.start_scan_angle..]);
                extended_returns = point.ExtraBytes[this.start_extended_returns];
                classification = point.ExtraBytes[this.start_classification];
                flags_and_channel = point.ExtraBytes[this.start_flags_and_channel];
                if (this.start_NIR_band != -1)
                {
                    point.Rgb[3] = BinaryPrimitives.ReadUInt16LittleEndian(point.ExtraBytes[this.start_NIR_band..]);
                }

                // decompose into individual attributes
                return_number_increment = (extended_returns >> 4) & 0x0F;
                number_of_returns_increment = extended_returns & 0x0F;
                scanner_channel = (flags_and_channel >> 1) & 0x03;
                overlap_bit = flags_and_channel & 0x01;

                // instill into point
                point.ExtendedScanAngle = (Int16)(scan_angle_remainder + MyDefs.QuantizeInt16(((float)point.ScanAngleRank) / 0.006F));
                point.ExtendedReturnNumber = (byte)(return_number_increment + point.ReturnNumber);
                point.ExtendedNumberOfReturnsOfGivenPulse = (byte)(number_of_returns_increment + point.NumberOfReturnsOfGivenPulse);
                point.ExtendedClassification = (byte)(classification + point.ClassificationAndFlags);
                point.ExtendedScannerChannel = (byte)scanner_channel;
                point.ExtendedClassificationFlags = (byte)((overlap_bit << 3) | ((point.WithheldFlag) << 2) | ((point.KeypointFlag) << 1) | (point.SyntheticFlag));
            }

            this.currentPointIndex++;
        }

        private int ReadInsidePoint(out bool is_done)
        {
            double xy;
            is_done = true;
            if (this.laxIndex != null)
            {
                while (this.laxIndex.SeekNext(this.reader, this.currentPointIndex))
                {
                    if (this.reader.TryRead(this.point_items))
                    {
                        this.currentPointIndex++;
                        xy = this.Header.XScaleFactor * this.Point.X + this.Header.XOffset;
                        if (xy < this.laxRminX || xy >= this.laxRmaxX) 
                            continue;
                        xy = this.Header.YScaleFactor * this.Point.Y + this.Header.YOffset;
                        if (xy < this.laxRminY || xy >= this.laxRmaxY) 
                            continue;
                        is_done = false;
                        break;
                    }
                }
            }
            else
            {
                while (this.reader.TryRead(this.point_items))
                {
                    this.currentPointIndex++;
                    xy = this.Header.XScaleFactor * this.Point.X + this.Header.XOffset;
                    if (xy < this.laxRminX || xy >= this.laxRmaxX) 
                        continue;
                    xy = this.Header.YScaleFactor * this.Point.Y + this.Header.YOffset;
                    if (xy < this.laxRminY || xy >= this.laxRmaxY) 
                        continue;
                    is_done = false;
                    break;
                }

                if (is_done)
                {
                    if (this.currentPointIndex < this.nPoints)
                    {
                        throw new InvalidOperationException("reading point " + this.currentPointIndex + " of " + this.nPoints + " total points");
                    }
                }
            }

            return 0;
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

            if ((this.leaveStreamInOpen == false) && (this.streamIn != null))
            {
                this.streamIn.Close();
                this.streamIn = null;
            }

            this.reader = null;
            this.point_items = Array.Empty<byte>();
            this.laxIndex = null;
            return 0;
        }

        #region C++ ifdef __cplusplus
        //private int OpenReaderStream(Stream stream, bool is_compressed)
        //{
        //    if (is_compressed == 0)
        //    {
        //        sprintf(this.error, "laszip_BOOL pointer 'is_compressed' is zero");
        //        return 1;
        //    }

        //    if (this.writer)
        //    {
        //        sprintf(this.error, "writer is already open");
        //        return 1;
        //    }

        //    if (this.reader)
        //    {
        //        sprintf(this.error, "reader is already open");
        //        return 1;
        //    }

        //    // open the file

        //    if (IS_LITTLE_ENDIAN())
        //        this.streamIn = new ByteStreamInIstreamLE(stream);
        //    else
        //        this.streamIn = new ByteStreamInIstreamBE(stream);

        //    if (this.streamIn == 0)
        //    {
        //        sprintf(this.error, "could not alloc ByteStreamInIstream");
        //        return 1;
        //    }

        //    return laszip_read_header(laszip_dll, is_compressed);
        //}

        /*---------------------------------------------------------------------------*/
        // The stream writer also supports software that writes the LAS header on its
        // own simply by setting the BOOL 'do_not_write_header' to true. This function
        // should then be called just prior to writing points as data is then written
        // to the current stream position
        //private int OpenWriterStream(Stream stream, bool compress, bool do_not_write_header)
        //{
        //    if (this.writer)
        //    {
        //        sprintf(this.error, "writer is already open");
        //        return 1;
        //    }

        //    if (this.reader)
        //    {
        //        sprintf(this.error, "reader is already open");
        //        return 1;
        //    }

        //    // create the outstream

        //    if (IS_LITTLE_ENDIAN())
        //        this.streamout = new ByteStreamOutOstreamLE(stream);
        //    else
        //        this.streamout = new ByteStreamOutOstreamBE(stream);

        //    if (this.streamout == 0)
        //    {
        //        sprintf(this.error, "could not alloc ByteStreamOutOstream");
        //        return 1;
        //    }

        //    // setup the items that make up the point

        //    LasZip laszip;
        //    if (setup_laszip_items(laszip_dll, &laszip, compress))
        //    {
        //        return 1;
        //    }

        //    // this supports software that writes the LAS header on its own

        //    if (do_not_write_header == false)
        //    {
        //        // prepare header

        //        if (laszip_prepare_header_for_write(laszip_dll))
        //        {
        //            return 1;
        //        }

        //        // prepare point

        //        if (laszip_prepare_point_for_write(laszip_dll, compress))
        //        {
        //            return 1;
        //        }

        //        // prepare VLRs

        //        if (laszip_prepare_vlrs_for_write(laszip_dll))
        //        {
        //            return 1;
        //        }

        //        // write header variable after variable

        //        if (laszip_write_header(laszip_dll, &laszip, compress))
        //        {
        //            return 1;
        //        }
        //    }

        //    // create the point writer

        //    if (create_point_writer(laszip_dll, &laszip))
        //    {
        //        return 1;
        //    }

        //    // set the point number and point count

        //    this.nPoints = (this.Header.number_of_point_records ? this.Header.number_of_point_records : this.Header.extended_number_of_point_records);
        //    this.currentPointIndex = 0;
        //    return 0;
        //}

        /*---------------------------------------------------------------------------*/
        // creates complete LasZip VLR for currently selected point type and compression
        // The VLR data is valid until the laszip_dll pointer is destroyed.
        //private int CreateLasZipVlr(out byte[] vlr, out UInt32 vlr_size)
        //{
        //    LasZip laszip;
        //    if (setup_laszip_items(laszip_dll, &laszip, true))
        //    {
        //        return 1;
        //    }

        //    ByteStreamOutArray outArray = 0;

        //    if (IS_LITTLE_ENDIAN())
        //        outArray = new ByteStreamOutArrayLE();
        //    else
        //        outArray = new ByteStreamOutArrayBE();

        //    if (outArray == 0)
        //    {
        //        sprintf(this.error, "could not alloc ByteStreamOutArray");
        //        return 1;
        //    }

        //    if (write_laszip_vlr_header(laszip_dll, &laszip, outArray))
        //    {
        //        return 1;
        //    }

        //    if (write_laszip_vlr_payload(laszip_dll, &laszip, outArray))
        //    {
        //        return 1;
        //    }

        //    *vlr = (laszip_U8*)malloc(outArray.getSize());
        //    *vlr_size = (UInt32)outArray.getSize();
        //    this.buffers.push_back(*vlr);
        //    memcpy(*vlr, outArray.getData(), outArray.getSize());
        //    return 0;
        //}
        #endregion
    }
}
