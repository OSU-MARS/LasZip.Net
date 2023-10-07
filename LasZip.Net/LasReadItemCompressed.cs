// lasreaditem.hpp
namespace LasZip
{
    abstract class LasReadItemCompressed : LasReadItem
    {
        public abstract bool Init(LasPoint item);
    }
}
