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
            if (outStream == null)
            {
                return false;
            }

            this.OutStream = outStream;
            return true;
        }
    }
}
