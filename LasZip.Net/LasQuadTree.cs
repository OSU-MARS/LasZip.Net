// lasquadtree.{hpp, cpp}
using LasZip.Extensions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LasZip
{
    internal class LasQuadTree
    {
        private const UInt32 LAS_SPATIAL_QUAD_TREE = 0;

        private UInt32 sub_level;
        private UInt32 sub_level_index;
        private readonly UInt32[] level_offset;
        private readonly UInt32[] coarser_indices;
        private UInt32 adaptive_alloc;
        private UInt32[]? adaptive;

        private List<UInt32>? current_cells;
        private int next_cell_index;

        public UInt32 Levels { get; set; }
        public float CellSize { get; set; }
        public float MinX { get; set; }
        public float MaxX { get; set; }
        public float MinY { get; set; }
        public float MaxY { get; set; }
        public UInt32 CellsX { get; set; }
        public UInt32 CellsY { get; set; }

        public UInt32 CurrentCell { get; private set; }

        public LasQuadTree()
        {
            this.level_offset = new UInt32[24];
            this.coarser_indices = new UInt32[4];

            Levels = 0;
            CellSize = 0;
            MinX = 0;
            MaxX = 0;
            MinY = 0;
            MaxY = 0;
            CellsX = 0;
            CellsY = 0;
            sub_level = 0;
            sub_level_index = 0;
            level_offset[0] = 0;
            for (int l = 0; l < 23; l++)
            {
                level_offset[l + 1] = level_offset[l] + (UInt32)((1 << l) * (1 << l));
            }
            current_cells = null;
            adaptive_alloc = 0;
            adaptive = null;
        }

        // returns the bounding box of the cell that x & y fall into at the specified level
        public void GetCellBoundingBox(double x, double y, UInt32 level, float[]? min, float[]? max)
        {
            float cell_mid_x;
            float cell_mid_y;
            float cell_min_x, cell_max_x;
            float cell_min_y, cell_max_y;

            cell_min_x = MinX;
            cell_max_x = MaxX;
            cell_min_y = MinY;
            cell_max_y = MaxY;

            while (level >= 0)
            {
                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;
                if (x < cell_mid_x)
                {
                    cell_max_x = cell_mid_x;
                }
                else
                {
                    cell_min_x = cell_mid_x;
                }
                if (y < cell_mid_y)
                {
                    cell_max_y = cell_mid_y;
                }
                else
                {
                    cell_min_y = cell_mid_y;
                }
                level--;
            }
            if (min != null)
            {
                min[0] = cell_min_x;
                min[1] = cell_min_y;
            }
            if (max != null)
            {
                max[0] = cell_max_x;
                max[1] = cell_max_y;
            }
        }

        // returns the bounding box of the cell that x & y fall into
        public void GetCellBoundingBox(double x, double y, float[]? min, float[]? max)
        {
            GetCellBoundingBox(x, y, Levels, min, max);
        }

        // returns the bounding box of the cell with the specified level_index at the specified level
        public void GetCellBoundingBox(UInt32 level_index, UInt32 level, float[]? min, float[]? max)
        {
            float cell_mid_x;
            float cell_mid_y;
            float cell_min_x, cell_max_x;
            float cell_min_y, cell_max_y;

            cell_min_x = MinX;
            cell_max_x = MaxX;
            cell_min_y = MinY;
            cell_max_y = MaxY;

            UInt32 index;
            while (level >= 0)
            {
                index = (level_index >> (int)(2 * (level - 1))) & 3;
                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;
                if ((index & 1) != 0)
                {
                    cell_min_x = cell_mid_x;
                }
                else
                {
                    cell_max_x = cell_mid_x;
                }
                if ((index & 2) != 0)
                {
                    cell_min_y = cell_mid_y;
                }
                else
                {
                    cell_max_y = cell_mid_y;
                }
                level--;
            }
            if (min != null)
            {
                min[0] = cell_min_x;
                min[1] = cell_min_y;
            }
            if (max != null)
            {
                max[0] = cell_max_x;
                max[1] = cell_max_y;
            }
        }

        // returns the bounding box of the cell with the specified level_index at the specified level
        public void GetCellBoundingBox(UInt32 level_index, UInt32 level, double[]? min, double[]? max)
        {
            double cell_mid_x;
            double cell_mid_y;
            double cell_min_x, cell_max_x;
            double cell_min_y, cell_max_y;

            cell_min_x = MinX;
            cell_max_x = MaxX;
            cell_min_y = MinY;
            cell_max_y = MaxY;

            UInt32 index;
            while (level >= 0)
            {
                index = (level_index >> (int)(2 * (level - 1))) & 3;
                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;
                if ((index & 1) != 0)
                {
                    cell_min_x = cell_mid_x;
                }
                else
                {
                    cell_max_x = cell_mid_x;
                }
                if ((index & 2) != 0)
                {
                    cell_min_y = cell_mid_y;
                }
                else
                {
                    cell_max_y = cell_mid_y;
                }
                level--;
            }
            if (min != null)
            {
                min[0] = cell_min_x;
                min[1] = cell_min_y;
            }
            if (max != null)
            {
                max[0] = cell_max_x;
                max[1] = cell_max_y;
            }
        }

        // returns the bounding box of the cell with the specified level_index
        public void GetCellBoundingBox(UInt32 level_index, float[]? min, float[]? max)
        {
            GetCellBoundingBox(level_index, Levels, min, max);
        }

        // returns the bounding box of the cell with the specified level_index
        public void GetCellBoundingBox(UInt32 level_index, double[]? min, double[]? max)
        {
            GetCellBoundingBox(level_index, Levels, min, max);
        }

        // returns the bounding box of the cell with the specified cell_index
        public void GetCellBoundingBox(int cell_index, float[]? min, float[]? max)
        {
            UInt32 level = GetLevel((UInt32)cell_index);
            UInt32 level_index = GetLevelIndex((UInt32)cell_index, level);
            GetCellBoundingBox(level_index, level, min, max);
        }

        // returns the (sub-)level index of the cell that x & y fall into at the specified level
        public UInt32 GetLevelIndex(double x, double y, UInt32 level)
        {
            float cell_mid_x;
            float cell_mid_y;
            float cell_min_x, cell_max_x;
            float cell_min_y, cell_max_y;

            cell_min_x = MinX;
            cell_max_x = MaxX;
            cell_min_y = MinY;
            cell_max_y = MaxY;

            UInt32 level_index = 0;

            while (level >= 0)
            {
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (x < cell_mid_x)
                {
                    cell_max_x = cell_mid_x;
                }
                else
                {
                    cell_min_x = cell_mid_x;
                    level_index |= 1;
                }
                if (y < cell_mid_y)
                {
                    cell_max_y = cell_mid_y;
                }
                else
                {
                    cell_min_y = cell_mid_y;
                    level_index |= 2;
                }
                level--;
            }

            return level_index;
        }

        // returns the (sub-)level index of the cell that x & y fall into
        public UInt32 GetLevelIndex(double x, double y)
        {
            return GetLevelIndex(x, y, Levels);
        }

        // returns the (sub-)level index and the bounding box of the cell that x & y fall into at the specified level
        public UInt32 GetLevelIndex(double x, double y, UInt32 level, float[]? min, float[]? max)
        {
            float cell_mid_x;
            float cell_mid_y;
            float cell_min_x, cell_max_x;
            float cell_min_y, cell_max_y;

            cell_min_x = MinX;
            cell_max_x = MaxX;
            cell_min_y = MinY;
            cell_max_y = MaxY;

            UInt32 level_index = 0;

            while (level > 0)
            {
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (x < cell_mid_x)
                {
                    cell_max_x = cell_mid_x;
                }
                else
                {
                    cell_min_x = cell_mid_x;
                    level_index |= 1;
                }
                if (y < cell_mid_y)
                {
                    cell_max_y = cell_mid_y;
                }
                else
                {
                    cell_min_y = cell_mid_y;
                    level_index |= 2;
                }
                level--;
            }
            if (min != null)
            {
                min[0] = cell_min_x;
                min[1] = cell_min_y;
            }
            if (max != null)
            {
                max[0] = cell_max_x;
                max[1] = cell_max_y;
            }
            return level_index;
        }

        // returns the (sub-)level index and the bounding box of the cell that x & y fall into
        public UInt32 GetLevelIndex(double x, double y, float[]? min, float[]? max)
        {
            return GetLevelIndex(x, y, Levels, min, max);
        }

        // returns the index of the cell that x & y fall into at the specified level
        public UInt32 GetCellIndex(double x, double y, UInt32 level)
        {
            if (sub_level != 0)
            {
                return (UInt32)(level_offset[sub_level + level] + (sub_level_index << (int)(level * 2)) + GetLevelIndex(x, y, level));
            }
            else
            {
                return level_offset[level] + GetLevelIndex(x, y, level);
            }
        }

        // returns the index of the cell that x & y fall into
        public UInt32 GetCellIndex(double x, double y)
        {
            return GetCellIndex(x, y, Levels);
        }

        // returns the indices of parent and siblings for the specified cell index
        public bool Coarsen(int cell_index, ref int coarser_cell_index, ref UInt32 num_cell_indices, ref UInt32[]? cell_indices)
        {
            if (cell_index < 0) { return false; }
            UInt32 level = GetLevel((UInt32)cell_index);
            if (level == 0) { return false; }
            UInt32 level_index = GetLevelIndex((UInt32)cell_index, level);
            level_index = level_index >> 2;
            if (coarser_cell_index != 0) { coarser_cell_index = (int)GetCellIndex(level_index, level - 1); }
            if ((num_cell_indices != 0) && (cell_indices != null))
            {
                num_cell_indices = 4;
                cell_indices = this.coarser_indices;
                level_index = level_index << 2;
                cell_indices[0] = GetCellIndex(level_index + 0, level);
                cell_indices[1] = GetCellIndex(level_index + 1, level);
                cell_indices[2] = GetCellIndex(level_index + 2, level);
                cell_indices[3] = GetCellIndex(level_index + 3, level);
            }
            return true;
        }

        // returns the level index of the cell index at the specified level
        public UInt32 GetLevelIndex(UInt32 cell_index, UInt32 level)
        {
            if (sub_level != 0)
            {
                return (UInt32)(cell_index - (sub_level_index << (int)(level * 2)) - level_offset[sub_level + level]);
            }
            else
            {
                return cell_index - level_offset[level];
            }
        }

        // returns the level index of the cell index
        public UInt32 GetLevelIndex(UInt32 cell_index)
        {
            return GetLevelIndex(cell_index, Levels);
        }

        // returns the level the cell index
        public UInt32 GetLevel(UInt32 cell_index)
        {
            UInt32 level = 0;
            while (cell_index >= level_offset[level + 1]) { level++; }
            return level;
        }

        // returns the cell index of the level index at the specified level
        public UInt32 GetCellIndex(UInt32 level_index, UInt32 level)
        {
            if (sub_level != 0)
            {
                return (UInt32)(level_index + (sub_level_index << (int)(level * 2)) + level_offset[sub_level + level]);
            }
            else
            {
                return level_index + level_offset[level];
            }
        }

        // returns the cell index of the level index
        public UInt32 GetCellIndex(UInt32 level_index)
        {
            return GetCellIndex(level_index, Levels);
        }

        // returns the maximal level index at the specified level
        public static UInt32 GetMaxLevelIndex(UInt32 level)
        {
            UInt32 levelShift = (1U << (int)level);
            return levelShift * levelShift;
            //return (1 << level)*(1 << level);
        }

        // returns the maximal level index
        public UInt32 GetMaxLevelIndex()
        {
            return GetMaxLevelIndex(Levels);
        }

        // returns the maximal cell index at the specified level
        public UInt32 GetMaxCellIndex(UInt32 level)
        {
            return this.level_offset[level + 1] - 1;
        }

        // returns the maximal cell index
        public UInt32 GetMaxCellIndex()
        {
            return GetMaxCellIndex(Levels);
        }

        // recursively does the actual rastering of the occupancy
        private void RasterOccupancy(Func<UInt32, bool> does_cell_exist, UInt32[] data, UInt32 min_x, UInt32 min_y, UInt32 level_index, UInt32 level, UInt32 stop_level)
        {
            UInt32 cell_index = GetCellIndex(level_index, level);
            UInt32 adaptive_pos = cell_index / 32;
            UInt32 adaptive_bit = (1U) << (int)(cell_index % 32);
            // have we reached a leaf
            if ((adaptive[adaptive_pos] & adaptive_bit) != 0) // interior node
            {
                if (level < stop_level) // do we need to continue
                {
                    level_index <<= 2;
                    level += 1;
                    UInt32 size = 1U << (int)(stop_level - level);
                    // recurse into the four children
                    RasterOccupancy(does_cell_exist, data, min_x, min_y, level_index, level, stop_level);
                    RasterOccupancy(does_cell_exist, data, min_x + size, min_y, level_index + 1, level, stop_level);
                    RasterOccupancy(does_cell_exist, data, min_x, min_y + size, level_index + 2, level, stop_level);
                    RasterOccupancy(does_cell_exist, data, min_x + size, min_y + size, level_index + 3, level, stop_level);
                    return;
                }
                else // no ... raster remaining subtree
                {
                    UInt32 full_size = (1U << (int)stop_level);
                    UInt32 size = 1U << (int)(stop_level - level);
                    UInt32 max_y = min_y + size;
                    UInt32 pos, pos_x, pos_y;
                    for (pos_y = min_y; pos_y < max_y; pos_y++)
                    {
                        pos = pos_y * full_size + min_x;
                        for (pos_x = 0; pos_x < size; pos_x++)
                        {
                            data[pos / 32] |= (1U << (int)(pos % 32));
                            pos++;
                        }
                    }
                }
            }
            else if (does_cell_exist(cell_index))
            {
                // raster actual cell
                UInt32 full_size = (1U << (int)stop_level);
                UInt32 size = 1U << (int)(stop_level - level);
                UInt32 max_y = min_y + size;
                UInt32 pos, pos_x, pos_y;
                for (pos_y = min_y; pos_y < max_y; pos_y++)
                {
                    pos = pos_y * full_size + min_x;
                    for (pos_x = 0; pos_x < size; pos_x++)
                    {
                        data[pos / 32] |= (1U << (int)(pos % 32));
                        pos++;
                    }
                }
            }
        }

        // rasters the occupancy to a simple binary raster at depth level
        public UInt32[] RasterOccupancy(Func<UInt32, bool> does_cell_exist, UInt32 level)
        {
            UInt32 size_xy = (1U << (int)level);
            UInt32 temp_size = (UInt32)((size_xy * size_xy) / 32U + ((size_xy * size_xy) % 32 != 0U ? 1U : 0U));
            UInt32[] data = new UInt32[temp_size]; // left as zero
            RasterOccupancy(does_cell_exist, data, 0, 0, 0, 0, level);
            return data;
        }

        // rasters the occupancy to a simple binary raster at depth levels
        public UInt32[] RasterOccupancy(Func<UInt32, bool> does_cell_exist)
        {
            return RasterOccupancy(does_cell_exist, Levels);
        }

        // read from file
        public bool Read(ByteStreamIn stream)
        {
            // read data in the following order
            //     UInt32  levels          4 bytes 
            //     UInt32  level_index     4 bytes (default 0)
            //     UInt32  implicit_levels 4 bytes (only used when level_index != 0))
            //     float  min_x           4 bytes 
            //     float  max_x           4 bytes 
            //     float  min_y           4 bytes 
            //     float  max_y           4 bytes 
            // which totals 28 bytes

            Span<byte> readBuffer = stackalloc byte[4];
            stream.GetBytes(readBuffer, 4);
            string signature = Encoding.UTF8.GetString(readBuffer);
            if (String.Equals(signature, "LASS", StringComparison.Ordinal) == false)
            {
                throw new ArgumentException("wrong LASspatial signature %4s instead of 'LASS'");
            }

            stream.GetBytes(readBuffer, 4);
            UInt32 type = BitConverter.ToUInt32(readBuffer);
            if (type != LAS_SPATIAL_QUAD_TREE)
            {
                throw new ArgumentException("unknown LASspatial type " + type);
            }

            stream.GetBytes(readBuffer, 4);
            signature = Encoding.UTF8.GetString(readBuffer);
            if (String.Equals(signature, "LASQ", StringComparison.Ordinal) == false)
            {
                //    fprintf(stderr,"ERROR (LASquadtree): wrong signature %4s instead of 'LASV'\n", signature);
                //    return false;
                this.Levels = BitConverter.ToUInt32(readBuffer);
            }
            else
            {
                stream.Get32bitsLE(readBuffer); // version
                stream.Get32bitsLE(readBuffer);
                this.Levels = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer);
            }

            stream.Get32bitsLE(readBuffer); // level_index
            stream.Get32bitsLE(readBuffer); // implicit_levels

            stream.Get32bitsLE(readBuffer);
            this.MinX = BinaryPrimitives.ReadSingleLittleEndian(readBuffer);
            stream.Get32bitsLE(readBuffer);
            this.MaxX = BinaryPrimitives.ReadSingleLittleEndian(readBuffer);
            stream.Get32bitsLE(readBuffer);
            this.MinY = BinaryPrimitives.ReadSingleLittleEndian(readBuffer);
            stream.Get32bitsLE(readBuffer);
            this.MaxY = BinaryPrimitives.ReadSingleLittleEndian(readBuffer);
            return true;
        }

        public bool Write(ByteStreamOut stream)
        {
            // which totals 28 bytes
            //     UInt32  levels          4 bytes 
            //     UInt32  level_index     4 bytes (default 0)
            //     UInt32  implicit_levels 4 bytes (only used when level_index != 0))
            //     float  min_x           4 bytes 
            //     float  max_x           4 bytes 
            //     float  min_y           4 bytes 
            //     float  max_y           4 bytes 
            // which totals 28 bytes

            if (!stream.PutBytes(Encoding.UTF8.GetBytes("LASS"), 4))
            {
                throw new IOException("writing LASspatial signature");
            }

            UInt32 type = LAS_SPATIAL_QUAD_TREE;
            if (!stream.Put32bitsLE(type))
            {
                throw new IOException("writing LASspatial type " + type);
            }

            if (!stream.PutBytes(Encoding.UTF8.GetBytes("LASQ"), 4))
            {
                throw new IOException("writing signature");
            }

            UInt32 version = 0;
            if (!stream.Put32bitsLE(version))
            {
                throw new IOException("writing version");
            }

            if (!stream.Put32bitsLE(Levels))
            {
                throw new IOException("writing levels " + Levels);
            }
            UInt32 level_index = 0;
            if (!stream.Put32bitsLE(level_index))
            {
                throw new IOException("writing level_index " + level_index);
            }
            UInt32 implicit_levels = 0;
            if (!stream.Put32bitsLE(implicit_levels))
            {
                throw new IOException("writing implicit_levels " + implicit_levels);
            }
            if (!stream.Put32bitsLE(MinX))
            {
                throw new IOException("writing min_x " + MinX);
            }
            if (!stream.Put32bitsLE(MaxX))
            {
                throw new IOException("writing max_x " + MaxX);
            }
            if (!stream.Put32bitsLE(MinY))
            {
                throw new IOException("writing min_y " + MinY);
            }
            if (!stream.Put32bitsLE(MaxY))
            {
                throw new IOException("writing max_y " + MaxY);
            }
            return true;
        }

        // create or finalize the cell (in the spatial hierarchy) 
        public bool ManageCell(UInt32 cell_index)
        {
            UInt32 adaptive_pos = cell_index / 32;
            UInt32 adaptive_bit = (1U) << (int)(cell_index % 32);
            if (adaptive_pos >= adaptive_alloc)
            {
                if (adaptive != null)
                {
                    adaptive = adaptive.Extend(2 * adaptive_pos);
                    for (UInt32 i = adaptive_alloc; i < adaptive_pos * 2; i++) { adaptive[i] = 0; }
                    adaptive_alloc = adaptive_pos * 2;
                }
                else
                {
                    adaptive = new UInt32[adaptive_pos + 1];
                    // for (UInt32 i = adaptive_alloc; i <= adaptive_pos; i++) { adaptive[i] = 0; }
                    adaptive_alloc = adaptive_pos + 1;
                }
            }
            adaptive[adaptive_pos] &= ~adaptive_bit;
            UInt32 index;
            UInt32 level = GetLevel(cell_index);
            UInt32 level_index = GetLevelIndex(cell_index, level);
            while (level != 0)
            {
                level--;
                level_index = level_index >> 2;
                index = GetCellIndex(level_index, level);
                adaptive_pos = index / 32;
                adaptive_bit = (1U) << (int)(index % 32);
                if ((adaptive[adaptive_pos] & adaptive_bit) != 0) { break; }
                adaptive[adaptive_pos] |= adaptive_bit;
            }
            return true;
        }

        // check whether the x & y coordinates fall into the tiling
        public bool Inside(double x, double y)
        {
            return ((MinX <= x) && (x < MaxX) && (MinY <= y) && (y < MaxY));
        }

        public UInt32 IntersectRectangle(double r_min_x, double r_min_y, double r_max_x, double r_max_y, UInt32 level)
        {
            if (current_cells == null)
            {
                current_cells = new();
            }
            else
            {
                current_cells.Clear();
            }

            if (r_max_x <= MinX || !(r_min_x <= MaxX) || r_max_y <= MinY || !(r_min_y <= MaxY))
            {
                return 0;
            }

            if (adaptive != null)
            {
                IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, MinX, MaxX, MinY, MaxY, 0, 0);
            }
            else
            {
                IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, MinX, MaxX, MinY, MaxY, level, 0);
            }

            return (UInt32)current_cells.Count;
        }

        public UInt32 IntersectRectangle(double r_min_x, double r_min_y, double r_max_x, double r_max_y)
        {
            return IntersectRectangle(r_min_x, r_min_y, r_max_x, r_max_y, Levels);
        }

        public UInt32 IntersectTile(float ll_x, float ll_y, float size, UInt32 level)
        {
            if (current_cells == null)
            {
                current_cells = new();
            }
            else
            {
                current_cells.Clear();
            }

            float ur_x = ll_x + size;
            float ur_y = ll_y + size;

            if (ur_x <= MinX || !(ll_x <= MaxX) || ur_y <= MinY || !(ll_y <= MaxY))
            {
                return 0;
            }

            if (adaptive != null)
            {
                IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, MinX, MaxX, MinY, MaxY, 0, 0);
            }
            else
            {
                IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, MinX, MaxX, MinY, MaxY, level, 0);
            }

            return (UInt32)current_cells.Count;
        }

        public UInt32 IntersectTile(float ll_x, float ll_y, float size)
        {
            return IntersectTile(ll_x, ll_y, size, Levels);
        }

        public UInt32 IntersectCircle(double center_x, double center_y, double radius, UInt32 level)
        {
            if (current_cells == null)
            {
                current_cells = new();
            }
            else
            {
                current_cells.Clear();
            }

            double r_min_x = center_x - radius;
            double r_min_y = center_y - radius;
            double r_max_x = center_x + radius;
            double r_max_y = center_y + radius;
            if (r_max_x <= MinX || !(r_min_x <= MaxX) || r_max_y <= MinY || !(r_min_y <= MaxY))
            {
                return 0;
            }

            if (adaptive != null)
            {
                IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, MinX, MaxX, MinY, MaxY, 0, 0);
            }
            else
            {
                IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, MinX, MaxX, MinY, MaxY, level, 0);
            }

            return (UInt32)current_cells.Count;
        }

        public UInt32 IntersectCircle(double center_x, double center_y, double radius)
        {
            return IntersectCircle(center_x, center_y, radius, Levels);
        }

        private void IntersectRectangleWithCells(double r_min_x, double r_min_y, double r_max_x, double r_max_y, float cell_min_x, float cell_max_x, float cell_min_y, float cell_max_y, UInt32 level, UInt32 level_index)
        {
            float cell_mid_x;
            float cell_mid_y;
            if (level != 0)
            {
                level--;
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (r_max_x <= cell_mid_x)
                {
                    // cell_max_x = cell_mid_x;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                    else
                    {
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                }
                else if (!(r_min_x < cell_mid_x))
                {
                    // cell_min_x = cell_mid_x;
                    // level_index |= 1;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
                else
                {
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectRectangleWithCells(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
            }
            else
            {
                current_cells.Add(level_index);
            }
        }

        private void IntersectRectangleWithCellsAdaptive(double r_min_x, double r_min_y, double r_max_x, double r_max_y, float cell_min_x, float cell_max_x, float cell_min_y, float cell_max_y, UInt32 level, UInt32 level_index)
        {
            float cell_mid_x;
            float cell_mid_y;
            UInt32 cell_index = GetCellIndex(level_index, level);
            UInt32 adaptive_pos = cell_index / 32;
            UInt32 adaptive_bit = (1U) << (int)(cell_index % 32);
            if ((level < Levels) && ((adaptive[adaptive_pos] & adaptive_bit) != 0))
            {
                level++;
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (r_max_x <= cell_mid_x)
                {
                    // cell_max_x = cell_mid_x;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                    else
                    {
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                }
                else if (!(r_min_x < cell_mid_x))
                {
                    // cell_min_x = cell_mid_x;
                    // level_index |= 1;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
                else
                {
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectRectangleWithCellsAdaptive(r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
            }
            else
            {
                current_cells.Add(cell_index);
            }
        }

        private void IntersectTileWithCells(float ll_x, float ll_y, float ur_x, float ur_y, float cell_min_x, float cell_max_x, float cell_min_y, float cell_max_y, UInt32 level, UInt32 level_index)
        {
            float cell_mid_x;
            float cell_mid_y;
            if (level != 0)
            {
                level--;
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (ur_x <= cell_mid_x)
                {
                    // cell_max_x = cell_mid_x;
                    if (ur_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                    }
                    else if (!(ll_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                    else
                    {
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                }
                else if (!(ll_x < cell_mid_x))
                {
                    // cell_min_x = cell_mid_x;
                    // level_index |= 1;
                    if (ur_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(ll_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
                else
                {
                    if (ur_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(ll_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectTileWithCells(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
            }
            else
            {
                current_cells.Add(level_index);
            }
        }

        private void IntersectTileWithCellsAdaptive(float ll_x, float ll_y, float ur_x, float ur_y, float cell_min_x, float cell_max_x, float cell_min_y, float cell_max_y, UInt32 level, UInt32 level_index)
        {
            float cell_mid_x;
            float cell_mid_y;
            UInt32 cell_index = GetCellIndex(level_index, level);
            UInt32 adaptive_pos = cell_index / 32;
            UInt32 adaptive_bit = (1U) << (int)(cell_index % 32);
            if ((level < Levels) && ((adaptive[adaptive_pos] & adaptive_bit)) != 0)
            {
                level++;
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (ur_x <= cell_mid_x)
                {
                    // cell_max_x = cell_mid_x;
                    if (ur_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                    }
                    else if (!(ll_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                    else
                    {
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                }
                else if (!(ll_x < cell_mid_x))
                {
                    // cell_min_x = cell_mid_x;
                    // level_index |= 1;
                    if (ur_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(ll_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
                else
                {
                    if (ur_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(ll_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectTileWithCellsAdaptive(ll_x, ll_y, ur_x, ur_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
            }
            else
            {
                current_cells.Add(cell_index);
            }
        }

        private void IntersectCircleWithCells(double center_x, double center_y, double radius, double r_min_x, double r_min_y, double r_max_x, double r_max_y, float cell_min_x, float cell_max_x, float cell_min_y, float cell_max_y, UInt32 level, UInt32 level_index)
        {
            float cell_mid_x;
            float cell_mid_y;
            if (level != 0)
            {
                level--;
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (r_max_x <= cell_mid_x)
                {
                    // cell_max_x = cell_mid_x;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                    else
                    {
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                }
                else if (!(r_min_x < cell_mid_x))
                {
                    // cell_min_x = cell_mid_x;
                    // level_index |= 1;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
                else
                {
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectCircleWithCells(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
            }
            else
            {
                if (IntersectCircleWithRectangle(center_x, center_y, radius, cell_min_x, cell_max_x, cell_min_y, cell_max_y))
                {
                    current_cells.Add(level_index);
                }
            }
        }

        private void IntersectCircleWithCellsAdaptive(double center_x, double center_y, double radius, double r_min_x, double r_min_y, double r_max_x, double r_max_y, float cell_min_x, float cell_max_x, float cell_min_y, float cell_max_y, UInt32 level, UInt32 level_index)
        {
            float cell_mid_x;
            float cell_mid_y;
            UInt32 cell_index = GetCellIndex(level_index, level);
            UInt32 adaptive_pos = cell_index / 32;
            UInt32 adaptive_bit = (1U) << (int)(cell_index % 32);
            if ((level < Levels) && ((adaptive[adaptive_pos] & adaptive_bit) != 0))
            {
                level++;
                level_index <<= 2;

                cell_mid_x = (cell_min_x + cell_max_x) / 2;
                cell_mid_y = (cell_min_y + cell_max_y) / 2;

                if (r_max_x <= cell_mid_x)
                {
                    // cell_max_x = cell_mid_x;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                    else
                    {
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                    }
                }
                else if (!(r_min_x < cell_mid_x))
                {
                    // cell_min_x = cell_mid_x;
                    // level_index |= 1;
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
                else
                {
                    if (r_max_y <= cell_mid_y)
                    {
                        // cell_max_y = cell_mid_y;
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                    }
                    else if (!(r_min_y < cell_mid_y))
                    {
                        // cell_min_y = cell_mid_y;
                        // level_index |= 1;
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                    else
                    {
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_min_y, cell_mid_y, level, level_index);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_min_y, cell_mid_y, level, level_index | 1);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_min_x, cell_mid_x, cell_mid_y, cell_max_y, level, level_index | 2);
                        IntersectCircleWithCellsAdaptive(center_x, center_y, radius, r_min_x, r_min_y, r_max_x, r_max_y, cell_mid_x, cell_max_x, cell_mid_y, cell_max_y, level, level_index | 3);
                    }
                }
            }
            else
            {
                if (IntersectCircleWithRectangle(center_x, center_y, radius, cell_min_x, cell_max_x, cell_min_y, cell_max_y))
                {
                    current_cells.Add(cell_index);
                }
            }
        }

        private static bool IntersectCircleWithRectangle(double center_x, double center_y, double radius, float r_min_x, float r_max_x, float r_min_y, float r_max_y)
        {
            double r_diff_x, r_diff_y;
            double radius_squared = radius * radius;
            if (r_max_x < center_x) // R to left of circle center
            {
                r_diff_x = center_x - r_max_x;
                if (r_max_y < center_y) // R in lower left corner
                {
                    r_diff_y = center_y - r_max_y;
                    return ((r_diff_x * r_diff_x + r_diff_y * r_diff_y) < radius_squared);
                }
                else if (r_min_y > center_y) // R in upper left corner
                {
                    r_diff_y = -center_y + r_min_y;
                    return ((r_diff_x * r_diff_x + r_diff_y * r_diff_y) < radius_squared);
                }
                else // R due West of circle
                {
                    return (r_diff_x < radius);
                }
            }
            else if (r_min_x > center_x) // R to right of circle center
            {
                r_diff_x = -center_x + r_min_x;
                if (r_max_y < center_y) // R in lower right corner
                {
                    r_diff_y = center_y - r_max_y;
                    return ((r_diff_x * r_diff_x + r_diff_y * r_diff_y) < radius_squared);
                }
                else if (r_min_y > center_y) // R in upper right corner
                {
                    r_diff_y = -center_y + r_min_y;
                    return ((r_diff_x * r_diff_x + r_diff_y * r_diff_y) < radius_squared);
                }
                else // R due East of circle
                {
                    return (r_diff_x < radius);
                }
            }
            else // R on circle vertical centerline
            {
                if (r_max_y < center_y) // R due South of circle
                {
                    r_diff_y = center_y - r_max_y;
                    return (r_diff_y < radius);
                }
                else if (r_min_y > center_y) // R due North of circle
                {
                    r_diff_y = -center_y + r_min_y;
                    return (r_diff_y < radius);
                }
                else // R contains circle centerpoint
                {
                    return true;
                }
            }
        }

        public bool GetAllCells()
        {
            IntersectRectangle(MinX, MinY, MaxX, MaxY);
            return GetIntersectedCells();
        }

        public bool GetIntersectedCells()
        {
            next_cell_index = 0;
            if (current_cells == null)
            {
                return false;
            }
            if (current_cells.Count == 0)
            {
                return false;
            }
            return true;
        }

        public bool HasMoreCells()
        {
            if (current_cells == null)
            {
                return false;
            }
            if (next_cell_index >= current_cells.Count)
            {
                return false;
            }
            if (adaptive != null)
            {
                CurrentCell = current_cells[next_cell_index];
            }
            else
            {
                CurrentCell = level_offset[Levels] + current_cells[next_cell_index];
            }
            next_cell_index++;
            return true;
        }

        public bool Setup(double bb_min_x, double bb_max_x, double bb_min_y, double bb_max_y, float cell_size)
        {
            this.CellSize = cell_size;
            this.sub_level = 0;
            this.sub_level_index = 0;

            // enlarge bounding box to units of cells
            if (bb_min_x >= 0) MinX = cell_size * ((int)(bb_min_x / cell_size));
            else MinX = cell_size * ((int)(bb_min_x / cell_size) - 1);
            if (bb_max_x >= 0) MaxX = cell_size * ((int)(bb_max_x / cell_size) + 1);
            else MaxX = cell_size * ((int)(bb_max_x / cell_size));
            if (bb_min_y >= 0) MinY = cell_size * ((int)(bb_min_y / cell_size));
            else MinY = cell_size * ((int)(bb_min_y / cell_size) - 1);
            if (bb_max_y >= 0) MaxY = cell_size * ((int)(bb_max_y / cell_size) + 1);
            else MaxY = cell_size * ((int)(bb_max_y / cell_size));

            // how many cells minimally in each direction
            CellsX = MyDefs.QuantizeUInt32((MaxX - MinX) / cell_size);
            CellsY = MyDefs.QuantizeUInt32((MaxY - MinY) / cell_size);

            if (CellsX == 0 || CellsY == 0)
            {
                throw new InvalidOperationException("cells_x " + CellsX + " cells_y " + CellsY);
            }

            // how many quad tree levels to get to that many cells
            UInt32 c = ((CellsX > CellsY) ? CellsX - 1 : CellsY - 1);
            Levels = 0;
            while (c != 0)
            {
                c = c >> 1;
                Levels++;
            }

            // enlarge bounding box to quad tree size
            UInt32 c1, c2;
            c = (1U << (int)Levels) - CellsX;
            c1 = c / 2;
            c2 = c - c1;
            MinX -= (c2 * cell_size);
            MaxX += (c1 * cell_size);
            c = (1U << (int)Levels) - CellsY;
            c1 = c / 2;
            c2 = c - c1;
            MinY -= (c2 * cell_size);
            MaxY += (c1 * cell_size);

            return true;
        }

        public bool Setup(double bb_min_x, double bb_max_x, double bb_min_y, double bb_max_y, float cell_size, float offset_x, float offset_y)
        {
            this.CellSize = cell_size;
            this.sub_level = 0;
            this.sub_level_index = 0;

            // enlarge bounding box to units of cells
            if ((bb_min_x - offset_x) >= 0) MinX = cell_size * ((int)((bb_min_x - offset_x) / cell_size)) + offset_x;
            else MinX = cell_size * ((int)((bb_min_x - offset_x) / cell_size) - 1) + offset_x;
            if ((bb_max_x - offset_x) >= 0) MaxX = cell_size * ((int)((bb_max_x - offset_x) / cell_size) + 1) + offset_x;
            else MaxX = cell_size * ((int)((bb_max_x - offset_x) / cell_size)) + offset_x;
            if ((bb_min_y - offset_y) >= 0) MinY = cell_size * ((int)((bb_min_y - offset_y) / cell_size)) + offset_y;
            else MinY = cell_size * ((int)((bb_min_y - offset_y) / cell_size) - 1) + offset_y;
            if ((bb_max_y - offset_y) >= 0) MaxY = cell_size * ((int)((bb_max_y - offset_y) / cell_size) + 1) + offset_y;
            else MaxY = cell_size * ((int)((bb_max_y - offset_y) / cell_size)) + offset_y;

            // how many cells minimally in each direction
            CellsX = MyDefs.QuantizeUInt32((MaxX - MinX) / cell_size);
            CellsY = MyDefs.QuantizeUInt32((MaxY - MinY) / cell_size);

            if (CellsX == 0 || CellsY == 0)
            {
                throw new InvalidOperationException("cells_x " + CellsX + " cells_y " + CellsY);
            }

            // how many quad tree levels to get to that many cells
            UInt32 c = ((CellsX > CellsY) ? CellsX - 1 : CellsY - 1);
            Levels = 0;
            while (c != 0)
            {
                c = c >> 1;
                Levels++;
            }

            // enlarge bounding box to quad tree size
            UInt32 c1, c2;
            c = (1U << (int)Levels) - CellsX;
            c1 = c / 2;
            c2 = c - c1;
            MinX -= (c2 * cell_size);
            MaxX += (c1 * cell_size);
            c = (1U << (int)Levels) - CellsY;
            c1 = c / 2;
            c2 = c - c1;
            MinY -= (c2 * cell_size);
            MaxY += (c1 * cell_size);

            return true;
        }

        public bool TilingSetup(float min_x, float max_x, float min_y, float max_y, UInt32 levels)
        {
            this.MinX = min_x;
            this.MaxX = max_x;
            this.MinY = min_y;
            this.MaxY = max_y;
            this.Levels = levels;
            this.sub_level = 0;
            this.sub_level_index = 0;
            return true;
        }

        public bool SubtilingSetup(float min_x, float max_x, float min_y, float max_y, UInt32 sub_level, UInt32 sub_level_index, UInt32 levels)
        {
            this.MinX = min_x;
            this.MaxX = max_x;
            this.MinY = min_y;
            this.MaxY = max_y;
            float[] min = new float[2];
            float[] max = new float[2];
            GetCellBoundingBox(sub_level_index, sub_level, min, max);
            this.MinX = min[0];
            this.MaxX = max[0];
            this.MinY = min[1];
            this.MaxY = max[1];
            this.sub_level = sub_level;
            this.sub_level_index = sub_level_index;
            this.Levels = levels;
            return true;
        }
    }
}
