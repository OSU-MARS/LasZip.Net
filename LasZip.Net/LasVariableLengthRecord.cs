// laszip_api.h
using System;

namespace LasZip
{
    public class LasVariableLengthRecord
    {
        public UInt16 Reserved { get; set; }
        public byte[] UserID { get; set; } = new byte[16];
        public UInt16 RecordID { get; set; }
        public UInt16 RecordLengthAfterHeader { get; set; }
        public byte[] Description { get; set; }
        public byte[]? Data { get; set; }

        public LasVariableLengthRecord()
        {
            this.UserID = new byte[16];
            this.Description = new byte[32];
        }
    }
}
