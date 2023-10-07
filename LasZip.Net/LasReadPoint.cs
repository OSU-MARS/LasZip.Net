// lasreadpoint.{hpp, cpp}
using System;
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
        private ArithmeticDecoder? dec;

        // used for chunking
        private UInt32 chunkSize;
        private UInt32 chunkCount;
        private UInt32 currentChunk;
        private UInt32 numberChunks;
        private UInt32 tabledChunks;
        private List<Int64>? chunkStarts;
        private UInt32[]? chunkTotals;

        // used for seeking
        private Int64 pointStart;
        private UInt32 pointSize;
        private readonly LasPoint seekPoint = new();

        public LasReadPoint()
        {
            pointSize = 0;
            inStream = null;
            numReaders = 0;
            readers = null;
            readersRaw = null;
            readersCompressed = null;
            dec = null;

            // used for chunking
            chunkSize = UInt32.MaxValue;
            chunkCount = 0;
            currentChunk = 0;
            numberChunks = 0;
            tabledChunks = 0;
            chunkTotals = null;
            chunkStarts = null;

            // used for seeking
            pointStart = 0;
        }

        // should only be called *once*
        public bool Setup(LasZip lasZip)
        {
            UInt16 numItems = lasZip.NumItems;
            LasItem[]? items = lasZip.Items;

            // create entropy decoder (if requested)
            dec = null;
            if (lasZip != null && lasZip.Compressor != 0)
            {
                switch (lasZip.Coder)
                {
                    case LasZip.CoderArithmetic: dec = new ArithmeticDecoder(); break;
                    default: return false; // entropy decoder not supported
                }
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
                    case LasItemType.Point10: readersRaw[i] = new LasReadItemRawPoint10(); break;
                    case LasItemType.Gpstime11: readersRaw[i] = new LasReadItemRawGpstime11(); break;
                    case LasItemType.Rgb12: readersRaw[i] = new LasReadItemRawRgb12(); break;
                    case LasItemType.Wavepacket13: readersRaw[i] = new LasReadItemRawWavepacket13(); break;
                    case LasItemType.Byte: readersRaw[i] = new LasReadItemRawByte(items[i].Size); break;
                    case LasItemType.Point14: readersRaw[i] = new LasReadItemRawPoint14(); break;
                    case LasItemType.RgbNir14: readersRaw[i] = new LasReadItemRawRgbNir14(); break;
                    default: return false;
                }
                pointSize += items[i].Size;
            }

            if (dec != null)
            {
                readersCompressed = new LasReadItem[numReaders];

                // seeks with compressed data need a seek point
                for (int i = 0; i < numReaders; i++)
                {
                    switch (items[i].Type)
                    {
                        case LasItemType.Point10:
                            if (items[i].Version == 1) readersCompressed[i] = new LasReadItemCompressedPoint10v1(dec);
                            else if (items[i].Version == 2) readersCompressed[i] = new LasReadItemCompressedPoint10v2(dec);
                            else return false;
                            break;
                        case LasItemType.Gpstime11:
                            if (items[i].Version == 1) readersCompressed[i] = new LasReadItemCompressedGpstime11v1(dec);
                            else if (items[i].Version == 2) readersCompressed[i] = new LasReadItemCompressedGpstime11v2(dec);
                            else return false;
                            break;
                        case LasItemType.Rgb12:
                            if (items[i].Version == 1) readersCompressed[i] = new LasReadItemCompressedRgb12v1(dec);
                            else if (items[i].Version == 2) readersCompressed[i] = new LasReadItemCompressedRgb12v2(dec);
                            else return false;
                            break;
                        case LasItemType.Wavepacket13:
                            if (items[i].Version == 1) readersCompressed[i] = new LASreadItemCompressedWavepacket13v1(dec);
                            else return false;
                            break;
                        case LasItemType.Byte:
                            seekPoint.ExtraBytes = new byte[items[i].Size];
                            seekPoint.NumExtraBytes = items[i].Size;
                            if (items[i].Version == 1) readersCompressed[i] = new LasReadItemCompressedByteV1(dec, items[i].Size);
                            else if (items[i].Version == 2) readersCompressed[i] = new LasReadItemCompressedByteV2(dec, items[i].Size);
                            else return false;
                            break;
                        default: return false;
                    }
                }

                if (lasZip.Compressor == LasZip.CompressorPointwiseChunked)
                {
                    if (lasZip.ChunkSize != 0) chunkSize = lasZip.ChunkSize;
                    numberChunks = UInt32.MaxValue;
                }
            }
            return true;
        }

        public bool Init(Stream instream)
        {
            if (instream == null) return false;
            this.inStream = instream;

            for (int i = 0; i < numReaders; i++)
            {
                ((LasReadItemRaw)(readersRaw[i])).Init(instream);
            }

            if (dec != null)
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
            if (!inStream.CanSeek) { return false; }

            UInt32 delta = 0;
            if (dec != null)
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
                            dec.Done();
                            currentChunk = (tabledChunks - 1);
                            inStream.Seek(chunkStarts[(int)currentChunk], SeekOrigin.Begin);
                            InitDec();
                            chunkCount = 0;
                        }
                        delta += (chunkSize * (targetChunk - currentChunk) - chunkCount);
                    }
                    else if (currentChunk != targetChunk || current > target)
                    {
                        dec.Done();
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
                    dec.Done();
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
                    TryRead(seekPoint);
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

        public bool TryRead(LasPoint point)
        {
            if (dec != null)
            {
                if (chunkCount == chunkSize)
                {
                    if (pointStart != 0)
                    {
                        dec.Done();
                        currentChunk++;
                        // check integrity
                        if (currentChunk < tabledChunks)
                        {
                            long here = inStream.Position;
                            if (chunkStarts[(int)currentChunk] != here)
                            {
                                // previous chunk was corrupt
                                currentChunk--;
                                throw new Exception("4711");
                            }
                        }
                    }
                    InitDec();
                    if (tabledChunks == currentChunk) // no or incomplete chunk table?
                    {
                        //if(current_chunk==number_chunks)
                        //{
                        //    number_chunks+=256;
                        //    chunk_starts=(I64*)realloc(chunk_starts, sizeof(I64)*(number_chunks+1));
                        //}
                        //chunkStarts[tabled_chunks]=pointStart; // needs fixing

                        // If there was no(!) chunk table, we haven't had the chance to create the chunk_starts list.
                        if (tabledChunks == 0 && chunkStarts == null) { chunkStarts = new List<long>(); }

                        chunkStarts.Add(pointStart);
                        numberChunks++;
                        tabledChunks++;
                    }
                    else if (chunkTotals != null) // variable sized chunks?
                    {
                        chunkSize = chunkTotals[currentChunk + 1] - chunkTotals[currentChunk];
                    }
                    chunkCount = 0;
                }
                chunkCount++;

                // BUGBUG: no check for end of points so TryRead() will return true after reading past the end of the points with
                // .laz files
                if (readers != null)
                {
                    for (int i = 0; i < numReaders; i++)
                    {
                        if (readers[i].TryRead(point) == false)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < numReaders; i++)
                    {
                        readersRaw[i].TryRead(point);
                        ((LasReadItemCompressed)(readersCompressed[i])).Init(point);
                    }
                    readers = readersCompressed;
                    dec.Init(inStream);
                }
            }
            else
            {
                for (int i = 0; i < numReaders; i++)
                {
                    if (readers[i].TryRead(point) == false)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool Done()
        {
            if (readers == readersCompressed)
            {
                if (dec != null) dec.Done();
            }

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
            byte[] buffer = new byte[8];

            // read the 8 bytes that store the location of the chunk table
            long chunkTableStartPosition;
            if (inStream.Read(buffer, 0, 8) != 8) { throw new EndOfStreamException(); }
            chunkTableStartPosition = BitConverter.ToInt64(buffer, 0);

            // this is where the chunks start
            long chunksStart = inStream.Position;

            if ((chunkTableStartPosition + 8) == chunksStart)
            {
                // no choice but to fail if adaptive chunking was used
                if (chunkSize == UInt32.MaxValue)
                {
                    return false;
                }

                // otherwise we build the chunk table as we read the file
                numberChunks = 0;
                chunkStarts = new()
                {
                    chunksStart
                };
                numberChunks++;
                tabledChunks = 1;
                return true;
            }

            if (!inStream.CanSeek)
            {
                // no choice but to fail if adaptive chunking was used
                if (chunkSize == UInt32.MaxValue)
                {
                    return false;
                }

                // if the stream is not seekable we cannot seek to the chunk table but won't need it anyways
                numberChunks = UInt32.MaxValue - 1;
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
                if (inStream.Read(buffer, 0, 8) != 8) throw new EndOfStreamException();
                chunkTableStartPosition = BitConverter.ToInt64(buffer, 0);
            }

            // read the chunk table
            inStream.Seek(chunkTableStartPosition, SeekOrigin.Begin);

            inStream.Read(buffer, 0, 8);
            UInt32 version = BitConverter.ToUInt32(buffer, 0);
            if (version != 0) throw new Exception();

            numberChunks = BitConverter.ToUInt32(buffer, 4);
            chunkTotals = null;
            chunkStarts = null;
            if (chunkSize == UInt32.MaxValue)
            {
                chunkTotals = new UInt32[numberChunks + 1];
                chunkTotals[0] = 0;
            }

            chunkStarts = new()
            {
                chunksStart
            };
            tabledChunks = 1;

            if (numberChunks > 0)
            {
                dec.Init(inStream);
                IntegerCompressor ic = new(dec, 32, 2);
                ic.InitDecompressor();
                for (int i = 1; i <= numberChunks; i++)
                {
                    if (chunkSize == UInt32.MaxValue) chunkTotals[i] = (UInt32)ic.Decompress((i > 1 ? (int)chunkTotals[i - 1] : 0), 0);
                    chunkStarts.Add(ic.Decompress((i > 1 ? (int)(chunkStarts[i - 1]) : 0), 1));
                    tabledChunks++;
                }
                dec.Done();
                for (int i = 1; i <= numberChunks; i++)
                {
                    if (chunkSize == UInt32.MaxValue) chunkTotals[i] += chunkTotals[i - 1];
                    chunkStarts[i] += chunkStarts[i - 1];
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
