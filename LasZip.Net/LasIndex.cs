using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LasZip
{
    internal class LasIndex
    {
        private bool haveInterval;

        public LasQuadTree? Spatial { get; private set; }
        public LasInterval? Interval { get; private set; }

        public UInt32 Start { get; set; }
        public UInt32 End { get; set; }
        public UInt32 Full { get; set; }
        public UInt32 Total { get; set; }
        public UInt32 Cells { get; set; }

        public LasIndex()
        {
            this.Spatial = null;
            this.Interval = null;
            this.haveInterval = false;
            this.Start = 0;
            this.End = 0;
            this.Full = 0;
            this.Total = 0;
            this.Cells = 0;
        }

        public void Prepare(LasQuadTree spatial, int threshold)
        {
            this.Spatial = spatial;
            this.Interval = new((UInt32)threshold);
        }

        public bool Add(double x, double y, UInt32 p_index)
        {
            UInt32 cell = this.Spatial.GetCellIndex(x, y);
            return this.Interval.Add(p_index, (int)cell);
        }

        public void Complete(UInt32 minimumPoints, int maximumIntervals)
        {
            //if (verbose)
            //{
            //    fprintf(stderr, "before complete %d %d\n", minimumPoints, maximumIntervals);
            //}
            if (minimumPoints != 0)
            {
                int hash1 = 0;
                SortedDictionary<int, UInt32>[] cellHash = new SortedDictionary<int, UInt32>[] { new(), new() };
                // insert all cells into hash1
                this.Interval.GetCells();
                while (this.Interval.HasCells())
                {
                    cellHash[hash1][this.Interval.Index] = this.Interval.Full;
                }
                while (cellHash[hash1].Count != 0)
                {
                    int hash2 = (hash1 + 1) % 2;
                    cellHash[hash2].Clear();
                    // coarsen if a coarser cell will still have fewer than minimumPoints (and points in all subcells)
                    bool coarsened = false;
                    UInt32 i, full;
                    int coarserIndex = 0;
                    UInt32 numIndices = 0;
                    UInt32 numFilled;
                    UInt32[]? indices = null;
                    foreach (KeyValuePair<int, UInt32> hashElementOuter in cellHash[hash1])
                    {
                        if (hashElementOuter.Value != 0)
                        {
                            if (this.Spatial.Coarsen(hashElementOuter.Key, ref coarserIndex, ref numIndices, ref indices))
                            {
                                full = 0;
                                numFilled = 0;
                                for (i = 0; i < numIndices; i++)
                                {
                                    KeyValuePair<int, UInt32> hashElementInner;
                                    if (hashElementOuter.Key == (int)indices[i])
                                    {
                                        hashElementInner = hashElementOuter;
                                    }
                                    else
                                    {
                                        int index = (int)indices[i];
                                        hashElementInner = new KeyValuePair<int, UInt32>(index, cellHash[hash1][index]);
                                    }
                                    if (cellHash[hash1].ContainsKey(hashElementInner.Key))
                                    {
                                        full += hashElementInner.Value;
                                        cellHash[hash1][hashElementInner.Key] = 0;
                                        numFilled++;
                                    }
                                }
                                if ((full < minimumPoints) && (numFilled == numIndices))
                                {
                                    Interval.MergeCells(numIndices, indices, coarserIndex);
                                    coarsened = true;
                                    cellHash[hash2][coarserIndex] = full;
                                }
                            }
                        }
                    }
                    if (!coarsened) { break; }
                    hash1 = (hash1 + 1) % 2;
                }
                // tell spatial about the existing cells
                Interval.GetCells();
                while (Interval.HasCells())
                {
                    Spatial.ManageCell((UInt32)Interval.Index);
                }
                //if (verbose)
                //{
                //    fprintf(stderr, "after minimumPoints %d\n", minimumPoints);
                //    print(false);
                //}
            }
            if (maximumIntervals < 0)
            {
                maximumIntervals = (int)(-maximumIntervals * this.Interval.GetTotalNumberOfCells());
            }
            if (maximumIntervals != 0)
            {
                this.Interval.MergeIntervals((UInt32)maximumIntervals);
                //if (verbose)
                //{
                //    fprintf(stderr, "after maximumIntervals %d\n", maximumIntervals);
                //}
            }
        }

        public void Print()
        {
            UInt32 totalCells = 0;
            UInt32 totalFull = 0;
            UInt32 totalTotal = 0;
            UInt32 totalIntervals = 0;
            UInt32 totalCheck;
            UInt32 intervals;
            this.Interval.GetCells();
            while (this.Interval.HasCells())
            {
                totalCheck = 0;
                intervals = 0;
                while (this.Interval.HasIntervals())
                {
                    totalCheck += this.Interval.End - this.Interval.Start + 1;
                    intervals++;
                }
                //if (total_check != interval.total)
                //{
                //    fprintf(stderr, "ERROR: total_check %d != interval.total %d\n", total_check, interval.total);
                //}
                // if (verbose) { fprintf(stderr, "cell %d intervals %d full %d total %d (%.2f)\n", interval.index, intervals, interval.full, interval.total, 100.0f * interval.full / interval.total); }
                totalCells++;
                totalFull += this.Interval.Full;
                totalTotal += this.Interval.Total;
                totalIntervals += intervals;
            }
            // if (verbose) { fprintf(stderr, "total cells/intervals %d/%d full %d (%.2f)\n", total_cells, total_intervals, total_full, 100.0f * total_full / total_total); }
        }

        public bool IntersectRectangle(double rMinX, double rMinY, double rMaxX, double rMaxY)
        {
            haveInterval = false;
            this.Cells = this.Spatial.IntersectRectangle(rMinX, rMinY, rMaxX, rMaxY);
            //  fprintf(stderr,"%d cells of %g/%g %g/%g intersect rect %g/%g %g/%g\n", num_cells, spatial.get_min_x(), spatial.get_min_y(), spatial.get_max_x(), spatial.get_max_y(), r_min_x, r_min_y, r_max_x, r_max_y);
            if (this.Cells != 0)
            {
                return this.MergeIntervals();
            }
            return false;
        }

        public bool IntersectTile(float llX, float llY, float size)
        {
            haveInterval = false;
            this.Cells = this.Spatial.IntersectTile(llX, llY, size);
            //  fprintf(stderr,"%d cells of %g/%g %g/%g intersect tile %g/%g/%g\n", num_cells, spatial.get_min_x(), spatial.get_min_y(), spatial.get_max_x(), spatial.get_max_y(), ll_x, ll_y, size);
            if (this.Cells != 0)
            {
                return this.MergeIntervals();
            }
            return false;
        }

        public bool IntersectCircle(double centerX, double centerY, double radius)
        {
            haveInterval = false;
            this.Cells = this.Spatial.IntersectCircle(centerX, centerY, radius);
            //  fprintf(stderr,"%d cells of %g/%g %g/%g intersect circle %g/%g/%g\n", num_cells, spatial.get_min_x(), spatial.get_min_y(), spatial.get_max_x(), spatial.get_max_y(), center_x, center_y, radius);
            if (this.Cells != 0)
            {
                return this.MergeIntervals();
            }
            return false;
        }

        public bool GetIntervals()
        {
            haveInterval = false;
            return this.Interval.GetMergedCell();
        }

        public bool HasIntervals()
        {
            if (this.Interval.HasIntervals())
            {
                this.Start = this.Interval.Start;
                this.End = this.Interval.End;
                this.Full = this.Interval.Full;
                this.haveInterval = true;
                return true;
            }

            this.haveInterval = false;
            return false;
        }

        public void Read(string filePath)
        {
            string laxFilePath = LasIndex.GetLaxFilePath(filePath);
            using FileStream file = new(laxFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (this.Read(file) == false)
            {
                throw new IOException("cannot read '" + laxFilePath + "'.");
            }
        }

        private static string GetLaxFilePath(string lasOrLazFilePath)
        {
            string lasLazFileExtension = Path.GetExtension(lasOrLazFilePath);
            string laxFileExtension = ".lax";
            if (String.Equals(lasLazFileExtension, ".LAS", StringComparison.Ordinal) || String.Equals(lasLazFileExtension, ".LAZ", StringComparison.Ordinal))
            {
                laxFileExtension = ".LAX";
            }
            string laxFilePath = Path.GetFileNameWithoutExtension(lasOrLazFilePath) + laxFileExtension;
            string? lasOrLazDirectoryPath = Path.GetDirectoryName(lasOrLazFilePath);
            if (lasOrLazDirectoryPath != null)
            {
                laxFilePath = Path.Combine(lasOrLazDirectoryPath, laxFilePath);
            }

            return laxFilePath;
        }

        public void Write(string filePath)
        {
            string laxFilePath = LasIndex.GetLaxFilePath(filePath);
            using FileStream file = new(laxFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            if (this.Write(file) == false)
            {
                throw new IOException("cannot write '" + laxFilePath + "'.");
            }
        }

        public bool Read(Stream stream)
        {
            Span<byte> signatureBytes = stackalloc byte[4];
            stream.ReadExactly(signatureBytes);
            string signature = Encoding.UTF8.GetString(signatureBytes);
            if (String.Equals(signature, "LASX", StringComparison.Ordinal) == false)
            {
                throw new IOException("wrong signature '" + signature + "' instead of 'LASX'");
            }
            Span<byte> version = stackalloc byte[4];
            stream.ReadExactly(version);

            // read spatial quadtree
            this.Spatial = new();
            if (!this.Spatial.Read(stream))
            {
                throw new IOException("cannot read LASspatial (LASquadtree)\n");
            }
            // read interval
            this.Interval = new();
            if (!this.Interval.Read(stream))
            {
                throw new IOException("reading LASinterval\n");
            }
            // tell spatial about the existing cells
            this.Interval.GetCells();
            while (this.Interval.HasCells())
            {
                this.Spatial.ManageCell((UInt32)this.Interval.Index);
            }
            return true;
        }

        public bool Write(Stream stream)
        {
            stream.Write(Encoding.UTF8.GetBytes("LASX"));

            Span<byte> version = stackalloc byte[] { 0, 0, 0, 0 };
            stream.Write(version);

            // write spatial quadtree
            if (!this.Spatial.Write(stream))
            {
                throw new IOException("cannot write LASspatial (LASquadtree)\n");
            }
            // write interval
            if (!this.Interval.Write(stream))
            {
                throw new IOException("writing LASinterval\n");
            }
            return true;
        }

        // seek to next interval point
        public bool SeekNext(LasReadPoint reader, Int64 pCount)
        {
            if (!this.haveInterval)
            {
                if (!this.HasIntervals()) { return false; }
                reader.Seek((UInt32)pCount, Start);
                pCount = Start;
            }
            if (pCount == End)
            {
                this.haveInterval = false;
            }
            return true;
        }

        // merge the intervals of non-empty cells
        public bool MergeIntervals()
        {
            if (this.Spatial.GetIntersectedCells())
            {
                UInt32 usedCells = 0;
                while (this.Spatial.HasMoreCells())
                {
                    if (this.Interval.GetCell((int)this.Spatial.CurrentCell))
                    {
                        this.Interval.AddCurrentCellToMergeCellSet();
                        usedCells++;
                    }
                }
                //    fprintf(stderr,"LASindex: used %d cells of total %d\n", used_cells, interval.get_number_cells());
                if (usedCells != 0)
                {
                    bool r = this.Interval.Merge();
                    this.Full = this.Interval.Full;
                    this.Total = this.Interval.Total;
                    this.Interval.ClearMergeCellSet();
                    return r;
                }
            }
            return false;
        }
    }
}
