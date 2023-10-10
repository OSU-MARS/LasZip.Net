// lasinterval.{hpp, cpp}
using System;

namespace LasZip
{
    internal class LasIntervalCell
    {
        public UInt32 Start { get; set; }
        public UInt32 End { get; set; }
        public LasIntervalCell? Next { get; set; }

        public LasIntervalCell()
        {
            Start = 0;
            End = 0;
            Next = null;
        }

        public LasIntervalCell(UInt32 p_index)
        {
            Start = p_index;
            End = p_index;
            Next = null;
        }

        public LasIntervalCell(LasIntervalCell cell)
        {
            Start = cell.Start;
            End = cell.End;
            Next = null;
        }
    }
}
