// lasreaditemcompressed_v3.{hpp, cpp}
using System;
using System.IO;

namespace LasZip
{
    internal class LasReadItemCompressedRgbNir14v3 : LasReadItemCompressed
    {
        /* not used as a decoder. just gives access to instream */
        private ArithmeticDecoder dec;

        private Stream instream_RGB;
        private Stream instream_NIR;

        private ArithmeticDecoder dec_RGB;
        private ArithmeticDecoder dec_NIR;

        private bool changed_RGB;
        private bool changed_NIR;

        private UInt32 num_bytes_RGB;
        private UInt32 num_bytes_NIR;

        private bool requested_RGB;
        private bool requested_NIR;

        private byte[] bytes;
        private UInt32 num_bytes_allocated;

        private UInt32 current_context;
        private LasContextRgbNir14[] contexts; // [4]
    }
}
