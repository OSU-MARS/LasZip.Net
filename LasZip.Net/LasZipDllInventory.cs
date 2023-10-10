// laszip_dll.cpp
using System;

namespace LasZip
{
    public class LasZipDllInventory
    {
        private bool noPointsAdded;

        public UInt32 NumberOfPointRecords { get; set; }
        public UInt32[] NumberOfPointsByReturn { get; private init; }
        public int MaxX { get; set; }
        public int MinX { get; set; }
        public int MaxY { get; set; }
        public int MinY { get; set; }
        public int MaxZ { get; set; }
        public int MinZ { get; set; }

        public LasZipDllInventory()
        {
            this.NumberOfPointsByReturn = new UInt32[8]; // left as zero
            this.NumberOfPointRecords = 0;
            this.MaxX = 0;
            this.MinX = 0;
            this.MaxY = 0; 
            this.MinY = 0;
            this.MaxZ = 0; 
            this.MinZ = 0;
            this.noPointsAdded = true;
        }

        public bool Active() 
        { 
            return this.noPointsAdded == false; 
        }

        public void Add(LasPoint point)
        {
            this.NumberOfPointRecords++;
            this.NumberOfPointsByReturn[point.ReturnNumber]++;
            if (noPointsAdded)
            {
                MinX = point.X;
                MaxX = point.X;
                MinY = point.Y;
                MaxY = point.Y;
                MinZ = point.Z;
                MaxZ = point.Z;
                noPointsAdded = false;
            }
            else
            {
                if (point.X < MinX) 
                { 
                    MinX = point.X; 
                }
                else if (point.X > MaxX) 
                { 
                    MaxX = point.X; 
                }
                
                if (point.Y < MinY) 
                { 
                    MinY = point.Y; 
                }
                else if (point.Y > MaxY) 
                { 
                    MaxY = point.Y; 
                }
                
                if (point.Z < MinZ) 
                { 
                    MinZ = point.Z; 
                }
                else if (point.Z > MaxZ) 
                { 
                    MaxZ = point.Z; 
                }
            }
        }
    }
}
