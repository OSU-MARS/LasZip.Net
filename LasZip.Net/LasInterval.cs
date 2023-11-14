// lasinterval.{hpp, cpp}
using LasZip.Extensions;
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
        private SortedSet<LasIntervalStartCell>? cellsToMerge;
        private readonly UInt32 threshold;
        private UInt32 totalNumberOfIntervals; // TODO: make public and remove GetTotalNumberOfIntervals()
        private int lastIndex;
        private LasIntervalStartCell? lastCell;
        private LasIntervalCell? currentCell;
        private LasIntervalStartCell? mergedCells;
        private bool mergedCellsTemporary;

        public int Index { get; set; }
        public UInt32 Start { get; set; }
        public UInt32 End { get; set; }
        public UInt32 Full { get; set; }
        public UInt32 Total { get; set; }

        public LasInterval(UInt32 threshold = 1000)
        {
            this.cells = new();
            this.cellsToMerge = null;
            this.threshold = threshold;
            this.totalNumberOfIntervals = 0;
            this.lastIndex = Int32.MinValue;
            this.lastCell = null;
            this.currentCell = null;
            this.mergedCells = null;
            this.mergedCellsTemporary = false;
        }

        public bool Add(UInt32 pIndex, int cIndex)
        {
            if (this.lastCell == null || this.lastIndex != cIndex)
            {
                this.lastIndex = cIndex;
                if (this.cells.TryGetValue(cIndex, out LasIntervalStartCell? interval) == false)
                {
                    lastCell = new(pIndex);
                    this.cells.Add(cIndex, lastCell);
                    totalNumberOfIntervals++;
                    return true;
                }
                lastCell = interval;
            }
            if (lastCell.Add(pIndex, threshold))
            {
                totalNumberOfIntervals++;
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
            return totalNumberOfIntervals;
        }

        // merge cells (and their intervals) into one cell
        public bool MergeCells(UInt32 numIndices, UInt32[] indices, int newIndex)
        {
            if (numIndices == 1)
            {
                if (this.cells.TryGetValue((int)indices[0], out LasIntervalStartCell? interval) == false)
                {
                    return false;
                }
                this.cells.Add(newIndex, interval);
                this.cells.Remove((int)indices[0]);
            }
            else
            {
                if (cellsToMerge != null) { this.cellsToMerge.Clear(); }
                for (int i = 0; i < numIndices; i++)
                {
                    AddCellToMergeCellSet((int)indices[i], true);
                }
                if (!Merge()) { return false; }
                this.cells.Add(newIndex, mergedCells);
                this.mergedCells = null;
            }
            return true;
        }

        // merge adjacent intervals with small gaps in cells to reduce total interval number to maximum
        public void MergeIntervals(UInt32 maximumIntervals)
        {
            // each cell has minimum one interval
            if (maximumIntervals < GetTotalNumberOfCells())
            {
                maximumIntervals = 0;
            }
            else
            {
                maximumIntervals -= this.GetTotalNumberOfCells();
            }

            // order intervals by smallest gap

            SortedDictionary<UInt32, LasIntervalCell> map = new();
            foreach (KeyValuePair<int, LasIntervalStartCell> hashElement in this.cells)
            {
                LasIntervalCell cell = hashElement.Value;
                while (cell.Next != null)
                {
                    UInt32 diff = cell.Next.Start - cell.End - 1;
                    map.Add(diff, cell);
                    cell = cell.Next;
                }
            }

            // maybe nothing to do
            if (map.Count <= maximumIntervals)
            {
                //if (map.size() == 0)
                //{
                //    fprintf(stderr, "maximumIntervals: %u number of interval gaps: 0 \n", maximumIntervals);
                //}
                //else
                //{
                //    diff = (*(map.begin())).first;
                //    fprintf(stderr, "maximumIntervals: %u number of interval gaps: %u next largest interval gap %u\n", maximumIntervals, (U32)map.size(), diff);
                //}
                return;
            }

            UInt32 size = (UInt32)map.Count;
            while (size > maximumIntervals)
            {
                KeyValuePair<UInt32, LasIntervalCell> mapElement = map.First();
                LasIntervalCell cell = mapElement.Value;
                map.Remove(mapElement.Key);
                if ((cell.Start == 1) && (cell.End == 0)) // the (start == 1 && end == 0) signals that the cell is to be deleted
                {
                    totalNumberOfIntervals--;
                }
                else
                {
                    LasIntervalCell? deleteCell = cell.Next;
                    cell.End = deleteCell.End;
                    cell.Next = deleteCell.Next;
                    if (cell.Next != null)
                    {
                        map.Add(cell.Next.Start - cell.End - 1, cell);
                        deleteCell.Start = 1; 
                        deleteCell.End = 0; // the (start == 1 && end == 0) signals that the cell is to be deleted
                    }
                    else
                    {
                        totalNumberOfIntervals--;
                    }
                    size--;
                }
            }

            foreach (KeyValuePair<UInt32, LasIntervalCell> mapElement in map)
            {
                LasIntervalCell cell = mapElement.Value;
                if ((cell.Start == 1) && (cell.End == 0)) // the (start == 1 && end == 0) signals that the cell is to be deleted
                {
                    totalNumberOfIntervals--;
                }
            }
            // if (verbose) fprintf(stderr, "largest interval gap increased to %u\n", diff);

            // update totals
            foreach (KeyValuePair<int, LasIntervalStartCell> hashElement in this.cells)
            {
                LasIntervalStartCell startCell = hashElement.Value;
                startCell.Total = 0;
                LasIntervalCell? cell = startCell;
                while (cell != null)
                {
                    startCell.Total += (cell.End - cell.Start + 1);
                    cell = cell.Next;
                }
            }
        }

        public void GetCells()
        {
            lastIndex = Int32.MinValue;
            currentCell = null;
        }

        public bool HasCells()
        {
            int index;
            LasIntervalStartCell? cell;
            if (lastIndex == Int32.MinValue)
            {
                SortedDictionary<int, LasIntervalStartCell>.Enumerator enumerator = this.cells.GetEnumerator();
                enumerator.MoveNext();
                index = enumerator.Current.Key;
                cell = enumerator.Current.Value;
            }
            else
            {
                if (this.cells.TryGetValue(lastIndex, out cell) == false)
                {
                    lastIndex = Int32.MinValue;
                    currentCell = null;
                    return false;
                }

                index = lastIndex;
            }

            this.lastIndex = index;
            this.Index = index;
            this.Full = cell.Full;
            this.Total = cell.Total;
            this.currentCell = cell;
            return true;
        }

        public bool GetCell(int cIndex)
        {
            if (this.cells.TryGetValue(cIndex, out LasIntervalStartCell? interval) == false)
            {
                currentCell = null;
                return false;
            }
            this.Index = cIndex;
            this.Full = interval.Full;
            this.Total = interval.Total;
            this.currentCell = interval;
            return true;
        }

        public bool AddCurrentCellToMergeCellSet()
        {
            if (currentCell == null)
            {
                return false;
            }
            cellsToMerge ??= new();
            this.cellsToMerge.Add((LasIntervalStartCell)this.currentCell);
            return true;
        }

        public bool AddCellToMergeCellSet(int cIndex, bool erase)
        {
            if (this.cells.TryGetValue(cIndex, out LasIntervalStartCell? interval) == false)
            {
                return false;
            }
            if (cellsToMerge == null)
            {
                cellsToMerge = new();
            }
            this.cellsToMerge.Add(interval);
            if (erase)
            {
                this.cells.Remove(cIndex);
            }
            return true;
        }

        public bool Merge()
        {
            // maybe delete temporary merge cells from the previous merge
            this.mergedCells = null;

            // are there cells to merge
            if ((cellsToMerge == null) || (this.cellsToMerge.Count == 0)) { return false; }

            // is there just one cell
            if (this.cellsToMerge.Count == 1)
            {
                mergedCellsTemporary = false;
                // simply use this cell as the merge cell
                mergedCells = this.cellsToMerge.First();
            }
            else
            {
                mergedCellsTemporary = true;
                mergedCells = new();
                // iterate over all cells and add their intervals to map
                SortedDictionary<UInt32, LasIntervalStartCell> map = new();
                foreach (LasIntervalStartCell interval in this.cellsToMerge)
                {
                    mergedCells.Full += interval.Full;
                    map.Add(interval.Start, interval);
                }

                // initialize mergedCells with first interval
                SortedDictionary<UInt32, LasIntervalStartCell>.Enumerator mapEnumerator = map.GetEnumerator();
                mapEnumerator.MoveNext();
                KeyValuePair<UInt32, LasIntervalStartCell> mapElement = mapEnumerator.Current;
                LasIntervalStartCell cell = mapElement.Value;

                mergedCells.Start = cell.Start;
                mergedCells.End = cell.End;
                mergedCells.Total = cell.End - cell.Start + 1;
                // if (erase) { delete cell; }

                // merge intervals
                LasIntervalCell lastCell = mergedCells;
                UInt32 diff;
                while (mapEnumerator.MoveNext())
                {
                    mapElement = mapEnumerator.Current;
                    cell = mapElement.Value;
                    // map.erase(mapElement);
                    diff = cell.Start - lastCell.End;
                    if (diff > (int)threshold)
                    {
                        lastCell.Next = new(cell);
                        lastCell = lastCell.Next;
                        mergedCells.Total += (cell.End - cell.Start + 1);
                    }
                    else
                    {
                        diff = cell.End - lastCell.End;
                        if (diff > 0)
                        {
                            lastCell.End = cell.End;
                            mergedCells.Total += diff;
                        }
                        totalNumberOfIntervals--;
                    }
                    // if (erase) { delete cell; }
                }
            }
            currentCell = mergedCells;
            Full = mergedCells.Full;
            Total = mergedCells.Total;
            return true;
        }

        public void ClearMergeCellSet()
        {
            if (cellsToMerge != null)
            {
                this.cellsToMerge.Clear();
            }
        }

        public bool GetMergedCell()
        {
            if (mergedCells != null)
            {
                Full = mergedCells.Full;
                Total = mergedCells.Total;
                currentCell = mergedCells;
                return true;
            }
            return false;
        }

        public bool HasIntervals()
        {
            if (currentCell != null)
            {
                Start = currentCell.Start;
                End = currentCell.End;
                currentCell = currentCell.Next;
                return true;
            }
            return false;
        }

        public bool Read(Stream stream)
        {
            Span<byte> readBuffer = stackalloc byte[4];
            stream.ReadExactly(readBuffer);
            string signature = Encoding.UTF8.GetString(readBuffer);
            if (String.Equals(signature, "LASV", StringComparison.Ordinal) == false)
            {
                throw new IOException("wrong signature: " + signature + " instead of 'LASV'.");
            }

            stream.ReadExactly(readBuffer); // version

            // read number of cells
            UInt32 numberCells = stream.ReadUInt32LittleEndian();

            // loop over all cells
            while (numberCells != 0)
            {
                // read index of cell
                int cellIndex = stream.ReadInt32LittleEndian();

                // create cell and insert into hash
                LasIntervalStartCell startCell = new();
                this.cells.Add(cellIndex, startCell);
                LasIntervalCell cell = startCell;
                // read number of intervals in cell
                UInt32 numberIntervals = stream.ReadUInt32LittleEndian();

                // read number of points in cell
                UInt32 numberPoints = stream.ReadUInt32LittleEndian();

                startCell.Full = numberPoints;
                startCell.Total = 0;
                while (numberIntervals != 0)
                {
                    // read start of interval
                    cell.Start = stream.ReadUInt32LittleEndian();

                    // read end of interval
                    cell.End = stream.ReadUInt32LittleEndian();

                    startCell.Total += (cell.End - cell.Start + 1);
                    numberIntervals--;
                    if (numberIntervals != 0)
                    {
                        cell.Next = new LasIntervalCell();
                        cell = cell.Next;
                    }
                }
                numberCells--;
            }

            return true;
        }

        public bool Write(Stream stream)
        {
            stream.Write(Encoding.UTF8.GetBytes("LASV"));
            Span<byte> version = stackalloc byte[] { 0, 0, 0, 0 };
            stream.Write(version);

            // write number of cells
            stream.WriteLittleEndian(this.cells.Count);

            // loop over all cells
            foreach (KeyValuePair<int, LasIntervalStartCell> hashElement in this.cells)
            {
                // count number of intervals and points in cell
                UInt32 numberIntervals = 0;
                UInt32 numberPoints = hashElement.Value.Full;
                LasIntervalCell? cell = hashElement.Value;
                while (cell != null)
                {
                    numberIntervals++;
                    cell = cell.Next;
                }

                // write index of cell
                int cellIndex = hashElement.Key;
                stream.WriteLittleEndian(cellIndex);
                // write number of intervals in cell
                stream.WriteLittleEndian(numberIntervals);
                // write number of points in cell
                stream.WriteLittleEndian(numberPoints);
                
                // write intervals
                cell = hashElement.Value;
                while (cell != null)
                {
                    // write start of interval
                    stream.WriteLittleEndian(cell.Start);
                    // write end of interval
                    stream.WriteLittleEndian(cell.End);
                    
                    cell = cell.Next;
                }
            }

            return true;
        }
    }
}
