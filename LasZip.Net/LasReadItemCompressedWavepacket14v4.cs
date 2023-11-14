// lasreaditemcompressed_v4.{hpp, cpp}
using System;
using System.IO;

namespace LasZip
{
    internal class LASreadItemCompressedWavepacket14v4 : LasReadItemCompressed
    {
        /* not used as a decoder. just gives access to instream */
        private ArithmeticDecoder dec;

        private Stream instream_wavepacket;

        private ArithmeticDecoder dec_wavepacket;

        private bool changed_wavepacket;

        private UInt32 num_bytes_wavepacket;

        private bool requested_wavepacket;

        private byte[] bytes;
        private UInt32 num_bytes_allocated;

        private UInt32 current_context;
        private LasContextWavepacket14[] contexts; // [4]
    }
}
