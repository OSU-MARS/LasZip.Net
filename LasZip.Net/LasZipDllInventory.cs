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
            this.NumberOfPointsByReturn = new UInt32[16]; // left as zero
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
            if (point.ExtendedPointType != 0)
            {
                this.NumberOfPointsByReturn[point.ExtendedReturnNumber]++;
            }
            else
            {
                if (this.NumberOfPointRecords == UInt32.MaxValue)
                {
                    throw new NotSupportedException("Reached " + UInt32.MaxValue + " points.");
                }

                this.NumberOfPointsByReturn[point.ReturnNumber]++;
            }

            if (noPointsAdded)
            {
                this.MinX = point.X;
                this.MaxX = point.X;
                this.MinY = point.Y;
                this.MaxY = point.Y;
                this.MinZ = point.Z;
                this.MaxZ = point.Z;
                this.noPointsAdded = false;
            }
            else
            {
                if (point.X < MinX) 
                {
                    this.MinX = point.X; 
                }
                else if (point.X > MaxX) 
                {
                    this.MaxX = point.X; 
                }
                
                if (point.Y < MinY) 
                {
                    this.MinY = point.Y; 
                }
                else if (point.Y > MaxY) 
                {
                    this.MaxY = point.Y; 
                }
                
                if (point.Z < MinZ) 
                {
                    this.MinZ = point.Z; 
                }
                else if (point.Z > MaxZ) 
                {
                    this.MaxZ = point.Z; 
                }
            }
        }
    }
}
