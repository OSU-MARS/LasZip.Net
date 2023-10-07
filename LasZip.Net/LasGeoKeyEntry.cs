// laszip_api.h
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LasGeoKeyEntry
    {
        public UInt16 KeyID { get; set; }
        public UInt16 TiffTagLocation { get; set; }
        public UInt16 Count { get; set; }
        public UInt16 ValueOffset { get; set; } // more accurately named ValueOrOffset but the LAS 1.4 spec uses Value_Offset

        public LasGeoKeyEntry()
        {
        }

        public void Parse(Span<byte> data)
        {
            if (data.Length != 8)
            {
                throw new ArgumentOutOfRangeException(nameof(data), "Data is " + data.Length + "bytes long instead of eight bytes.");
            }

            this.KeyID = BinaryPrimitives.ReadUInt16LittleEndian(data[..2]);
            this.TiffTagLocation = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
            this.Count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
            this.ValueOffset = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
        }
    }
}
