// lasinterval.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class LasIntervalStartCell : LasIntervalCell
    {
        public UInt32 Full { get; set; }
        public UInt32 Total { get; set; }
        public LasIntervalCell? Last { get; set; }

        public LasIntervalStartCell()
        {
            Full = 0;
            Total = 0;
            Last = null;
        }

        public LasIntervalStartCell(UInt32 p_index)
            : base(p_index)
        {
            Full = 1;
            Total = 1;
            Last = null;
        }

        public bool Add(UInt32 p_index, UInt32 threshold)
        {
            UInt32 current_end = (Last != null ? Last.End : End);
            Debug.Assert(p_index > current_end);
            UInt32 diff = p_index - current_end;
            Full++;
            if (diff > threshold)
            {
                if (Last != null)
                {
                    Last.Next = new LasIntervalCell(p_index);
                    Last = Last.Next;
                }
                else
                {
                    Next = new LasIntervalCell(p_index);
                    Last = Next;
                }
                Total++;
                return true; // created new interval
            }
            if (Last != null)
            {
                Last.End = p_index;
            }
            else
            {
                End = p_index;
            }
            Total += diff;
            return false; // added to interval
        }
    }
}
