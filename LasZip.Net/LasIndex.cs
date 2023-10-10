using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LasZip
{
    internal class LasIndex
    {
        private bool have_interval;

        public LasQuadTree? Spatial { get; private set; }
        public LasInterval? Interval { get; private set; }

        public UInt32 Start { get; set; }
        public UInt32 End { get; set; }
        public UInt32 Full { get; set; }
        public UInt32 Total { get; set; }
        public UInt32 Cells { get; set; }

        public LasIndex()
        {
            Spatial = null;
            Interval = null;
            have_interval = false;
            Start = 0;
            End = 0;
            Full = 0;
            Total = 0;
            Cells = 0;
        }

        public void Prepare(LasQuadTree spatial, int threshold)
        {
            this.Spatial = spatial;
            this.Interval = new((UInt32)threshold);
        }

        public bool Add(double x, double y, UInt32 p_index)
        {
            UInt32 cell = Spatial.GetCellIndex(x, y);
            return Interval.Add(p_index, (int)cell);
        }

        public void Complete(UInt32 minimum_points, int maximum_intervals)
        {
            //if (verbose)
            //{
            //    fprintf(stderr, "before complete %d %d\n", minimum_points, maximum_intervals);
            //}
            if (minimum_points != 0)
            {
                int hash1 = 0;
                SortedDictionary<int, UInt32>[] cell_hash = new SortedDictionary<int, UInt32>[] { new(), new() };
                // insert all cells into hash1
                Interval.GetCells();
                while (Interval.HasCells())
                {
                    cell_hash[hash1][Interval.Index] = Interval.Full;
                }
                while (cell_hash[hash1].Count != 0)
                {
                    int hash2 = (hash1 + 1) % 2;
                    cell_hash[hash2].Clear();
                    // coarsen if a coarser cell will still have fewer than minimum_points (and points in all subcells)
                    bool coarsened = false;
                    UInt32 i, full;
                    int coarser_index = 0;
                    UInt32 num_indices = 0;
                    UInt32 num_filled;
                    UInt32[]? indices = null;
                    foreach (KeyValuePair<int, UInt32> hash_element_outer in cell_hash[hash1])
                    {
                        if (hash_element_outer.Value != 0)
                        {
                            if (Spatial.Coarsen(hash_element_outer.Key, ref coarser_index, ref num_indices, ref indices))
                            {
                                full = 0;
                                num_filled = 0;
                                for (i = 0; i < num_indices; i++)
                                {
                                    KeyValuePair<int, UInt32> hash_element_inner;
                                    if (hash_element_outer.Key == (int)indices[i])
                                    {
                                        hash_element_inner = hash_element_outer;
                                    }
                                    else
                                    {
                                        int index = (int)indices[i];
                                        hash_element_inner = new KeyValuePair<int, UInt32>(index, cell_hash[hash1][index]);
                                    }
                                    if (cell_hash[hash1].ContainsKey(hash_element_inner.Key))
                                    {
                                        full += hash_element_inner.Value;
                                        cell_hash[hash1][hash_element_inner.Key] = 0;
                                        num_filled++;
                                    }
                                }
                                if ((full < minimum_points) && (num_filled == num_indices))
                                {
                                    Interval.MergeCells(num_indices, indices, coarser_index);
                                    coarsened = true;
                                    cell_hash[hash2][coarser_index] = full;
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
                //    fprintf(stderr, "after minimum_points %d\n", minimum_points);
                //    print(false);
                //}
            }
            if (maximum_intervals < 0)
            {
                maximum_intervals = (int)(-maximum_intervals * Interval.GetTotalNumberOfCells());
            }
            if (maximum_intervals != 0)
            {
                Interval.MergeIntervals((UInt32)maximum_intervals);
                //if (verbose)
                //{
                //    fprintf(stderr, "after maximum_intervals %d\n", maximum_intervals);
                //}
            }
        }

        public void Print()
        {
            UInt32 total_cells = 0;
            UInt32 total_full = 0;
            UInt32 total_total = 0;
            UInt32 total_intervals = 0;
            UInt32 total_check;
            UInt32 intervals;
            Interval.GetCells();
            while (Interval.HasCells())
            {
                total_check = 0;
                intervals = 0;
                while (Interval.HasIntervals())
                {
                    total_check += Interval.End - Interval.Start + 1;
                    intervals++;
                }
                //if (total_check != interval.total)
                //{
                //    fprintf(stderr, "ERROR: total_check %d != interval.total %d\n", total_check, interval.total);
                //}
                // if (verbose) { fprintf(stderr, "cell %d intervals %d full %d total %d (%.2f)\n", interval.index, intervals, interval.full, interval.total, 100.0f * interval.full / interval.total); }
                total_cells++;
                total_full += Interval.Full;
                total_total += Interval.Total;
                total_intervals += intervals;
            }
            // if (verbose) { fprintf(stderr, "total cells/intervals %d/%d full %d (%.2f)\n", total_cells, total_intervals, total_full, 100.0f * total_full / total_total); }
        }

        public bool IntersectRectangle(double r_min_x, double r_min_y, double r_max_x, double r_max_y)
        {
            have_interval = false;
            Cells = Spatial.IntersectRectangle(r_min_x, r_min_y, r_max_x, r_max_y);
            //  fprintf(stderr,"%d cells of %g/%g %g/%g intersect rect %g/%g %g/%g\n", num_cells, spatial.get_min_x(), spatial.get_min_y(), spatial.get_max_x(), spatial.get_max_y(), r_min_x, r_min_y, r_max_x, r_max_y);
            if (Cells != 0)
            {
                return MergeIntervals();
            }
            return false;
        }

        public bool IntersectTile(float ll_x, float ll_y, float size)
        {
            have_interval = false;
            Cells = Spatial.IntersectTile(ll_x, ll_y, size);
            //  fprintf(stderr,"%d cells of %g/%g %g/%g intersect tile %g/%g/%g\n", num_cells, spatial.get_min_x(), spatial.get_min_y(), spatial.get_max_x(), spatial.get_max_y(), ll_x, ll_y, size);
            if (Cells != 0)
            {
                return MergeIntervals();
            }
            return false;
        }

        public bool IntersectCircle(double center_x, double center_y, double radius)
        {
            have_interval = false;
            Cells = Spatial.IntersectCircle(center_x, center_y, radius);
            //  fprintf(stderr,"%d cells of %g/%g %g/%g intersect circle %g/%g/%g\n", num_cells, spatial.get_min_x(), spatial.get_min_y(), spatial.get_max_x(), spatial.get_max_y(), center_x, center_y, radius);
            if (Cells != 0)
            {
                return MergeIntervals();
            }
            return false;
        }

        public bool GetIntervals()
        {
            have_interval = false;
            return Interval.GetMergedCell();
        }

        public bool HasIntervals()
        {
            if (Interval.HasIntervals())
            {
                Start = Interval.Start;
                End = Interval.End;
                Full = Interval.Full;
                have_interval = true;
                return true;
            }
            have_interval = false;
            return false;
        }

        public void Read(string file_name)
        {
            string laxFilePath = LasIndex.GetLaxFilePath(file_name);
            using FileStream file = new(laxFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ByteStreamInFile stream = BitConverter.IsLittleEndian ? new ByteStreamInFileLE(file) : new ByteStreamInFileBE(file);
            if (this.Read(stream) == false)
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

        public void Write(string file_name)
        {
            string laxFilePath = LasIndex.GetLaxFilePath(file_name);
            using FileStream file = new(laxFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using ByteStreamOutFile stream = BitConverter.IsLittleEndian ? new ByteStreamOutFileLE(file) : new ByteStreamOutFileBE(file);
            if (this.Write(stream) == false)
            {
                throw new IOException("cannot write '" + laxFilePath + "'.");
            }
        }

        public bool Read(ByteStreamIn stream)
        {
            Span<byte> signatureBytes = stackalloc byte[4];
            stream.GetBytes(signatureBytes, 4);
            string signature = Encoding.UTF8.GetString(signatureBytes);
            if (String.Equals(signature, "LASX", StringComparison.Ordinal) == false)
            {
                throw new IOException("wrong signature '" + signature + "' instead of 'LASX'");
            }
            Span<byte> version = stackalloc byte[4];
            stream.Get32bitsLE(version);

            // read spatial quadtree
            Spatial = new();
            if (!Spatial.Read(stream))
            {
                throw new IOException("cannot read LASspatial (LASquadtree)\n");
            }
            // read interval
            Interval = new();
            if (!Interval.Read(stream))
            {
                throw new IOException("reading LASinterval\n");
            }
            // tell spatial about the existing cells
            Interval.GetCells();
            while (Interval.HasCells())
            {
                Spatial.ManageCell((UInt32)Interval.Index);
            }
            return true;
        }

        public bool Write(ByteStreamOut stream)
        {
            if (!stream.PutBytes(Encoding.UTF8.GetBytes("LASX"), 4))
            {
                throw new IOException("writing signature");
            }
            Span<byte> version = stackalloc byte[] { 0, 0, 0, 0 };
            if (!stream.Put32bitsLE(version))
            {
                throw new IOException("writing version\n");
            }
            // write spatial quadtree
            if (!Spatial.Write(stream))
            {
                throw new IOException("cannot write LASspatial (LASquadtree)\n");
            }
            // write interval
            if (!Interval.Write(stream))
            {
                throw new IOException("writing LASinterval\n");
            }
            return true;
        }

        // seek to next interval point
        public bool SeekNext(LasReadPoint reader, Int64 p_count)
        {
            if (!have_interval)
            {
                if (!HasIntervals()) return false;
                reader.Seek((UInt32)p_count, Start);
                p_count = Start;
            }
            if (p_count == End)
            {
                have_interval = false;
            }
            return true;
        }

        // merge the intervals of non-empty cells
        public bool MergeIntervals()
        {
            if (Spatial.GetIntersectedCells())
            {
                UInt32 used_cells = 0;
                while (Spatial.HasMoreCells())
                {
                    if (Interval.GetCell((int)Spatial.CurrentCell))
                    {
                        Interval.AddCurrentCellToMergeCellSet();
                        used_cells++;
                    }
                }
                //    fprintf(stderr,"LASindex: used %d cells of total %d\n", used_cells, interval.get_number_cells());
                if (used_cells != 0)
                {
                    bool r = Interval.Merge();
                    Full = Interval.Full;
                    Total = Interval.Total;
                    Interval.ClearMergeCellSet();
                    return r;
                }
            }
            return false;
        }
    }
}
