// lasreaditemcompressed_v4.{hpp, cpp}
using System;
using System.IO;

namespace LasZip
{
    internal class LasReadItemCompressedRgb14v4 : LasReadItemCompressed
    {
        /* not used as a decoder. just gives access to instream */
        private ArithmeticDecoder dec;
        private Stream instream_RGB;
        private ArithmeticDecoder dec_RGB;

        private bool changed_RGB;
        private UInt32 num_bytes_RGB;
        private bool requested_RGB;

        private byte[] bytes;
        private UInt32 num_bytes_allocated;

        private UInt32 current_context;
        private LasContextRgb14[] contexts; // [4];
    }
}
