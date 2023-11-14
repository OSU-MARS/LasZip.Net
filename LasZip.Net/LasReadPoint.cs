// lasreadpoint.{hpp, cpp}
using LasZip.Extensions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace LasZip
{
    internal class LasReadPoint
    {
        private Stream? inStream;
        private UInt32 numReaders;
        private LasReadItem[]? readers;
        private LasReadItem[]? readersRaw;
        private LasReadItem[]? readersCompressed;
        private ArithmeticDecoder? decoder;
        private bool layeredLas14compression;

        // used for chunking
        private UInt32 chunkSize;
        private UInt32 chunkCount;
        private UInt32 currentChunk;
        private UInt32 numberChunks;
        private UInt32 tabledChunks;
        private List<Int64>? chunkStarts;
        private UInt32[]? chunkTotals;

        // used for selective decompression (LAS 1.4 point types only)
        private readonly LasZipDecompressSelective decompressSelective;

        // used for seeking
        private Int64 pointStart;
        private UInt32 pointSize;
        private byte[] seekPoint;

        public LasReadPoint(LasZipDecompressSelective decompressSelective = LasZipDecompressSelective.All)
        {
            this.pointSize = 0;
            this.inStream = null;
            this.numReaders = 0;
            this.readers = null;
            this.readersRaw = null;
            this.readersCompressed = null;
            this.decoder = null;
            this.layeredLas14compression = false;

            // used for chunking
            this.chunkSize = UInt32.MaxValue;
            this.chunkCount = 0;
            this.currentChunk = 0;
            this.numberChunks = 0;
            this.tabledChunks = 0;
            this.chunkTotals = null;
            this.chunkStarts = null;
            this.decompressSelective = decompressSelective;

            // used for seeking
            this.pointStart = 0;
            this.seekPoint = Array.Empty<byte>();
        }

        // should only be called *once*
        public bool Setup(LasZip lasZip)
        {
            UInt16 numItems = lasZip.NumItems;
            LasItem[]? items = lasZip.Items;
            if ((numItems == 0) || (items == null))
            {
                return false;
            }

            // create entropy decoder (if requested)
            this.decoder = null;
            if (lasZip.Compressor != 0)
            {
                switch (lasZip.Coder)
                {
                    case LasZip.CoderArithmetic: 
                        this.decoder = new(); 
                        break;
                    default: 
                        return false; // entropy decoder not supported
                }

                // maybe layered compression for LAS 1.4 
                this.layeredLas14compression = lasZip.Compressor == LasZip.CompressorLayeredChunked;
            }

            // initizalize the readers
            readers = null;
            numReaders = numItems;

            // disable chunking
            chunkSize = UInt32.MaxValue;

            // always create the raw readers
            readersRaw = new LasReadItem[numReaders];
            for (int i = 0; i < numReaders; i++)
            {
                switch (items[i].Type)
                {
                    case LasItemType.Point10:
                        readersRaw[i] = BitConverter.IsLittleEndian ? new LasReadItemRawPoint10LittleEndian() : new LasReadItemRawPoint10BigEndian(); 
                        break;
                    case LasItemType.Gpstime11:
                        readersRaw[i] = BitConverter.IsLittleEndian ? new LasReadItemRawGpstime11BigEndian() : new LasReadItemRawGpstime11BigEndian();
                        break;
                    case LasItemType.Rgb12:
                    case LasItemType.Rgb14:
                        readersRaw[i] = BitConverter.IsLittleEndian ? new LasReadItemRawRgb12LittleEndian() : new LasReadItemRawRgb12BigEndian();
                        break;
                    case LasItemType.Byte:
                    case LasItemType.Byte14:
                        readersRaw[i] = new LasReadItemRawByte(items[i].Size); 
                        break;
                    case LasItemType.Point14:
                        readersRaw[i] = BitConverter.IsLittleEndian ? new LasReadItemRawPoint14LittleEndian() : new LasReadItemRawPoint14BigEndian();
                        break;
                    case LasItemType.RgbNir14:
                        readersRaw[i] = BitConverter.IsLittleEndian ? new LasReadItemRawRgbNir14LittleEndian() : new LasReadItemRawRgbNir14BigEndian();
                        break;
                    case LasItemType.Wavepacket13:
                    case LasItemType.Wavepacket14:
                        readersRaw[i] = BitConverter.IsLittleEndian ? new LasReadItemRawWavepacket13LittleEndian() : new LasReadItemRawWavepacket13BigEndian();
                        break;
                    default: 
                        return false;
                }

                this.pointSize += items[i].Size;
            }

            if (this.decoder != null)
            {
                this.readersCompressed = new LasReadItem[numReaders];
                if (this.layeredLas14compression)
                {
                    // because combo LAS 1.0 - 1.4 point struct has padding
                    this.seekPoint = new byte[2 * this.pointSize];
                    // because extended_point_type must be set
                    this.seekPoint[22] = 1;
                }
                else
                {
                    this.seekPoint = new byte[this.pointSize];
                }

                // seeks with compressed data need a seek point
                for (int i = 0; i < numReaders; i++)
                {
                    switch (items[i].Type)
                    {
                        case LasItemType.Point10:
                            if (items[i].Version == 1) 
                                this.readersCompressed[i] = new LasReadItemCompressedPoint10v1(this.decoder);
                            else if (items[i].Version == 2) 
                                this.readersCompressed[i] = new LasReadItemCompressedPoint10v2(this.decoder);
                            else 
                                return false;
                            break;
                        case LasItemType.Gpstime11:
                            if (items[i].Version == 1) 
                                this.readersCompressed[i] = new LasReadItemCompressedGpstime11v1(this.decoder);
                            else if (items[i].Version == 2) 
                                this.readersCompressed[i] = new LasReadItemCompressedGpstime11v2(this.decoder);
                            else 
                                return false;
                            break;
                        case LasItemType.Rgb12:
                            if (items[i].Version == 1) 
                                this.readersCompressed[i] = new LasReadItemCompressedRgb12v1(this.decoder);
                            else if (items[i].Version == 2) 
                                this.readersCompressed[i] = new LasReadItemCompressedRgb12v2(this.decoder);
                            else 
                                return false;
                            break;
                        case LasItemType.Byte:
                            if (items[i].Version == 1) 
                                this.readersCompressed[i] = new LasReadItemCompressedByteV1(this.decoder, items[i].Size);
                            else if (items[i].Version == 2)
                                this.readersCompressed[i] = new LasReadItemCompressedByteV2(this.decoder, items[i].Size);
                            else 
                                return false;
                            break;
                        case LasItemType.Point14:
                            if ((items[i].Version == 3) || (items[i].Version == 2)) // version == 2 from lasproto
                                this.readersCompressed[i] = new LasReadItemCompressedPoint14v3(this.decoder, this.decompressSelective);
                            else if (items[i].Version == 4)
                                this.readersCompressed[i] = new LasReadItemCompressedPoint14v4(this.decoder, this.decompressSelective);
                            else
                                return false;
                            break;
                        case LasItemType.Rgb14:
                            if ((items[i].Version == 3) || (items[i].Version == 2)) // version == 2 from lasproto
                                this.readersCompressed[i] = new LasReadItemCompressedRgb14v3(this.decoder, this.decompressSelective);
                            else if (items[i].Version == 4)
                                this.readersCompressed[i] = new LasReadItemCompressedRgb14v4(this.decoder, this.decompressSelective);
                            else
                                return false;
                            break;
                        case LasItemType.RgbNir14:
                            if ((items[i].Version == 3) || (items[i].Version == 2)) // version == 2 from lasproto
                                this.readersCompressed[i] = new LasReadItemCompressedRgbNir14v3(this.decoder, this.decompressSelective);
                            else if (items[i].Version == 4)
                                this.readersCompressed[i] = new LasReadItemCompressedRgbNir14v4(this.decoder, this.decompressSelective);
                            else
                                return false;
                            break;
                        case LasItemType.Byte14:
                            if ((items[i].Version == 3) || (items[i].Version == 2)) // version == 2 from lasproto
                                this.readersCompressed[i] = new LasReadItemCompressedByte14v3(this.decoder, items[i].Size, this.decompressSelective);
                            else if (items[i].Version == 4)
                                this.readersCompressed[i] = new LasReadItemCompressedByte14v4(this.decoder, items[i].Size, this.decompressSelective);
                            else
                                return false;
                            break;
                        case LasItemType.Wavepacket13:
                            if (items[i].Version == 1)
                                this.readersCompressed[i] = new LASreadItemCompressedWavepacket13v1(this.decoder);
                            else
                                return false;
                            break;
                        case LasItemType.Wavepacket14:
                            if (items[i].Version == 3)
                                this.readersCompressed[i] = new LasReadItemCompressedWavepacket14v3(this.decoder, this.decompressSelective);
                            else if (items[i].Version == 4)
                                this.readersCompressed[i] = new LasReadItemCompressedWavepacket14v4(this.decoder, this.decompressSelective);
                            else
                                return false;
                            break;
                        default: 
                            return false;
                    }

                    if (i != 0)
                    {
                        if (this.layeredLas14compression)
                        {
                            // because combo LAS 1.0 - 1.4 point struct has padding
                            this.seekPoint[i] = (byte)(this.seekPoint[i - 1] + (2 * items[i - 1].Size));
                        }
                        else
                        {
                            this.seekPoint[i] = (byte)(this.seekPoint[i - 1] + items[i - 1].Size);
                        }
                    }
                }

                if (lasZip.Compressor == LasZip.CompressorPointwiseChunked)
                {
                    if (lasZip.ChunkSize != 0) { this.chunkSize = lasZip.ChunkSize; }
                    this.numberChunks = UInt32.MaxValue;
                }
            }
            return true;
        }

        public bool Init(Stream instream)
        {
            this.inStream = instream;
            for (int i = 0; i < numReaders; i++)
            {
                ((LasReadItemRaw)(readersRaw[i])).Init(instream);
            }

            if (decoder != null)
            {
                chunkCount = chunkSize;
                pointStart = 0;
                readers = null;
            }
            else
            {
                pointStart = instream.Position;
                readers = readersRaw;
            }

            return true;
        }

        public bool Seek(UInt32 current, UInt32 target)
        {
            if (!inStream.CanSeek) 
            { 
                return false; 
            }

            UInt32 delta = 0;
            if (decoder != null)
            {
                if (pointStart == 0)
                {
                    InitDec();
                    chunkCount = 0;
                }

                if (chunkStarts != null)
                {
                    UInt32 targetChunk;
                    if (chunkTotals != null)
                    {
                        targetChunk = SearchChunkTable(target, 0, numberChunks);
                        chunkSize = chunkTotals[targetChunk + 1] - chunkTotals[targetChunk];
                        delta = target - chunkTotals[targetChunk];
                    }
                    else
                    {
                        targetChunk = target / chunkSize;
                        delta = target % chunkSize;
                    }
                    if (targetChunk >= tabledChunks)
                    {
                        if (currentChunk < (tabledChunks - 1))
                        {
                            decoder.Done();
                            currentChunk = (tabledChunks - 1);
                            inStream.Seek(chunkStarts[(int)currentChunk], SeekOrigin.Begin);
                            InitDec();
                            chunkCount = 0;
                        }
                        delta += (chunkSize * (targetChunk - currentChunk) - chunkCount);
                    }
                    else if (currentChunk != targetChunk || current > target)
                    {
                        decoder.Done();
                        currentChunk = targetChunk;
                        inStream.Seek(chunkStarts[(int)currentChunk], SeekOrigin.Begin);
                        InitDec();
                        chunkCount = 0;
                    }
                    else
                    {
                        delta = target - current;
                    }
                }
                else if (current > target)
                {
                    decoder.Done();
                    inStream.Seek(pointStart, SeekOrigin.Begin);
                    InitDec();
                    delta = target;
                }
                else if (current < target)
                {
                    delta = target - current;
                }

                while (delta != 0)
                {
                    this.TryRead(this.seekPoint);
                    delta--;
                }
            }
            else
            {
                if (current != target)
                {
                    inStream.Seek(pointStart + pointSize * target, SeekOrigin.Begin);
                }
            }
            return true;
        }

        public bool TryRead(Span<byte> point)
        {
            if (this.decoder != null)
            {
                if (chunkCount == chunkSize)
                {
                    if (pointStart != 0)
                    {
                        decoder.Done();
                        currentChunk++;
                        // check integrity
                        if (currentChunk < tabledChunks)
                        {
                            long here = inStream.Position;
                            if (chunkStarts[(int)currentChunk] != here)
                            {
                                // previous chunk was corrupt
                                currentChunk--;
                                throw new IOException("4711");
                            }
                        }
                    }
                    this.InitDec();
                    if (currentChunk == tabledChunks) // no or incomplete chunk table?
                    {
                        if (this.currentChunk >= this.numberChunks)
                        {
                            this.numberChunks += 256;
                            // this.chunkStarts.Capacity = (int)(this.numberChunks + 1);
                        }
                        this.chunkStarts.Add(pointStart);
                        this.tabledChunks++;
                    }
                    else if (chunkTotals != null) // variable sized chunks?
                    {
                        this.chunkSize = this.chunkTotals[this.currentChunk + 1] - this.chunkTotals[this.currentChunk];
                    }
                    this.chunkCount = 0;
                }
                this.chunkCount++;

                if (this.readers != null)
                {
                    for (int i = 0; i < this.numReaders; i++)
                    {
                        if (this.readers[i].TryRead(point, 0) == false)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (this.layeredLas14compression)
                    {
                        // for layered compression 'dec' only hands over the stream
                        this.decoder.Init(this.inStream, false);
                        // read how many points are in the chunk
                        UInt32 count = this.inStream.ReadUInt32LittleEndian();
                        // read the sizes of all layers
                        for (int i = 0; i < this.numReaders; i++)
                        {
                            ((LasReadItemCompressed)this.readersCompressed[i]).ChunkSizes();
                        }
                        for (int i = 0; i < this.numReaders; i++)
                        {
                            ((LasReadItemCompressed)this.readersCompressed[i]).Init(point[i..], 0);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < this.numReaders; i++)
                        {
                            this.readersRaw[i].TryRead(point, 0);
                            ((LasReadItemCompressed)this.readersCompressed[i]).Init(point, 0);
                        }
                        this.decoder.Init(inStream);
                    }

                    this.readers = this.readersCompressed;
                }
            }
            else
            {
                for (int i = 0; i < this.numReaders; i++)
                {
                    if (this.readers[i].TryRead(point, 0) == false)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool CheckEnd()
        {
            if (this.readers == this.readersCompressed)
            {
                if (this.decoder != null)
                {
                    this.decoder.Done();
                    this.currentChunk++;
                    // check integrity
                    if (this.currentChunk < this.tabledChunks)
                    {
                        long here = this.inStream.Position;
                        if (this.chunkStarts[(int)this.currentChunk] != here)
                        {
                            // last chunk was corrupt
                            throw new IOException("chunk with index " + currentChunk + " of " + tabledChunks + " is corrupt");
                        }
                    }
                }
            }
            return true;
        }

        public bool Done()
        {
            inStream = null;
            return true;
        }

        private bool InitDec()
        {
            // maybe read chunk table (only if chunking enabled)
            if (numberChunks == UInt32.MaxValue)
            {
                if (!ReadChunkTable()) return false;

                currentChunk = 0;
                if (chunkTotals != null) chunkSize = chunkTotals[1];
            }

            pointStart = inStream.Position;
            readers = null;

            return true;
        }

        private bool ReadChunkTable()
        {
            // read the 8 bytes that store the location of the chunk table
            long chunkTableStartPosition = this.inStream.ReadInt64LittleEndian();

            // this is where the chunks start
            long chunksStart = inStream.Position;

            if ((chunkTableStartPosition + 8) == chunksStart)
            {
                // no choice but to fail if adaptive chunking was used
                if (this.chunkSize == UInt32.MaxValue)
                {
                    throw new InvalidOperationException("compressor was interrupted before writing adaptive chunk table of .laz file");
                }

                // otherwise we build the chunk table as we read the file
                numberChunks = 0;
                chunkStarts = [ chunksStart ];
                numberChunks++;
                tabledChunks = 1;
                return true;
            }

            if (inStream.CanSeek == false)
            {
                // no choice but to fail if adaptive chunking was used
                if (chunkSize == UInt32.MaxValue)
                {
                    return false;
                }

                // then we cannot seek to the chunk table but won't need it anyways
                numberChunks = 0;
                tabledChunks = 0;
                return true;
            }

            if (chunkTableStartPosition == -1)
            {
                // the compressor was writing to a non-seekable stream and wrote the chunk table start at the end
                if (inStream.Seek(-8, SeekOrigin.End) == 0)
                {
                    return false;
                }
                chunkTableStartPosition = this.inStream.ReadInt64LittleEndian();
            }

            // read the chunk table
            // seek to where the chunk table
            inStream.Seek(chunkTableStartPosition, SeekOrigin.Begin);
            UInt32 version = this.inStream.ReadUInt32LittleEndian();
            if (version != 0)
            { 
                throw new IOException(); // fail if the version is wrong
            }

            this.numberChunks = this.inStream.ReadUInt32LittleEndian();
            this.chunkTotals = null;
            this.chunkStarts = null;
            if (this.chunkSize == UInt32.MaxValue)
            {
                this.chunkTotals = new UInt32[this.numberChunks + 1];
                this.chunkTotals[0] = 0;
            }

            this.chunkStarts = new()
            {
                chunksStart
            };
            this.tabledChunks = 1;

            if (this.numberChunks > 0)
            {
                this.decoder.Init(inStream);
                IntegerCompressor ic = new(this.decoder, 32, 2);
                ic.InitDecompressor();
                for (int i = 1; i <= this.numberChunks; i++)
                {
                    if (chunkSize == UInt32.MaxValue) { this.chunkTotals[i] = (UInt32)ic.Decompress((i > 1 ? (int)this.chunkTotals[i - 1] : 0), 0); }
                    this.chunkStarts.Add(ic.Decompress((i > 1 ? (int)(chunkStarts[i - 1]) : 0), 1));
                    this.tabledChunks++;
                }
                this.decoder.Done();
                for (int i = 1; i <= this.numberChunks; i++)
                {
                    if (this.chunkSize == UInt32.MaxValue) { chunkTotals[i] += chunkTotals[i - 1]; }
                    this.chunkStarts[i] += this.chunkStarts[i - 1];
                }
            }

            if (inStream.Seek(chunksStart, SeekOrigin.Begin) == 0)
            {
                return false;
            }
            return true;
        }

        private UInt32 SearchChunkTable(UInt32 index, UInt32 lower, UInt32 upper)
        {
            if (lower + 1 == upper) return lower;
            UInt32 mid = (lower + upper) / 2;
            if (index >= chunkTotals[mid])
                return SearchChunkTable(index, mid, upper);
            else
                return SearchChunkTable(index, lower, mid);
        }
    }
}
