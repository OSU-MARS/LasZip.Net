using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LasGeoKeys
    {
        public UInt16 KeyDirectoryVersion { get; set; }
        public UInt16 KeyRevision { get; set; }
        public UInt16 MinorRevision { get; set; }
        public UInt16 NumberOfKeys { get; set; }

        public LasGeoKeyEntry[]? Keys { get; set; }

        public LasGeoKeys()
        {
        }

        public LasGeoKeys(Span<byte> data)
        {
            if (data.Length < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(data), "A minimum of eight bytes is required for a GeoKeys variable length record but data has only " + data.Length + " bytes.");
            }

            // NumberOfKeys may be too high, resulting in , and not all of data may be used
            this.KeyDirectoryVersion = BinaryPrimitives.ReadUInt16LittleEndian(data[..2]);
            this.KeyRevision = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
            this.MinorRevision = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
            this.NumberOfKeys = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));

            this.Keys = new LasGeoKeyEntry[this.NumberOfKeys];
            int keyReadOffset = 8;
            for (int keyIndex = 0; keyIndex < this.NumberOfKeys; ++keyIndex)
            {
                ref LasGeoKeyEntry geoKey = ref this.Keys[keyIndex];
                geoKey.Parse(data.Slice(keyReadOffset, 8));
                switch (geoKey.TiffTagLocation)
                {
                    case 0:
                        keyReadOffset += 8; // nothing to do since key data is in geoKey.ValueOffset
                        break;
                    case 34736:
                        throw new NotSupportedException("GeoDoubleParamsTag encountered.");
                    case 34737:
                        // string tiffTags = Encoding.ASCII.GetString(data.Slice(geoKey.ValueOffset, geoKey.Count)); // ?
                        throw new NotSupportedException("GeoAsciiParamsTag encountered.");
                    default:
                        throw new NotSupportedException("Unknown TIFFTagLocation " + geoKey.TiffTagLocation + " encountered at read offset " + keyReadOffset + ".");
                }
            }
        }
    }
}
