// laswriteitemraw.hpp
using System.IO;

namespace LasZip
{
    internal abstract class LasWriteItemRaw : LasWriteItem
    {
        protected Stream? OutStream { get; set; }

        protected LasWriteItemRaw()
        {
            this.OutStream = null;
        }

        public bool Init(Stream outStream)
        {
            this.OutStream = outStream;
            return true;
        }
    }
}
