// laszip.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasZip
    {
        public static readonly Version Version;

        public const int CompressorNone = 0;
        public const int CompressorPointwise = 1;
        public const int CompressorPointwiseChunked = 2;
        public const int CompressorLayeredChunked = 3;
        public const int CompressorTotalNumberOf = 4;

        public const int CompressorChunked = CompressorPointwiseChunked;
        public const int CompressorNotChunked = CompressorPointwise;

        public const int CompressorDefault = CompressorChunked;

        public const int CoderArithmetic = 0;
        private const int CoderTotalNumberOf = 1;

        public const int ChunkSizeDefault = 50000;

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
            if (compressor < LasZip.CompressorTotalNumberOf) { return true; }
            return this.SetLastErrorWithConfiguredVersion(String.Format("compressor {0} not supported", compressor));
        }

        public bool CheckCoder(UInt16 coder)
        {
            if (coder < LasZip.CoderTotalNumberOf) { return true; }
            return this.SetLastErrorWithConfiguredVersion(String.Format("coder {0} not supported", coder));
        }

        public bool CheckItem(LasItem item)
        {
            switch (item.Type)
            {
                case LasItemType.Point10:
                    if (item.Size != 20) { return this.SetLastErrorWithConfiguredVersion("POINT10 has size != 20"); }
                    if (item.Version > 2) { return this.SetLastErrorWithConfiguredVersion("POINT10 has version > 2"); }
                    break;
                case LasItemType.Gpstime11:
                    if (item.Size != 8) { return this.SetLastErrorWithConfiguredVersion("GPSTIME11 has size != 8"); }
                    if (item.Version > 2) { return this.SetLastErrorWithConfiguredVersion("GPSTIME11 has version > 2"); }
                    break;
                case LasItemType.Rgb12:
                    if (item.Size != 6) { return this.SetLastErrorWithConfiguredVersion("RGB12 has size != 6"); }
                    if (item.Version > 2) { return this.SetLastErrorWithConfiguredVersion("RGB12 has version > 2"); }
                    break;
                case LasItemType.Byte:
                    if (item.Size < 1) { return this.SetLastErrorWithConfiguredVersion("BYTE has size < 1"); }
                    if (item.Version > 2) { return this.SetLastErrorWithConfiguredVersion("BYTE has version > 2"); }
                    break;
                case LasItemType.Point14:
                    if (item.Size != 30) { return this.SetLastErrorWithConfiguredVersion("POINT14 has size != 30"); }
                    if (item.Version > 0) { return this.SetLastErrorWithConfiguredVersion("POINT14 has version > 0"); }
                    break;
                case LasItemType.Rgb14:
                    if (item.Size != 6) { return this.SetLastErrorWithConfiguredVersion("RGB14 has size != 6"); }
                    if ((item.Version != 0) && (item.Version != 2) && (item.Version != 3) && (item.Version != 4)) { return this.SetLastErrorWithConfiguredVersion("RGB14 has version != 0 and != 2 and != 3 and != 4"); } // version == 2 from lasproto, version == 4 fixes context-switch
                    break;
                case LasItemType.RgbNir14:
                    if (item.Size != 8) { return this.SetLastErrorWithConfiguredVersion("RGBNIR14 has size != 8"); }
                    if (item.Version > 0) { return this.SetLastErrorWithConfiguredVersion("RGBNIR14 has version > 0"); }
                    break;
                case LasItemType.Byte14:
                    if (item.Size < 1) { return this.SetLastErrorWithConfiguredVersion("BYTE14 has size < 1"); }
                    if ((item.Version != 0) && (item.Version != 2) && (item.Version != 3) && (item.Version != 4)) { return this.SetLastErrorWithConfiguredVersion("BYTE14 has version != 0 and != 2 and != 3 and != 4"); } // version == 2 from lasproto, version == 4 fixes context-switch
                    break;
                case LasItemType.Wavepacket13:
                    if (item.Size != 29) { return this.SetLastErrorWithConfiguredVersion("WAVEPACKET13 has size != 29"); }
                    if (item.Version > 1) { return this.SetLastErrorWithConfiguredVersion("WAVEPACKET13 has version > 1"); }
                    break;
                case LasItemType.Wavepacket14:
                    if (item.Size != 29) { return this.SetLastErrorWithConfiguredVersion("WAVEPACKET14 has size != 29"); }
                    if ((item.Version != 0) && (item.Version != 3) && (item.Version != 4)) { return this.SetLastErrorWithConfiguredVersion("WAVEPACKET14 has version != 0 and != 3 and != 4"); } // version == 4 fixes context-switch
                    break;
                default:
                    return this.SetLastErrorWithConfiguredVersion(String.Format("item unknown ({0},{1},{2})", item.Type, item.Size, item.Version));
            }
            return true;
        }

        public bool CheckItems(UInt16 numItems, LasItem[] items, UInt16 pointSize = 0)
        {
            if (numItems == 0) { return this.SetLastErrorWithConfiguredVersion("number of items cannot be zero"); }
            if (items == null) { return this.SetLastErrorWithConfiguredVersion("items pointer cannot be NULL"); }
            UInt16 size = 0;
            for (int i = 0; i < numItems; i++)
            {
                if (!CheckItem(items[i])) { return false; }
                size += items[i].Size;
            }
            if ((pointSize != 0) && (pointSize != size))
            {
                return this.SetLastErrorWithConfiguredVersion("point has size of " + pointSize + " but items only add up to " + size + " bytes");
            }
            return true;
        }

        public bool Check(UInt16 pointSize = 0)
        {
            if (this.CheckCompressor(this.Compressor) == false) { return false; }
            if (this.CheckCoder(this.Coder) == false) { return false; }
            if (this.CheckItems(this.NumItems, this.Items, pointSize) == false) { return false; }
            return true;
        }

        public bool RequestCompatibilityMode(UInt16 requestedCompatibilityMode)
        {
            if (this.NumItems != 0)
            {
                throw new InvalidOperationException("request compatibility mode before calling setup()");
            }

            if (requestedCompatibilityMode > 1)
            {
                throw new InvalidOperationException("compatibility mode larger than 1 not supported");
            }
            if (requestedCompatibilityMode != 0)
            {
                this.Options |= 0x00000001;
            }
            else
            {
                this.Options &= 0xFFFFFFFE;
            }
            return true;
        }

        // go back and forth between item array and point type & size
        public bool Setup(out UInt16 numItems, out LasItem[]? items, byte pointType, UInt16 pointSize, UInt16 compressor = LasZip.CompressorNone)
        {
            numItems = 0;
            items = null;

            bool compatible = false;
            bool havePoint14 = false;
            bool haveGpsTime = false;
            bool haveRgb = false;
            bool haveNir = false;
            bool haveWavepacket = false;
            int extraBytesNumber;

            // turns on LAS 1.4 compatibility mode 
            if ((this.Options & 1) != 0)
            {
                compatible = true;
            }

            // switch over the point types we know
            switch (pointType)
            {
                case 0:
                    extraBytesNumber = (int)pointSize - 20;
                    break;
                case 1:
                    haveGpsTime = true;
                    extraBytesNumber = (int)pointSize - 28;
                    break;
                case 2:
                    haveRgb = true;
                    extraBytesNumber = (int)pointSize - 26;
                    break;
                case 3:
                    haveGpsTime = true;
                    haveRgb = true;
                    extraBytesNumber = (int)pointSize - 34;
                    break;
                case 4:
                    haveGpsTime = true;
                    haveWavepacket = true;
                    extraBytesNumber = (int)pointSize - 57;
                    break;
                case 5:
                    haveGpsTime = true;
                    haveRgb = true;
                    haveWavepacket = true;
                    extraBytesNumber = (int)pointSize - 63;
                    break;
                case 6:
                    havePoint14 = true;
                    extraBytesNumber = (int)pointSize - 30;
                    break;
                case 7:
                    havePoint14 = true;
                    haveRgb = true;
                    extraBytesNumber = (int)pointSize - 36;
                    break;
                case 8:
                    havePoint14 = true;
                    haveRgb = true;
                    haveNir = true;
                    extraBytesNumber = (int)pointSize - 38;
                    break;
                case 9:
                    havePoint14 = true;
                    haveWavepacket = true;
                    extraBytesNumber = (int)pointSize - 59;
                    break;
                case 10:
                    havePoint14 = true;
                    haveRgb = true;
                    haveNir = true;
                    haveWavepacket = true;
                    extraBytesNumber = (int)pointSize - 67;
                    break;
                default:
                    return this.SetLastErrorWithConfiguredVersion("point type " + pointType + " unknown");
            }

            if (extraBytesNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pointSize), "point size " + pointSize + " too small by " + -extraBytesNumber + " bytes for point type " + pointType + ".");
            }

            // maybe represent new LAS 1.4 as corresponding LAS 1.3 points plus extra bytes for compatibility
            if (havePoint14 && compatible)
            {
                // we need 4 extra bytes for the new point attributes
                extraBytesNumber += 5;
                // we store the GPS time separately
                haveGpsTime = true;
                // we do not use the point14 item
                havePoint14 = false;
                // if we have NIR ...
                if (haveNir)
                {
                    // we need another 2 extra bytes 
                    extraBytesNumber += 2;
                    // we do not use the NIR item
                    haveNir = false;
                }
            }
            
            // create item description
            numItems = (UInt16)(1 + (haveGpsTime ? 1 : 0) + (haveRgb ? 1 : 0) + (haveWavepacket ? 1 : 0) + (extraBytesNumber != 0 ? 1 : 0));
            items = new LasItem[numItems];

            UInt16 i = 1;
            if (havePoint14)
            {
                items[0] = new()
                {
                    Type = LasItemType.Point14,
                    Size = 30,
                    Version = 0
                };
            }
            else
            {
                items[0] = new()
                {
                    Type = LasItemType.Point10,
                    Size = 20,
                    Version = 0
                };
            }
            if (haveGpsTime)
            {
                items[i] = new()
                {
                    Type = LasItemType.Gpstime11,
                    Size = 8,
                    Version = 0
                };
                i++;
            }
            if (haveRgb)
            {
                if (havePoint14)
                {
                    items[i] = new();
                    if (haveNir)
                    {
                        items[i].Type = LasItemType.RgbNir14;
                        items[i].Size = 8;
                        items[i].Version = 0;
                    }
                    else
                    {
                        items[i].Type = LasItemType.Rgb14;
                        items[i].Size = 6;
                        items[i].Version = 0;
                    }
                }
                else
                {
                    items[i].Type = LasItemType.Rgb12;
                    items[i].Size = 6;
                    items[i].Version = 0;
                }
                i++;
            }
            if (haveWavepacket)
            {
                items[i] = new();
                if (havePoint14)
                {
                    items[i].Type = LasItemType.Wavepacket14;
                    items[i].Size = 29;
                    items[i].Version = 0;
                }
                else
                {
                    items[i].Type = LasItemType.Wavepacket13;
                    items[i].Size = 29;
                    items[i].Version = 0;
                };
                i++;
            }
            if (extraBytesNumber != 0)
            {
                items[i] = new();
                if (havePoint14)
                {
                    items[i].Type = LasItemType.Byte14;
                    items[i].Size = (UInt16)extraBytesNumber;
                    items[i].Version = 0;
                }
                else
                {
                    items[i].Type = LasItemType.Byte;
                    items[i].Size = (UInt16)extraBytesNumber;
                    items[i].Version = 0;
                };
                i++;
            }
            if (compressor != 0) { this.RequestVersion(2); }
            Debug.Assert(i == numItems);
            return true;
        }

        public bool IsStandard(out byte pointType, out UInt16 recordLength)
        {
            return this.IsStandard(this.NumItems, this.Items, out pointType, out recordLength);
        }

        public bool IsStandard(UInt16 numItems, LasItem[] items, out byte pointType, out UInt16 recordLength)
        {
            // this is always true
            pointType = 127;
            recordLength = 0;

            if (items == null) { return this.SetLastErrorWithConfiguredVersion("LASitem array is zero"); }

            for (int itemIndex = 0; itemIndex < numItems; itemIndex++) { recordLength += items[itemIndex].Size; }

            // the minimal number of items is 1
            if (numItems < 1) { return this.SetLastErrorWithConfiguredVersion("less than one LASitem entries"); }
            // the maximal number of items is 5
            if (numItems > 5) { return this.SetLastErrorWithConfiguredVersion("more than five LASitem entries"); }

            if (items[0].IsType(LasItemType.Point10))
            {
                // consider all the POINT10 combinations
                if (numItems == 1)
                {
                    pointType = 0;
                    Debug.Assert(recordLength == 20);
                    return true;
                }
                else
                {
                    if (items[1].IsType(LasItemType.Gpstime11))
                    {
                        if (numItems == 2)
                        {
                            pointType = 1;
                            Debug.Assert(recordLength == 28);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Rgb12))
                            {
                                if (numItems == 3)
                                {
                                    pointType = 3;
                                    Debug.Assert(recordLength == 34);
                                    return true;
                                }
                                else
                                {
                                    if (items[3].IsType(LasItemType.Wavepacket13))
                                    {
                                        if (numItems == 4)
                                        {
                                            pointType = 5;
                                            Debug.Assert(recordLength == 63);
                                            return true;
                                        }
                                        else
                                        {
                                            if (items[4].IsType(LasItemType.Byte))
                                            {
                                                if (numItems == 5)
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
                                        if (numItems == 4)
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
                                if (numItems == 3)
                                {
                                    pointType = 4;
                                    Debug.Assert(recordLength == 57);
                                    return true;
                                }
                                else
                                {
                                    if (items[3].IsType(LasItemType.Byte))
                                    {
                                        if (numItems == 4)
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
                                if (numItems == 3)
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
                        if (numItems == 2)
                        {
                            pointType = 2;
                            Debug.Assert(recordLength == 26);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Byte))
                            {
                                if (numItems == 3)
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
                        if (numItems == 2)
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
                if (numItems == 1)
                {
                    pointType = 6;
                    Debug.Assert(recordLength == 30);
                    return true;
                }
                else
                {
                    if (items[1].IsType(LasItemType.Rgb14))
                    {
                        if (numItems == 2)
                        {
                            pointType = 7;
                            Debug.Assert(recordLength == 36);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Byte) || items[2].IsType(LasItemType.Byte14))
                            {
                                if (numItems == 3)
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
                        if (numItems == 2)
                        {
                            pointType = 8;
                            Debug.Assert(recordLength == 38);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Wavepacket13) || items[2].IsType(LasItemType.Wavepacket14))
                            {
                                if (numItems == 3)
                                {
                                    pointType = 10;
                                    Debug.Assert(recordLength == 67);
                                    return true;
                                }
                                else
                                {
                                    if (items[3].IsType(LasItemType.Byte) || items[3].IsType(LasItemType.Byte14))
                                    {
                                        if (numItems == 4)
                                        {
                                            pointType = 10;
                                            Debug.Assert(recordLength == (67 + items[3].Size));
                                            return true;
                                        }
                                    }
                                }
                            }
                            else if (items[2].IsType(LasItemType.Byte) || items[2].IsType(LasItemType.Byte14))
                            {
                                if (numItems == 3)
                                {
                                    pointType = 8;
                                    Debug.Assert(recordLength == (38 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.Wavepacket13) || items[1].IsType(LasItemType.Wavepacket14))
                    {
                        if (numItems == 2)
                        {
                            pointType = 9;
                            Debug.Assert(recordLength == 59);
                            return true;
                        }
                        else
                        {
                            if (items[2].IsType(LasItemType.Byte) || items[2].IsType(LasItemType.Byte14))
                            {
                                if (numItems == 3)
                                {
                                    pointType = 9;
                                    Debug.Assert(recordLength == (59 + items[2].Size));
                                    return true;
                                }
                            }
                        }
                    }
                    else if (items[1].IsType(LasItemType.Byte) || items[1].IsType(LasItemType.Byte14))
                    {
                        if (numItems == 2)
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
                this.SetLastErrorWithConfiguredVersion("first LASitem is neither POINT10 nor POINT14");
            }
            return this.SetLastErrorWithConfiguredVersion("LASitem array does not match LAS specification 1.4");
        }

        // pack to and unpack from VLR
        public unsafe bool Unpack(byte[] bytes, int num)
        {
            // check input
            if (num < 34) { return this.SetLastErrorWithConfiguredVersion("too few bytes to unpack"); }
            if (((num - 34) % 6) != 0) { return this.SetLastErrorWithConfiguredVersion("wrong number bytes to unpack"); }
            if (((num - 34) / 6) == 0) { return this.SetLastErrorWithConfiguredVersion("zero items to unpack"); }
            this.NumItems = (UInt16)((num - 34) / 6);

            // create item list
            this.Items = new LasItem[this.NumItems];

            // do the unpacking
            UInt16 itemIndex;
            fixed (byte* pBytes = bytes)
            {
                byte* b = pBytes;
                this.Compressor = *((UInt16*)b);
                b += 2;
                this.Coder = *((UInt16*)b);
                b += 2;
                this.VersionMajor = *b;
                b += 1;
                this.VersionMinor = *b;
                b += 1;
                this.VersionRevision = *((UInt16*)b);
                b += 2;
                this.Options = *((UInt32*)b);
                b += 4;
                this.ChunkSize = *((UInt32*)b);
                b += 4;
                this.NumberOfSpecialEvlrs = *((Int64*)b);
                b += 8;
                this.OffsetToSpecialEvlrs = *((Int64*)b);
                b += 8;
                this.NumItems = *((UInt16*)b);
                b += 2;
                for (itemIndex = 0; itemIndex < this.NumItems; itemIndex++)
                {
                    this.Items[itemIndex].Type = (LasItemType)(int)*((UInt16*)b);
                    b += 2;
                    this.Items[itemIndex].Size = *((UInt16*)b);
                    b += 2;
                    this.Items[itemIndex].Version = *((UInt16*)b);
                    b += 2;
                }
                Debug.Assert((pBytes + num) == b);

                // check if we support the contents
                for (itemIndex = 0; itemIndex < this.NumItems; itemIndex++)
                {
                    if (!this.CheckItem(this.Items[itemIndex])) { return false; }
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
        public bool Setup(byte pointType, UInt16 pointSize, UInt16 compressor = LasZip.CompressorDefault)
        {
            if (!this.CheckCompressor(compressor)) { return false; }
            this.NumItems = 0;
            this.Items = null;
            if (!this.Setup(out this.NumItems, out this.Items, pointType, pointSize, compressor)) 
            { 
                return false; 
            }
            if (compressor != 0)
            {
                if (this.Items[0].Type == LasItemType.Point14)
                {
                    if (compressor != LasZip.CompressorLayeredChunked)
                    {
                        return false;
                    }
                    this.Compressor = LasZip.CompressorLayeredChunked;
                }
                else
                {
                    if (compressor == LasZip.CompressorLayeredChunked)
                    {
                        this.Compressor = LasZip.CompressorChunked;
                    }
                    else
                    {
                        this.Compressor = compressor;
                    }
                }
                if (compressor != LasZip.CompressorPointwise)
                {
                    if (this.ChunkSize == 0) { this.ChunkSize = LasZip.ChunkSizeDefault; }
                }
            }
            else
            {
                this.Compressor = LasZip.CompressorNone;
            }
            return true;
        }

        public bool Setup(UInt16 numItems, LasItem[] items, UInt16 compressor)
        {
            // check input
            if (!this.CheckCompressor(compressor)) { return false; }
            if (!this.CheckItems(numItems, items)) { return false; }

            // setup compressor
            this.Compressor = compressor;
            if (compressor != 0)
            {
                if (items[0].Type == LasItemType.Point14)
                {
                    if (compressor != LasZip.CompressorLayeredChunked)
                    {
                        return false;
                    }
                    this.Compressor = LasZip.CompressorLayeredChunked;
                }
                else
                {
                    if (compressor == LasZip.CompressorLayeredChunked)
                    {
                        this.Compressor = LasZip.CompressorChunked;
                    }
                    else
                    {
                        this.Compressor = compressor;
                    }
                }
                if (compressor != LasZip.CompressorPointwise)
                {
                    if (this.ChunkSize == 0) { this.ChunkSize = LasZip.ChunkSizeDefault; }
                }
            }
            else
            {
                this.Compressor = LasZip.CompressorNone;
            }

            // prepare items
            this.NumItems = 0;
            this.Items = null;
            this.NumItems = numItems;
            this.Items = new LasItem[numItems];

            // setup items
            for (int i = 0; i < numItems; i++)
            {
                this.Items[i] = items[i];
            }

            return true;
        }

        public bool SetChunkSize(UInt32 chunkSize) // for compressor only
        {
            if (this.NumItems == 0) { return this.SetLastErrorWithConfiguredVersion("call setup() before setting chunk size"); }
            if (this.Compressor != LasZip.CompressorPointwise)
            {
                this.ChunkSize = chunkSize;
                return true;
            }
            return false;
        }

        public bool RequestVersion(UInt16 requestedVersion) // for compressor only
        {
            if (this.NumItems == 0) { return this.SetLastErrorWithConfiguredVersion("call setup() before requesting version"); }
            if (this.Compressor == LasZip.CompressorNone)
            {
                if (requestedVersion > 0) { return this.SetLastErrorWithConfiguredVersion("without compression version is always 0"); }
            }
            else
            {
                if (requestedVersion < 1) { return this.SetLastErrorWithConfiguredVersion("with compression version is at least 1"); }
                if (requestedVersion > 2) { return this.SetLastErrorWithConfiguredVersion("version larger than 2 not supported"); }
            }
            for (int itemIndex = 0; itemIndex < this.NumItems; itemIndex++)
            {
                switch (this.Items[itemIndex].Type)
                {
                    case LasItemType.Point10:
                    case LasItemType.Gpstime11:
                    case LasItemType.Rgb12:
                    case LasItemType.Byte: 
                        this.Items[itemIndex].Version = requestedVersion; 
                        break;
                    case LasItemType.Wavepacket13: 
                        this.Items[itemIndex].Version = 1;  // no version 2
                        break;
                    case LasItemType.Point14:
                    case LasItemType.Rgb14:
                    case LasItemType.RgbNir14:
                    case LasItemType.Wavepacket14:
                    case LasItemType.Byte14:
                        this.Items[itemIndex].Version = 3; // no version 1 or 2
                        break;
                    default: 
                        return this.SetLastErrorWithConfiguredVersion("item type not supported");
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
