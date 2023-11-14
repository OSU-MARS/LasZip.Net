// lasreaditemcompressed_v4.{hpp, cpp}
using System;
using System.IO;

namespace LasZip
{
    internal class LasReadItemCompressedPoint14v4 : LasReadItemCompressed
    {
        /* not used as a decoder. just gives access to instream */
        private ArithmeticDecoder dec;

        private Stream instream_channel_returns_XY;
        private Stream instream_Z;
        private Stream instream_classification;
        private Stream instream_flags;
        private Stream instream_intensity;
        private Stream instream_scan_angle;
        private Stream instream_user_data;
        private Stream instream_point_source;
        private Stream instream_gps_time;

        private ArithmeticDecoder dec_channel_returns_XY;
        private ArithmeticDecoder dec_Z;
        private ArithmeticDecoder dec_classification;
        private ArithmeticDecoder dec_flags;
        private ArithmeticDecoder dec_intensity;
        private ArithmeticDecoder dec_scan_angle;
        private ArithmeticDecoder dec_user_data;
        private ArithmeticDecoder dec_point_source;
        private ArithmeticDecoder dec_gps_time;

        private bool changed_Z;
        private bool changed_classification;
        private bool changed_flags;
        private bool changed_intensity;
        private bool changed_scan_angle;
        private bool changed_user_data;
        private bool changed_point_source;
        private bool changed_gps_time;

        private UInt32 num_bytes_channel_returns_XY;
        private UInt32 num_bytes_Z;
        private UInt32 num_bytes_classification;
        private UInt32 num_bytes_flags;
        private UInt32 num_bytes_intensity;
        private UInt32 num_bytes_scan_angle;
        private UInt32 num_bytes_user_data;
        private UInt32 num_bytes_point_source;
        private UInt32 num_bytes_gps_time;

        private bool requested_Z;
        private bool requested_classification;
        private bool requested_flags;
        private bool requested_intensity;
        private bool requested_scan_angle;
        private bool requested_user_data;
        private bool requested_point_source;
        private bool requested_gps_time;

        private byte[] bytes;
        private UInt32 num_bytes_allocated;

        private UInt32 current_context;
        private LasContextPoint14[] contexts; // [4]
    }
}
