// lasreaditemcompressed_v4.{hpp, cpp}
using System;
using System.IO;

namespace LasZip
{
    internal class LasReadItemCompressedByte14v4 : LasReadItemCompressed
    {
        /* not used as a decoder. just gives access to instream */
        private ArithmeticDecoder dec;

        private Stream[] instream_Bytes;

        private ArithmeticDecoder[] dec_Bytes;

        private UInt32 num_bytes_Bytes;

        private bool[] changed_Bytes;

        private bool[] requested_Bytes;

        private bool[] bytes;
        private UInt32 num_bytes_allocated;

        private UInt32 current_context;
        private LasContextByte14[] contexts; // [4]

        private UInt32 number;
    }
}
