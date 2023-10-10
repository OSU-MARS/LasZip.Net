// lasinterval.{hpp, cpp}
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LasZip
{
    internal class LasInterval
    {
        private readonly SortedDictionary<int, LasIntervalStartCell> cells;
        private SortedSet<LasIntervalStartCell>? cells_to_merge;
        private readonly UInt32 threshold;
        private UInt32 number_intervals; // TODO: make public and remove get_number_intervals()
        private int last_index;
        private LasIntervalStartCell? last_cell;
        private LasIntervalCell? current_cell;
        private LasIntervalStartCell? merged_cells;
        private bool merged_cells_temporary;

        public int Index { get; set; }
        public UInt32 Start { get; set; }
        public UInt32 End { get; set; }
        public UInt32 Full { get; set; }
        public UInt32 Total { get; set; }

        public LasInterval(UInt32 threshold = 1000)
        {
            this.cells = new();
            this.cells_to_merge = null;
            this.threshold = threshold;
            this.number_intervals = 0;
            this.last_index = Int32.MinValue;
            this.last_cell = null;
            this.current_cell = null;
            this.merged_cells = null;
            this.merged_cells_temporary = false;
        }

        public bool Add(UInt32 p_index, int c_index)
        {
            if (this.last_cell == null || this.last_index != c_index)
            {
                this.last_index = c_index;
                if (this.cells.TryGetValue(c_index, out LasIntervalStartCell? interval) == false)
                {
                    last_cell = new(p_index);
                    this.cells.Add(c_index, last_cell);
                    number_intervals++;
                    return true;
                }
                last_cell = interval;
            }
            if (last_cell.Add(p_index, threshold))
            {
                number_intervals++;
                return true;
            }
            return false;
        }

        // get total number of cells
        public UInt32 GetTotalNumberOfCells()
        {
            return (UInt32)this.cells.Count;
        }

        // get total number of intervals
        public UInt32 GetTotalNumberOfIntervals()
        {
            return number_intervals;
        }

        // merge cells (and their intervals) into one cell
        public bool MergeCells(UInt32 num_indices, UInt32[] indices, int new_index)
        {
            if (num_indices == 1)
            {
                if (this.cells.TryGetValue((int)indices[0], out LasIntervalStartCell? interval) == false)
                {
                    return false;
                }
                this.cells.Add(new_index, interval);
                this.cells.Remove((int)indices[0]);
            }
            else
            {
                if (cells_to_merge != null) { this.cells_to_merge.Clear(); }
                for (int i = 0; i < num_indices; i++)
                {
                    AddCellToMergeCellSet((int)indices[i], true);
                }
                if (!Merge()) { return false; }
                this.cells.Add(new_index, merged_cells);
                this.merged_cells = null;
            }
            return true;
        }

        // merge adjacent intervals with small gaps in cells to reduce total interval number to maximum
        public void MergeIntervals(UInt32 maximum_intervals)
        {
            // each cell has minimum one interval
            if (maximum_intervals < GetTotalNumberOfCells())
            {
                maximum_intervals = 0;
            }
            else
            {
                maximum_intervals -= GetTotalNumberOfCells();
            }

            // order intervals by smallest gap

            SortedDictionary<UInt32, LasIntervalCell> map = new();
            foreach (KeyValuePair<int, LasIntervalStartCell> hash_element in this.cells)
            {
                LasIntervalCell cell = hash_element.Value;
                while (cell.Next != null)
                {
                    UInt32 diff = cell.Next.Start - cell.End - 1;
                    map.Add(diff, cell);
                    cell = cell.Next;
                }
            }

            // maybe nothing to do
            if (map.Count <= maximum_intervals)
            {
                // if (verbose) fprintf(stderr, "next largest interval gap is %u\n", diff);
                return;
            }

            UInt32 size = (UInt32)map.Count;
            while (size > maximum_intervals)
            {
                KeyValuePair<UInt32, LasIntervalCell> map_element = map.First();
                LasIntervalCell cell = map_element.Value;
                map.Remove(map_element.Key);
                if ((cell.Start == 1) && (cell.End == 0)) // the (start == 1 && end == 0) signals that the cell is to be deleted
                {
                    number_intervals--;
                }
                else
                {
                    LasIntervalCell? delete_cell = cell.Next;
                    cell.End = delete_cell.End;
                    cell.Next = delete_cell.Next;
                    if (cell.Next != null)
                    {
                        map.Add(cell.Next.Start - cell.End - 1, cell);
                        delete_cell.Start = 1; 
                        delete_cell.End = 0; // the (start == 1 && end == 0) signals that the cell is to be deleted
                    }
                    else
                    {
                        number_intervals--;
                    }
                    size--;
                }
            }

            foreach (KeyValuePair<UInt32, LasIntervalCell> map_element in map)
            {
                LasIntervalCell cell = map_element.Value;
                if ((cell.Start == 1) && (cell.End == 0)) // the (start == 1 && end == 0) signals that the cell is to be deleted
                {
                    number_intervals--;
                }
            }
            // fprintf(stderr, "largest interval gap increased to %u\n", diff);

            // update totals
            foreach (KeyValuePair<int, LasIntervalStartCell> hash_element in this.cells)
            {
                LasIntervalStartCell start_cell = hash_element.Value;
                start_cell.Total = 0;
                LasIntervalCell? cell = start_cell;
                while (cell != null)
                {
                    start_cell.Total += (cell.End - cell.Start + 1);
                    cell = cell.Next;
                }
            }
        }

        public void GetCells()
        {
            last_index = Int32.MinValue;
            current_cell = null;
        }

        public bool HasCells()
        {
            int index;
            LasIntervalStartCell? cell;
            if (last_index == Int32.MinValue)
            {
                SortedDictionary<int, LasIntervalStartCell>.Enumerator enumerator = this.cells.GetEnumerator();
                enumerator.MoveNext();
                index = enumerator.Current.Key;
                cell = enumerator.Current.Value;
            }
            else
            {
                if (this.cells.TryGetValue(last_index, out cell) == false)
                {
                    last_index = Int32.MinValue;
                    current_cell = null;
                    return false;
                }

                index = last_index;
            }

            this.last_index = index;
            this.Index = index;
            this.Full = cell.Full;
            this.Total = cell.Total;
            this.current_cell = cell;
            return true;
        }

        public bool GetCell(int c_index)
        {
            if (this.cells.TryGetValue(c_index, out LasIntervalStartCell? interval) == false)
            {
                current_cell = null;
                return false;
            }
            this.Index = c_index;
            this.Full = interval.Full;
            this.Total = interval.Total;
            this.current_cell = interval;
            return true;
        }

        public bool AddCurrentCellToMergeCellSet()
        {
            if (current_cell == null)
            {
                return false;
            }
            cells_to_merge ??= new();
            this.cells_to_merge.Add((LasIntervalStartCell)this.current_cell);
            return true;
        }

        public bool AddCellToMergeCellSet(int c_index, bool erase)
        {
            if (this.cells.TryGetValue(c_index, out LasIntervalStartCell? interval) == false)
            {
                return false;
            }
            if (cells_to_merge == null)
            {
                cells_to_merge = new();
            }
            this.cells_to_merge.Add(interval);
            if (erase)
            {
                this.cells.Remove(c_index);
            }
            return true;
        }

        public bool Merge()
        {
            // maybe delete temporary merge cells from the previous merge
            this.merged_cells = null;

            // are there cells to merge
            if ((cells_to_merge == null) || (this.cells_to_merge.Count == 0)) { return false; }

            // is there just one cell
            if (this.cells_to_merge.Count == 1)
            {
                merged_cells_temporary = false;
                // simply use this cell as the merge cell
                merged_cells = this.cells_to_merge.First();
            }
            else
            {
                merged_cells_temporary = true;
                merged_cells = new();
                // iterate over all cells and add their intervals to map
                SortedDictionary<UInt32, LasIntervalStartCell> map = new();
                foreach (LasIntervalStartCell interval in this.cells_to_merge)
                {
                    merged_cells.Full += interval.Full;
                    map.Add(interval.Start, interval);
                }

                // initialize merged_cells with first interval
                SortedDictionary<UInt32, LasIntervalStartCell>.Enumerator mapEnumerator = map.GetEnumerator();
                mapEnumerator.MoveNext();
                KeyValuePair<UInt32, LasIntervalStartCell> map_element = mapEnumerator.Current;
                LasIntervalStartCell cell = map_element.Value;

                merged_cells.Start = cell.Start;
                merged_cells.End = cell.End;
                merged_cells.Total = cell.End - cell.Start + 1;
                // if (erase) { delete cell; }

                // merge intervals
                LasIntervalCell last_cell = merged_cells;
                UInt32 diff;
                while (mapEnumerator.MoveNext())
                {
                    map_element = mapEnumerator.Current;
                    cell = map_element.Value;
                    // map.erase(map_element);
                    diff = cell.Start - last_cell.End;
                    if (diff > (int)threshold)
                    {
                        last_cell.Next = new(cell);
                        last_cell = last_cell.Next;
                        merged_cells.Total += (cell.End - cell.Start + 1);
                    }
                    else
                    {
                        diff = cell.End - last_cell.End;
                        if (diff > 0)
                        {
                            last_cell.End = cell.End;
                            merged_cells.Total += diff;
                        }
                        number_intervals--;
                    }
                    // if (erase) { delete cell; }
                }
            }
            current_cell = merged_cells;
            Full = merged_cells.Full;
            Total = merged_cells.Total;
            return true;
        }

        public void ClearMergeCellSet()
        {
            if (cells_to_merge != null)
            {
                this.cells_to_merge.Clear();
            }
        }

        public bool GetMergedCell()
        {
            if (merged_cells != null)
            {
                Full = merged_cells.Full;
                Total = merged_cells.Total;
                current_cell = merged_cells;
                return true;
            }
            return false;
        }

        public bool HasIntervals()
        {
            if (current_cell != null)
            {
                Start = current_cell.Start;
                End = current_cell.End;
                current_cell = current_cell.Next;
                return true;
            }
            return false;
        }

        public bool Read(ByteStreamIn stream)
        {
            Span<byte> readBuffer = stackalloc byte[4];
            stream.GetBytes(readBuffer, 4);
            string signature = Encoding.UTF8.GetString(readBuffer);
            if (String.Equals(signature, "LASV", StringComparison.Ordinal) == false)
            {
                throw new IOException("wrong signature: " + signature + " instead of 'LASV'.");
            }

            stream.Get32bitsLE(readBuffer); // version

            // read number of cells
            stream.Get32bitsLE(readBuffer);
            UInt32 number_cells = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer);

            // loop over all cells
            while (number_cells != 0)
            {
                // read index of cell
                stream.Get32bitsLE(readBuffer);
                int cell_index = BinaryPrimitives.ReadInt32LittleEndian(readBuffer);

                // create cell and insert into hash
                LasIntervalStartCell start_cell = new();
                this.cells.Add(cell_index, start_cell);
                LasIntervalCell cell = start_cell;
                // read number of intervals in cell
                stream.Get32bitsLE(readBuffer);
                UInt32 number_intervals = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer);

                // read number of points in cell
                stream.Get32bitsLE(readBuffer);
                UInt32 number_points = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer);

                start_cell.Full = number_points;
                start_cell.Total = 0;
                while (number_intervals != 0)
                {
                    // read start of interval
                    stream.Get32bitsLE(readBuffer);
                    cell.Start = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer);

                    // read end of interval
                    stream.Get32bitsLE(readBuffer);
                    cell.End = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer);

                    start_cell.Total += (cell.End - cell.Start + 1);
                    number_intervals--;
                    if (number_intervals != 0)
                    {
                        cell.Next = new LasIntervalCell();
                        cell = cell.Next;
                    }
                }
                number_cells--;
            }

            return true;
        }

        public bool Write(ByteStreamOut stream)
        {
            if (!stream.PutBytes(Encoding.UTF8.GetBytes("LASV"), 4))
            {
                throw new IOException("writing signature\n");
            }
            Span<byte> version = stackalloc byte[] { 0, 0, 0, 0 };
            if (!stream.Put32bitsLE(version))
            {
                throw new IOException("writing version");
            }
            // write number of cells
            if (!stream.Put32bitsLE(this.cells.Count))
            {
                throw new IOException("writing number of cells " +  this.cells.Count);
            }
            // loop over all cells
            foreach (KeyValuePair<int, LasIntervalStartCell> hash_element in this.cells)
            {
                // count number of intervals and points in cell
                UInt32 number_intervals = 0;
                UInt32 number_points = hash_element.Value.Full;
                LasIntervalCell? cell = hash_element.Value;
                while (cell != null)
                {
                    number_intervals++;
                    cell = cell.Next;
                }

                // write index of cell
                int cell_index = hash_element.Key;
                if (!stream.Put32bitsLE(cell_index))
                {
                    throw new IOException("writing cell index " + cell_index);
                }
                // write number of intervals in cell
                if (!stream.Put32bitsLE(number_intervals))
                {
                    throw new IOException("writing number of intervals %d in cell " + number_intervals);
                }
                // write number of points in cell
                if (!stream.Put32bitsLE(number_points))
                {
                    throw new IOException("writing number of points %d in cell " + number_points);
                }
                // write intervals
                cell = hash_element.Value;
                while (cell != null)
                {
                    // write start of interval
                    if (!stream.Put32bitsLE(cell.Start))
                    {
                        throw new IOException("writing start " + cell.Start + " of interval");
                    }
                    // write end of interval
                    if (!stream.Put32bitsLE(cell.End))
                    {
                        throw new IOException("writing end " + cell.End + " of interval");
                    }
                    cell = cell.Next;
                }
            }

            return true;
        }
    }
}
