// laswritepoint.{hpp, cpp}
using LasZip.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static System.Net.WebRequestMethods;

namespace LasZip
{
    internal class LasWritePoint
    {
        private Stream? outStream;
        private UInt32 numWriters;
        private LasWriteItem[]? writers;
        private LasWriteItem[]? writersRaw;
        private LasWriteItem[]? writersCompressed;
        private ArithmeticEncoder? encoder;
        private bool layeredLas14compression;

        // used for chunking
        private bool initChunking;
        private UInt32 chunkSize;
        private UInt32 chunkCount;
        private readonly List<UInt32> chunkSizes = new();
        private readonly List<UInt32> chunkBytes = new();
        private long chunkStartPosition;
        private long chunkTableStartPosition;

        public LasWritePoint()
        {
            this.outStream = null;
            this.numWriters = 0;
            this.writers = null;
            this.writersRaw = null;
            this.writersCompressed = null;
            this.encoder = null;
            this.layeredLas14compression = false;

            // used for chunking
            this.initChunking = false;
            this.chunkSize = UInt32.MaxValue;
            this.chunkCount = 0;
            this.chunkTableStartPosition = 0;
            this.chunkStartPosition = 0;
        }

        // should only be called *once*
        public bool Setup(LasZip lasZip)
        {
            UInt16 numItems = lasZip.NumItems;
            LasItem[]? items = lasZip.Items;
            if ((numItems == 0) || (items == null) || (numItems != items.Length))
            { 
                throw new ArgumentOutOfRangeException(nameof(lasZip));
            }

            // create entropy encoder (if requested)
            this.encoder = null;
            if (lasZip.Compressor != 0)
            {
                this.encoder = lasZip.Coder switch
                {
                    LasZip.CoderArithmetic => new ArithmeticEncoder(),
                    _ => throw new NotSupportedException("Entropy decoder not supported."),
                };

                // maybe layered compression for LAS 1.4 
                this.layeredLas14compression = lasZip.Compressor == LasZip.CompressorLayeredChunked;
            }

            // initizalize the writers
            this.writers = null;
            this.numWriters = numItems;

            // disable chunking
            this.chunkSize = UInt32.MaxValue;

            // always create the raw writers
            this.writersRaw = new LasWriteItem[numWriters];

            for (int writerIndex = 0; writerIndex < numWriters; writerIndex++)
            {
                switch (items[writerIndex].Type)
                {
                    case LasItemType.Point10: 
                        writersRaw[writerIndex] = BitConverter.IsLittleEndian ? new LasWriteItemRawPoint10LittleEndian(): new LasWriteItemRawPoint10BigEndian(); 
                        break;
                    case LasItemType.Gpstime11:
                        writersRaw[writerIndex] = BitConverter.IsLittleEndian ? new LasWriteItemRawGpstime11LittleEndian() : new LasWriteItemRawGpstime11BigEndian(); 
                        break;
                    case LasItemType.Rgb12:
                    case LasItemType.Rgb14:
                        writersRaw[writerIndex] = BitConverter.IsLittleEndian ? new LasWriteItemRawRgb12LittleEndian() : new LasWriteItemRawRgb12BigEndian(); 
                        break;
                    case LasItemType.Byte:
                    case LasItemType.Byte14:
                        writersRaw[writerIndex] = new LasWriteItemRawByte(items[writerIndex].Size); 
                        break;
                    case LasItemType.Point14: 
                        writersRaw[writerIndex] = BitConverter.IsLittleEndian ? new LasWriteItemRawPoint14LittleEndian() : new LasWriteItemRawPoint14BigEndian();
                        break;
                    case LasItemType.RgbNir14: 
                        writersRaw[writerIndex] = BitConverter.IsLittleEndian ? new LasWriteItemRawRgbNir14LittleEndian() : new LasWriteItemRawRgbNir14BigEndian();
                        break;
                    case LasItemType.Wavepacket13:
                    case LasItemType.Wavepacket14:
                        writersRaw[writerIndex] = BitConverter.IsLittleEndian ? new LasWriteItemRawWavepacket13LittleEndian() : new LasWriteItemRawWavepacket13BigEndian(); 
                        break;
                    default:
                        return false;
                }
            }

            // if needed create the compressed writers and set versions
            if (encoder != null)
            {
                writersCompressed = new LasWriteItem[numWriters];

                for (UInt32 i = 0; i < numWriters; i++)
                {
                    switch (items[i].Type)
                    {
                        case LasItemType.Point10:
                            if (items[i].Version == 1) 
                                throw new NotSupportedException("Version 1 POINT10 is no longer supported, use version 2.");
                            else if (items[i].Version == 2) 
                                writersCompressed[i] = new LasWriteItemCompressedPoint10v2(encoder);
                            else 
                            { 
                                return false; 
                            }
                            break;
                        case LasItemType.Gpstime11:
                            if (items[i].Version == 1) 
                                throw new NotSupportedException("Version 1 GPSTIME11 is no longer supported, use version 2.");
                            else if (items[i].Version == 2) 
                                writersCompressed[i] = new LasWriteItemCompressedGpstime11v2(encoder);
                            else 
                            { 
                                return false; 
                            }
                            break;
                        case LasItemType.Rgb12:
                            if (items[i].Version == 1)
                                throw new NotSupportedException("Version 1 RGB12 is no longer supported, use version 2.");
                            else if (items[i].Version == 2) 
                                writersCompressed[i] = new LasWriteItemCompressedRgb12v2(encoder);
                            else 
                            { 
                                return false; 
                            }
                            break;
                        case LasItemType.Byte:
                            if (items[i].Version == 1) 
                                throw new NotSupportedException("Version 1 BYTE is no longer supported, use version 2.");
                            else if (items[i].Version == 2)
                                writersCompressed[i] = new LasWriteItemCompressedByteV2(encoder, items[i].Size);
                            else
                            { 
                                return false; 
                            }
                            break;
                        case LasItemType.Point14:
                            if (items[i].Version == 3)
                                writersCompressed[i] = new LasWriteItemCompressedPoint14v3(encoder);
                            else if (items[i].Version == 4)
                                writersCompressed[i] = new LasWriteItemCompressedPoint14v4(encoder);
                            else
                                return false;
                            break;
                        case LasItemType.Rgb14:
                            if (items[i].Version == 3)
                                writersCompressed[i] = new LasWriteItemCompressedRgb14v3(encoder);
                            else if (items[i].Version == 4)
                                writersCompressed[i] = new LasWriteItemCompressedRgb14v4(encoder);
                            else
                                return false;
                            break;
                        case LasItemType.RgbNir14:
                            if (items[i].Version == 3)
                                writersCompressed[i] = new LasWriteItemCompressedRgbNir14v3(encoder);
                            else if (items[i].Version == 4)
                                writersCompressed[i] = new LasWriteItemCompressedRgbNir14v4(encoder);
                            else
                                return false;
                            break;
                        case LasItemType.Byte14:
                            if (items[i].Version == 3)
                                writersCompressed[i] = new LasWriteItemCompressedByte14v3(encoder, items[i].Size);
                            else if (items[i].Version == 4)
                                writersCompressed[i] = new LasWriteItemCompressedByte14v4(encoder, items[i].Size);
                            else
                                return false;
                            break;
                        case LasItemType.Wavepacket13:
                            if (items[i].Version == 1) 
                                writersCompressed[i] = new LasWriteItemCompressedWavepacket13v1(encoder);
                            else 
                            { 
                                return false; 
                            }
                            break;
                        case LasItemType.Wavepacket14:
                            if (items[i].Version == 3)
                                writersCompressed[i] = new LasWriteItemCompressedWavepacket14v3(encoder);
                            else if (items[i].Version == 4)
                                writersCompressed[i] = new LasWriteItemCompressedWavepacket14v4(encoder);
                            else
                                return false;
                            break;
                        default: return false;
                    }
                }

                if (lasZip.Compressor != LasZip.CompressorPointwise)
                {
                    if (lasZip.ChunkSize != 0) { this.chunkSize = lasZip.ChunkSize; }
                    this.chunkCount = 0;
                    this.initChunking = true;
                }
            }

            return true;
        }

        public bool Init(Stream outstream)
        {
            if (outstream == null) return false;
            this.outStream = outstream;

            // if chunking is enabled
            if (initChunking)
            {
                initChunking = false;
                if (outstream.CanSeek) chunkTableStartPosition = outstream.Position;
                else chunkTableStartPosition = -1;

                outstream.WriteLittleEndian(chunkTableStartPosition);

                chunkStartPosition = outstream.Position;
            }

            for (UInt32 i = 0; i < numWriters; i++)
            {
                ((LasWriteItemRaw)(writersRaw[i])).Init(outstream);
            }

            if (encoder != null) writers = null;
            else writers = writersRaw;

            return true;
        }

        public bool Write(ReadOnlySpan<byte> point)
        {
            if (this.chunkCount == this.chunkSize)
            {
                if (this.encoder != null)
                {
                    if (this.layeredLas14compression)
                    {
                        // write how many points are in the chunk
                        this.outStream.WriteLittleEndian(this.chunkCount);
                        // write all layers 
                        for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                        {
                            ((LasWriteItemCompressed)writers[writerIndex]).ChunkSizes();
                        }
                        for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                        {
                            ((LasWriteItemCompressed)writers[writerIndex]).ChunkBytes();
                        }
                    }
                    else
                    {
                        this.encoder.Done();
                    }
                    this.AddChunkToTable();
                    this.Init(this.outStream);
                    chunkCount = 0;
                }
                else
                {
                    // happens *only* for uncompressed LAS with over U32_MAX points 
                    Debug.Assert(this.chunkSize == UInt32.MaxValue);
                }
            }
            chunkCount++;

            if (this.writers != null)
            {
                for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                {
                    if (this.writers[writerIndex].Write(point, 0) == false)
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                {
                    if (this.writersRaw[writerIndex].Write(point, 0) == false)
                    {
                        return false;
                    }
                    ((LasWriteItemCompressed)(this.writersCompressed[writerIndex])).Init(point, 0);
                }
                this.writers = writersCompressed;
                this.encoder.Init(this.outStream);
            }

            return true;
        }

        public bool Chunk()
        {
            if ((this.chunkStartPosition == 0) || (this.chunkSize != UInt32.MaxValue))
            {
                return false;
            }
            if (this.encoder == null)
            {
                throw new InvalidOperationException();
            }

            if (this.layeredLas14compression)
            {
                // write how many points are in the chunk
                this.outStream.WriteLittleEndian(this.chunkCount);
                // write all layers 
                for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                {
                    ((LasWriteItemCompressed)writers[writerIndex]).ChunkSizes();
                }
                for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                {
                    ((LasWriteItemCompressed)writers[writerIndex]).ChunkBytes();
                }
            }
            else
            {
                encoder.Done();
            }

            this.AddChunkToTable();
            this.Init(this.outStream);
            chunkCount = 0;
            return true;
        }

        public bool Done()
        {
            if (writers == writersCompressed)
            {
                if (this.layeredLas14compression)
                {
                    // write how many points are in the chunk
                    this.outStream.WriteLittleEndian(this.chunkCount);
                    // write all layers 
                    for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                    {
                        ((LasWriteItemCompressed)writers[writerIndex]).ChunkSizes();
                    }
                    for (int writerIndex = 0; writerIndex < this.numWriters; writerIndex++)
                    {
                        ((LasWriteItemCompressed)writers[writerIndex]).ChunkBytes();
                    }
                }
                else
                {
                    if (this.encoder == null)
                    {
                        throw new InvalidOperationException();
                    }
                    this.encoder.Done();
                }
                if (chunkStartPosition != 0)
                {
                    if (chunkCount != 0) { this.AddChunkToTable(); }
                    return this.WriteChunkTable();
                }
            }
            else if (writers == null)
            {
                if (chunkStartPosition != 0)
                {
                    return this.WriteChunkTable();
                }
            }

            return true;
        }

        private bool AddChunkToTable()
        {
            Debug.Assert(this.outStream != null);

            long position = outStream.Position;
            if (chunkSize == UInt32.MaxValue) { chunkSizes.Add(chunkCount); }
            chunkBytes.Add((UInt32)(position - chunkStartPosition));
            chunkStartPosition = position;

            return true;
        }

        private bool WriteChunkTable()
        {
            Debug.Assert((this.outStream != null) && (this.encoder != null));

            long position = outStream.Position;
            if (chunkTableStartPosition != -1) // stream is seekable
            {
                outStream.Seek(chunkTableStartPosition, SeekOrigin.Begin);
                outStream.WriteLittleEndian(position);
                outStream.Seek(position, SeekOrigin.Begin);
            }

            UInt32 version = 0;
            outStream.WriteLittleEndian(version);
            outStream.WriteLittleEndian(chunkBytes.Count);

            if (chunkBytes.Count > 0)
            {
                encoder.Init(outStream);

                IntegerCompressor ic = new(encoder, 32, 2);
                ic.InitCompressor();

                for (int index = 0; index < chunkBytes.Count; index++)
                {
                    if (chunkSize == UInt32.MaxValue) ic.Compress((index != 0 ? (int)chunkSizes[index - 1] : 0), (int)chunkSizes[index], 0);
                    ic.Compress((index != 0 ? (int)chunkBytes[index - 1] : 0), (int)chunkBytes[index], 1);
                }

                encoder.Done();
            }

            if (chunkTableStartPosition == -1) // stream is not-seekable
            {
                outStream.WriteLittleEndian(position);
            }

            return true;
        }
    }
}
