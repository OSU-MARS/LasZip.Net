// laszip.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasZip
    {
        private static readonly Version Version;

        public const int CompressorNone = 0;
        public const int CompressorPointwise = 1;
        public const int CompressorPointwiseChunked = 2;
        public const int CompressorTotalNumberOf = 3;

        public const int CompressorChunked = CompressorPointwiseChunked;
        public const int CompressorNotChunked = CompressorPointwise;

        public const int CompressorDefault = CompressorChunked;

        public const int CoderArithmetic = 0;
        private const int CoderTotalNumberOf = 1;
        private const int ChunkSizeDefault = 50000;

        private string? errorString;

        // in case a function returns false this string describes the problem
        public string? GetError() { return errorString; }

        public byte[]? Bytes { get; private set; }

        // stored in LASzip VLR data section
        public UInt16 Compressor { get; set; }
        public UInt16 Coder { get; set; }
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }
        public UInt16 VersionRevision { get; set; }
        public UInt32 Options { get; set; }
        public UInt32 ChunkSize { get; set; }
        public long NumberOfSpecialEvlrs { get; set; } // must be -1 if unused
        public long OffsetToSpecialEvlrs { get; set; } // must be -1 if unused
        public UInt16 NumItems;
        public LasItem[]? Items;

        static LasZip()
        {
            Version? version = typeof(LasZip).Assembly.GetName().Version;
            if (version == null)
            {
                throw new InvalidOperationException("Assembly version not found for LasZip.Net.dll.");
            }
            LasZip.Version = version;
        }

        public LasZip()
        {
            this.Compressor = CompressorDefault;
            this.Coder = CoderArithmetic;
            this.VersionMajor = (byte)LasZip.Version.Major;
            this.VersionMinor = (byte)LasZip.Version.Minor;
            this.VersionRevision = 0;
            this.Options = 0;
            this.NumItems = 0;
            this.ChunkSize = LasZip.ChunkSizeDefault;
            this.NumberOfSpecialEvlrs = -1;
            this.OffsetToSpecialEvlrs = -1;
            this.errorString = null;
            this.Items = null;
            this.Bytes = null;
        }

        // supported version control
        public bool CheckCompressor(UInt16 compressor)
        {
            if (compressor < CompressorTotalNumberOf) return true;
            return SetLastErrorWithConfiguredVersion(String.Format("compressor {0} not supported", compressor));
        }

        public bool CheckCoder(UInt16 coder)
        {
            if (coder < CoderTotalNumberOf) return true;
            return SetLastErrorWithConfiguredVersion(String.Format("coder {0} not supported", coder));
        }

        public bool CheckItem(LasItem item)
        {
            switch (item.Type)
            {
                case LasItemType.Point10:
                    if (item.Size != 20) return SetLastErrorWithConfiguredVersion("POINT10 has size != 20");
                    if (item.Version > 2) return SetLastErrorWithConfiguredVersion("POINT10 has version > 2");
                    break;
                case LasItemType.Gpstime11:
                    if (item.Size != 8) return SetLastErrorWithConfiguredVersion("GPSTIME11 has size != 8");
                    if (item.Version > 2) return SetLastErrorWithConfiguredVersion("GPSTIME11 has version > 2");
                    break;
                case LasItemType.Rgb12:
                    if (item.Size != 6) return SetLastErrorWithConfiguredVersion("RGB12 has size != 6");
                    if (item.Version > 2) return SetLastErrorWithConfiguredVersion("RGB12 has version > 2");
                    break;
                case LasItemType.Wavepacket13:
                    if (item.Size != 29) return SetLastErrorWithConfiguredVersion("WAVEPACKET13 has size != 29");
                    if (item.Version > 1) return SetLastErrorWithConfiguredVersion("WAVEPACKET13 has version > 1");
                    break;
                case LasItemType.Byte:
                    if (item.Size < 1) return SetLastErrorWithConfiguredVersion("BYTE has size < 1");
                    if (item.Version > 2) return SetLastErrorWithConfiguredVersion("BYTE has version > 2");
                    break;
                case LasItemType.Point14:
                    if (item.Size != 30) return SetLastErrorWithConfiguredVersion("POINT14 has size != 30");
                    if (item.Version > 0) return SetLastErrorWithConfiguredVersion("POINT14 has version > 0");
                    break;
                case LasItemType.RgbNir14:
                    if (item.Size != 8) return SetLastErrorWithConfiguredVersion("RGBNIR14 has size != 8");
                    if (item.Version > 0) return SetLastErrorWithConfiguredVersion("RGBNIR14 has version > 0");
                    break;
                default:
                    if (true)
                        return SetLastErrorWithConfiguredVersion(String.Format("item unknown ({0},{1},{2})", item.Type, item.Size, item.Version));
            }
            return true;
        }

        public bool CheckItems(UInt16 num_items, LasItem[] items)
        {
            if (num_items == 0) return SetLastErrorWithConfiguredVersion("number of items cannot be zero");
            if (items == null) return SetLastErrorWithConfiguredVersion("items pointer cannot be NULL");
            for (int i = 0; i < num_items; i++)
            {
                if (!CheckItem(items[i])) return false;
            }
            return true;
        }

        public bool Check()
        {
            if (this.CheckCompressor(this.Compressor) == false) { return false; }
            if (this.CheckCoder(this.Coder) == false) { return false; }
            if (this.CheckItems(this.NumItems, this.Items) == false) { return false; }
            return true;
        }

        // go back and forth between item array and point type & size
        public bool Setup(out UInt16 numItems, out LasItem[]? items, byte pointType, UInt16 pointSize, UInt16 compressor = CompressorNone)
        {
            numItems = 0;
            items = null;

            bool have_point14 = false;
            bool have_gps_time = false;
            bool have_rgb = false;
            bool have_nir = false;
            bool have_wavepacket = false;
            int extra_bytes_number = 0;

            // switch over the point types we know
            switch (pointType)
            {
                case 0:
                    extra_bytes_number = (int)pointSize - 20;
                    break;
                case 1:
                    have_gps_time = true;
                    extra_bytes_number = (int)pointSize - 28;
                    break;
                case 2:
                    have_rgb = true;
                    extra_bytes_number = (int)pointSize - 26;
                    break;
                case 3:
                    have_gps_time = true;
                    have_rgb = true;
                    extra_bytes_number = (int)pointSize - 34;
                    break;
                case 4:
                    have_gps_time = true;
                    have_wavepacket = true;
                    extra_bytes_number = (int)pointSize - 57;
                    break;
                case 5:
                    have_gps_time = true;
                    have_rgb = true;
                    have_wavepacket = true;
                    extra_bytes_number = (int)pointSize - 63;
                    break;
                case 6:
                    have_point14 = true;
                    extra_bytes_number = (int)pointSize - 30;
                    break;
                case 7:
                    have_point14 = true;
                    have_rgb = true;
                    extra_bytes_number = (int)pointSize - 36;
                    break;
                case 8:
                    have_point14 = true;
                    have_rgb = true;
                    have_nir = true;
                    extra_bytes_number = (int)pointSize - 38;
                    break;
                case 9:
                    have_point14 = true;
                    have_wavepacket = true;
                    extra_bytes_number = (int)pointSize - 59;
                    break;
                case 10:
                    have_point14 = true;
                    have_rgb = true;
                    have_nir = true;
                    have_wavepacket = true;
                    extra_bytes_number = (int)pointSize - 67;
                    break;
                default:
                    if (true)
                        return SetLastErrorWithConfiguredVersion(String.Format("point type {0} unknown", pointType));
            }

            if (extra_bytes_number < 0)
            {
                Console.Error.WriteLine("WARNING: point size {0} too small by {1} bytes for point type {2}. assuming point_size of {3}", pointSize, -extra_bytes_number, pointType, pointSize - extra_bytes_number);
                extra_bytes_number = 0;
            }

            // create item description

            numItems = (UInt16)(1 + (have_gps_time ? 1 : 0) + (have_rgb ? 1 : 0) + (have_wavepacket ? 1 : 0) + (extra_bytes_number != 0 ? 1 : 0));
            items = new LasItem[numItems];

            UInt16 i = 1;
            if (have_point14)
            {
                items[0] = new LasItem();
                items[0].Type = LasItemType.Point14;
                items[0].Size = 30;
                items[0].Version = 0;
            }
            else
            {
                items[0] = new LasItem();
                items[0].Type = LasItemType.Point10;
                items[0].Size = 20;
                items[0].Version = 0;
            }
            if (have_gps_time)
            {
                items[i] = new LasItem();
                items[i].Type = LasItemType.Gpstime11;
                items[i].Size = 8;
                items[i].Version = 0;
                i++;
            }
            if (have_rgb)
            {
                items[i] = new LasItem();
                if (have_nir)
                {
                    items[i].Type = LasItemType.RgbNir14;
                    items[i].Size = 8;
                    items[i].Version = 0;
                }
                else
                {
                    items[i].Type = LasItemType.Rgb12;
                    items[i].Size = 6;
                    items[i].Version = 0;
                }
                i++;
            }
            if (have_wavepacket)
            {
                items[i] = new LasItem();
                items[i].Type = LasItemType.Wavepacket13;
                items[i].Size = 29;
                items[i].Version = 0;
                i++;
            }
            if (extra_bytes_number != 0)
            {
                items[i] = new LasItem();
                items[i].Type = LasItemType.Byte;
                items[i].Size = (UInt16)extra_bytes_number;
                items[i].Version = 0;
                i++;
            }
            if (compressor != 0) RequestVersion(2);
            Debug.Assert(i == numItems);
            return true;
        }

        public bool IsStandard(UInt16 num_items, LasItem[] items, out byte pointType, out UInt16 recordLength)
        {
            // this is always true
            pointType = 127;
            recordLength = 0;

            if (items == null) return SetLastErrorWithConfiguredVersion("LASitem array is zero");

            for (int i = 0; i < num_items; i++) recordLength += items[i].Size;

            // the minimal number of items is 1
            if (num_items < 1) return SetLastErrorWithConfiguredVersion("less than one LASitem entries");
            // the maximal number of items is 5
            if (num_items > 5) return SetLastErrorWithConfiguredVersion("more than five LASitem entries");

            if (items[0].IsType(LasItemType.Point10))
            {
                // consider all the POINT10 combinations
                if (num_items == 1)
                {
                    pointType = 0;
                    Debug.Assert(recordLength == 20);
                    return true;
                }
                else
                {
                    if (items[1].IsType(LasItemType.Gpstime11))
                    {
                        if (num_items == 2)
                        {
                            pointType = 1;
                            Debug.Assert(recordLength == 28);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Rgb12))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 3;
                                    Debug.Assert(recordLength == 34);
                                    return true;
                                }
                                else
                                {
                                    if (items[3].IsType(LasItemType.Wavepacket13))
                                    {
                                        if (num_items == 4)
                                        {
                                            pointType = 5;
                                            Debug.Assert(recordLength == 63);
                                            return true;
                                        }
                                        else
                                        {
                                            if (items[4].IsType(LasItemType.Byte))
                                            {
                                                if (num_items == 5)
                                                {
                                                    pointType = 5;
                                                    Debug.Assert(recordLength == (63 + items[4].Size));
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                    else if (items[3].IsType(LasItemType.Byte))
                                    {
                                        if (num_items == 4)
                                        {
                                            pointType = 3;
                                            Debug.Assert(recordLength == (34 + items[3].Size));
                                            return true;
                                        }
                                    }
                                }
                            }
                            else if (items[2].IsType(LasItemType.Wavepacket13))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 4;
                                    Debug.Assert(recordLength == 57);
                                    return true;
                                }
                                else
                                {
                                    if (items[3].IsType(LasItemType.Byte))
                                    {
                                        if (num_items == 4)
                                        {
                                            pointType = 4;
                                            Debug.Assert(recordLength == (57 + items[3].Size));
                                            return true;
                                        }
                                    }
                                }
                            }
                            else if (items[2].IsType(LasItemType.Byte))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 1;
                                    Debug.Assert(recordLength == (28 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.Rgb12))
                    {
                        if (num_items == 2)
                        {
                            pointType = 2;
                            Debug.Assert(recordLength == 26);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Byte))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 2;
                                    Debug.Assert(recordLength == (26 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.Byte))
                    {
                        if (num_items == 2)
                        {
                            pointType = 0;
                            Debug.Assert(recordLength == (20 + items[1].Size));
                            return true;
                        }
                    }
                }
            }
            else if (items[0].IsType(LasItemType.Point14))
            {
                // consider all the POINT14 combinations
                if (num_items == 1)
                {
                    pointType = 6;
                    Debug.Assert(recordLength == 30);
                    return true;
                }
                else
                {
                    if (items[1].IsType(LasItemType.Rgb12))
                    {
                        if (num_items == 2)
                        {
                            pointType = 7;
                            Debug.Assert(recordLength == 36);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Byte))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 7;
                                    Debug.Assert(recordLength == (36 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.RgbNir14))
                    {
                        if (num_items == 2)
                        {
                            pointType = 8;
                            Debug.Assert(recordLength == 38);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Wavepacket13))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 10;
                                    Debug.Assert(recordLength == 67);
                                    return true;
                                }
                                else
                                {
                                    if (items[3].IsType(LasItemType.Byte))
                                    {
                                        if (num_items == 4)
                                        {
                                            pointType = 10;
                                            Debug.Assert(recordLength == (67 + items[3].Size));
                                            return true;
                                        }
                                    }
                                }
                            }
                            else if (items[2].IsType(LasItemType.Byte))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 8;
                                    Debug.Assert(recordLength == (38 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.Wavepacket13))
                    {
                        if (num_items == 2)
                        {
                            pointType = 9;
                            Debug.Assert(recordLength == 59);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Byte))
                            {
                                if (num_items == 3)
                                {
                                    pointType = 9;
                                    Debug.Assert(recordLength == (59 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.Byte))
                    {
                        if (num_items == 2)
                        {
                            pointType = 6;
                            Debug.Assert(recordLength == (30 + items[1].Size));
                            return true;
                        }
                    }
                }
            }
            else
            {
                SetLastErrorWithConfiguredVersion("first LASitem is neither POINT10 nor POINT14");
            }
            return SetLastErrorWithConfiguredVersion("LASitem array does not match LAS specification 1.4");
        }

        public bool IsStandard(out byte pointType, out UInt16 recordLength)
        {
            return this.IsStandard(NumItems, this.Items, out pointType, out recordLength);
        }

        // pack to and unpack from VLR
        public unsafe bool Unpack(byte[] bytes, int num)
        {
            // check input
            if (num < 34) return SetLastErrorWithConfiguredVersion("too few bytes to unpack");
            if (((num - 34) % 6) != 0) return SetLastErrorWithConfiguredVersion("wrong number bytes to unpack");
            if (((num - 34) / 6) == 0) return SetLastErrorWithConfiguredVersion("zero items to unpack");
            NumItems = (UInt16)((num - 34) / 6);

            // create item list
            Items = new LasItem[NumItems];

            // do the unpacking
            UInt16 i;
            fixed (byte* pBytes = bytes)
            {
                byte* b = pBytes;
                Compressor = *((UInt16*)b);
                b += 2;
                Coder = *((UInt16*)b);
                b += 2;
                VersionMajor = *b;
                b += 1;
                VersionMinor = *b;
                b += 1;
                VersionRevision = *((UInt16*)b);
                b += 2;
                Options = *((UInt32*)b);
                b += 4;
                ChunkSize = *((UInt32*)b);
                b += 4;
                NumberOfSpecialEvlrs = *((Int64*)b);
                b += 8;
                OffsetToSpecialEvlrs = *((Int64*)b);
                b += 8;
                NumItems = *((UInt16*)b);
                b += 2;
                for (i = 0; i < NumItems; i++)
                {
                    Items[i].Type = (LasItemType)(int)*((UInt16*)b);
                    b += 2;
                    Items[i].Size = *((UInt16*)b);
                    b += 2;
                    Items[i].Version = *((UInt16*)b);
                    b += 2;
                }
                Debug.Assert((pBytes + num) == b);

                // check if we support the contents
                for (i = 0; i < NumItems; i++)
                {
                    if (!CheckItem(Items[i])) return false;
                }
                return true;
            }
        }

        public unsafe bool Pack(out byte[]? bytes, ref int num)
        {
            bytes = null;
            num = 0;

            // check if we support the contents
            if (!Check()) return false;

            // prepare output
            num = 34 + 6 * NumItems;
            this.Bytes = bytes = new byte[num];

            // pack
            UInt16 i;
            fixed (byte* pBytes = bytes)
            {
                byte* b = pBytes;
                *((UInt16*)b) = Compressor;
                b += 2;
                *((UInt16*)b) = Coder;
                b += 2;
                *b = VersionMajor;
                b += 1;
                *b = VersionMinor;
                b += 1;
                *((UInt16*)b) = VersionRevision;
                b += 2;
                *((UInt32*)b) = Options;
                b += 4;
                *((UInt32*)b) = ChunkSize;
                b += 4;
                *((Int64*)b) = NumberOfSpecialEvlrs;
                b += 8;
                *((Int64*)b) = OffsetToSpecialEvlrs;
                b += 8;
                *((UInt16*)b) = NumItems;
                b += 2;
                for (i = 0; i < NumItems; i++)
                {
                    *((UInt16*)b) = (UInt16)this.Items[i].Type;
                    b += 2;
                    *((UInt16*)b) = this.Items[i].Size;
                    b += 2;
                    *((UInt16*)b) = this.Items[i].Version;
                    b += 2;
                }
                Debug.Assert((pBytes + num) == b);
                return true;
            }
        }

        // setup
        public bool Setup(byte point_type, UInt16 point_size, UInt16 compressor = CompressorDefault)
        {
            if (!CheckCompressor(compressor)) return false;
            NumItems = 0;
            Items = null;
            if (!Setup(out NumItems, out Items, point_type, point_size, compressor)) return false;
            this.Compressor = compressor;
            if (this.Compressor == CompressorPointwiseChunked)
            {
                if (ChunkSize == 0) ChunkSize = ChunkSizeDefault;
            }
            return true;
        }

        public bool Setup(UInt16 num_items, LasItem[] items, UInt16 compressor)
        {
            // check input
            if (!CheckCompressor(compressor)) return false;
            if (!CheckItems(num_items, items)) return false;

            // setup compressor
            this.Compressor = compressor;
            if (this.Compressor == CompressorPointwiseChunked)
            {
                if (ChunkSize == 0) ChunkSize = ChunkSizeDefault;
            }

            // prepare items
            this.NumItems = 0;
            this.Items = null;
            this.NumItems = num_items;
            this.Items = new LasItem[num_items];

            // setup items
            for (int i = 0; i < num_items; i++)
            {
                this.Items[i] = items[i];
            }

            return true;
        }

        public bool SetChunkSize(UInt32 chunkSize) // for compressor only
        {
            if (NumItems == 0) return SetLastErrorWithConfiguredVersion("call setup() before setting chunk size");
            if (Compressor == CompressorPointwiseChunked)
            {
                this.ChunkSize = chunkSize;
                return true;
            }
            return false;
        }

        public bool RequestVersion(UInt16 requested_version) // for compressor only
        {
            if (NumItems == 0) return SetLastErrorWithConfiguredVersion("call setup() before requesting version");
            if (Compressor == CompressorNone)
            {
                if (requested_version > 0) return SetLastErrorWithConfiguredVersion("without compression version is always 0");
            }
            else
            {
                if (requested_version < 1) return SetLastErrorWithConfiguredVersion("with compression version is at least 1");
                if (requested_version > 2) return SetLastErrorWithConfiguredVersion("version larger than 2 not supported");
            }
            for (int i = 0; i < NumItems; i++)
            {
                switch (this.Items[i].Type)
                {
                    case LasItemType.Point10:
                    case LasItemType.Gpstime11:
                    case LasItemType.Rgb12:
                    case LasItemType.Byte: Items[i].Version = requested_version; break;
                    case LasItemType.Wavepacket13: Items[i].Version = 1; break; // no version 2
                    default: return SetLastErrorWithConfiguredVersion("item type not supported");
                }
            }
            
            return true;
        }

        public static string GetAssemblyVersionString()
        {
            return "LasZip.Net " + LasZip.Version.Major + "." + LasZip.Version.Minor + " r0 (" + LasZip.Version.Build + LasZip.Version.Revision + ")";
        }

        private bool SetLastErrorWithConfiguredVersion(string error)
        {
            this.errorString = error + "(LasZip.Net " + this.VersionMajor + "." +  this.VersionMinor + " r" + this.VersionRevision + ")"; // no build number available
            return false;
        }
    }
}
