// lasreaditem.hpp
using System.IO;

namespace LasZip
{
    internal abstract class LasReadItemRaw : LasReadItem
    {
        protected Stream? InStream { get; private set; }

        public LasReadItemRaw()
        {
            this.InStream = null;
        }

        public bool Init(Stream inStream)
        {
            if (inStream == null) 
            { 
                return false; 
            }
            
            this.InStream = inStream;
            return true;
        }
    }
}
