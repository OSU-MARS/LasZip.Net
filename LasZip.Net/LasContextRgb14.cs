// laszip_common_v3.hpp
using System;

namespace LasZip
{
    internal class LasContextRgb14
    {
        public bool unused { get; set; }

        public UInt16[]? last_item { get; set; } // [3];

        public ArithmeticModel? m_byte_used { get; set; }
        public ArithmeticModel? m_rgb_diff_0 { get; set; }
        public ArithmeticModel? m_rgb_diff_1 { get; set; }
        public ArithmeticModel? m_rgb_diff_2 { get; set; }
        public ArithmeticModel? m_rgb_diff_3 { get; set; }
        public ArithmeticModel? m_rgb_diff_4 { get; set; }
        public ArithmeticModel? m_rgb_diff_5 { get; set; }
    }
}
