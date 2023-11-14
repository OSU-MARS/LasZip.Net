// laszip_common_v3.hpp
using System;

namespace LasZip
{
    internal class LasContextWavepacket14
    {
        public bool unused { get; set; }

        public byte[]? last_item { get; set; } // [29]
        public Int32 last_diff_32 { get; set; }
        public UInt32 sym_last_offset_diff { get; set; }

        public ArithmeticModel? m_packet_index { get; set; }
        public ArithmeticModel[]? m_offset_diff { get; set; }
        public IntegerCompressor? ic_offset_diff { get; set; }
        public IntegerCompressor? ic_packet_size { get; set; }
        public IntegerCompressor? ic_return_point { get; set; }
        public IntegerCompressor? ic_xyz { get; set; }
    }
}
