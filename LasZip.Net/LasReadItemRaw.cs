// lasreaditemraw.hpp
using System.IO;

namespace LasZip
{
    internal abstract class LasReadItemRaw : LasReadItem
    {
        protected Stream? inStream = null;

        public bool Init(Stream inStream)
        {
            if (inStream == null) { return false; }
            this.inStream = inStream;
            return true;
        }
    }
}
