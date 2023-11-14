// laszip_common_v3.hpp
using System;

namespace LasZip
{
    internal class LasContextPoint14
    {
        public bool unused { get; set; }

        public byte[]? last_item { get; set; } // [128]
        public UInt16[]? last_intensity { get; set; } // [8]
        public StreamingMedian5[]? last_X_diff_median5 { get; set; } // [12]
        public StreamingMedian5[]? last_Y_diff_median5 { get; set; } // [12]
        public Int32[]? last_Z { get; set; } // [8]

        public ArithmeticModel[]? m_changed_values { get; set; } // [8]
        public ArithmeticModel[]? m_scanner_channel { get; set; }
        public ArithmeticModel[]? m_number_of_returns { get; set; } // [16]
        public ArithmeticModel? m_return_number_gps_same { get; set; }
        public ArithmeticModel[]? m_return_number { get; set; } // [16]
        public IntegerCompressor? ic_dX { get; set; }
        public IntegerCompressor? ic_dY { get; set; }
        public IntegerCompressor? ic_Z { get; set; }

        public ArithmeticModel[]? m_classification { get; set; } // [64]
        public ArithmeticModel[]? m_flags { get; set; } // [64]
        public ArithmeticModel[]? m_user_data { get; set; } // [64]

        public IntegerCompressor? ic_intensity { get; set; }
        public IntegerCompressor? ic_scan_angle { get; set; }
        public IntegerCompressor? ic_point_source_ID { get; set; }

        // GPS time stuff
        public UInt32 last { get; set; }
        public UInt32 next { get; set; }
        public Interpretable64[]? last_gpstime { get; set; } // [4]
        public Int32[]? last_gpstime_diff { get; set; } // [4]
        public Int32[]? multi_extreme_counter { get; set; } // [4]

        public ArithmeticModel? m_gpstime_multi { get; set; }
        public ArithmeticModel? m_gpstime_0diff { get; set; }
        public IntegerCompressor? ic_gpstime { get; set; }
    }
}
