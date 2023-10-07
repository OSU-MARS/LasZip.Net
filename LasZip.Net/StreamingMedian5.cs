//===============================================================================
//
//  FILE:  streamingmedian5.cs
//
//  CONTENTS:
//
//    Common defines and functionalities for version 2 of LASitemReadCompressed
//    and LASitemwriteCompressed.
//
//  PROGRAMMERS:
//
//    martin.isenburg@rapidlasso.com  -  http://rapidlasso.com
//
//  COPYRIGHT:
//
//    (c) 2005-2012, martin isenburg, rapidlasso - tools to catch reality
//    (c) of the C# port 2014 by Shinta <shintadono@googlemail.com>
//
//    This is free software; you can redistribute and/or modify it under the
//    terms of the GNU Lesser General Licence as published by the Free Software
//    Foundation. See the COPYING file for more information.
//
//    This software is distributed WITHOUT ANY WARRANTY and without even the
//    implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
//  CHANGE HISTORY: omitted for easier Copy&Paste (pls see the original)
//
//===============================================================================

namespace LasZip
{
    internal struct StreamingMedian5
    {
        public int Values0 { get; set; }
        public int Values1 { get; set; }
        public int Values2 { get; set; }
        public int Values3 { get; set; }
        public int Values4 { get; set; }
        public bool Low { get; set; }

        public void Init()
        {
            Values0 = Values1 = Values2 = Values3 = Values4 = 0;
            Low = false;
        }

        public void Add(int v)
        {
            if (!Low)
            {
                if (v < Values2)
                {
                    Values4 = Values3;
                    Values3 = Values2;
                    if (v < Values0)
                    {
                        Values2 = Values1;
                        Values1 = Values0;
                        Values0 = v;
                    }
                    else if (v < Values1)
                    {
                        Values2 = Values1;
                        Values1 = v;
                    }
                    else
                    {
                        Values2 = v;
                    }
                }
                else
                {
                    if (v < Values3)
                    {
                        Values4 = Values3;
                        Values3 = v;
                    }
                    else
                    {
                        Values4 = v;
                    }
                    Low = true;
                }
            }
            else
            {
                if (Values2 < v)
                {
                    Values0 = Values1;
                    Values1 = Values2;
                    if (Values4 < v)
                    {
                        Values2 = Values3;
                        Values3 = Values4;
                        Values4 = v;
                    }
                    else if (Values3 < v)
                    {
                        Values2 = Values3;
                        Values3 = v;
                    }
                    else
                    {
                        Values2 = v;
                    }
                }
                else
                {
                    if (Values1 < v)
                    {
                        Values0 = Values1;
                        Values1 = v;
                    }
                    else
                    {
                        Values0 = v;
                    }
                    Low = false;
                }
            }
        }

        public readonly int Get()
        {
            return Values2;
        }
    }
}
