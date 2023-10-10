// laswritepoint.{hpp, cpp}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
            outStream = null;
            numWriters = 0;
            writers = null;
            writersRaw = null;
            writersCompressed = null;
            encoder = null;

            // used for chunking
            initChunking = false;
            chunkSize = UInt32.MaxValue;
            chunkCount = 0;
            chunkTableStartPosition = 0;
            chunkStartPosition = 0;
        }

        // should only be called *once*
        public bool Setup(LasZip lazZip)
        {
            UInt16 numItems = lazZip.NumItems;
            LasItem[]? items = lazZip.Items;
            Debug.Assert((numItems == 0) || ((items != null) && (numItems == items.Length)));

            // create entropy encoder (if requested)
            encoder = null;
            if (lazZip != null && lazZip.Compressor != 0)
            {
                encoder = lazZip.Coder switch
                {
                    LasZip.CoderArithmetic => new ArithmeticEncoder(),
                    _ => throw new NotSupportedException("Entropy decoder not supported."),
                };
            }

            // initizalize the writers
            writers = null;
            numWriters = numItems;

            // disable chunking
            chunkSize = UInt32.MaxValue;

            // always create the raw writers
            writersRaw = new LasWriteItem[numWriters];

            for (UInt32 i = 0; i < numWriters; i++)
            {
                switch (items[i].Type)
                {
                    case LasItemType.Point10: writersRaw[i] = new LasWriteItemRawPoint10(); break;
                    case LasItemType.Gpstime11: writersRaw[i] = new LasWriteItemRawGpstime11(); break;
                    case LasItemType.Rgb12: writersRaw[i] = new LasWriteItemRawRgb12(); break;
                    case LasItemType.Wavepacket13: writersRaw[i] = new LasWriteItemRawWavepacket13(); break;
                    case LasItemType.Byte: writersRaw[i] = new LasWriteItemRawByte(items[i].Size); break;
                    case LasItemType.Point14: writersRaw[i] = new LasWriteItemRawPoint14(); break;
                    case LasItemType.RgbNir14: writersRaw[i] = new LasWriteItemRawRgbNir14(); break;
                    default: return false;
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
                            if (items[i].Version == 1) throw new NotSupportedException("Version 1 POINT10 is no longer supported, use version 2.");
                            else if (items[i].Version == 2) writersCompressed[i] = new LasWriteItemCompressedPoint10v2(encoder);
                            else return false;
                            break;
                        case LasItemType.Gpstime11:
                            if (items[i].Version == 1) throw new NotSupportedException("Version 1 GPSTIME11 is no longer supported, use version 2.");
                            else if (items[i].Version == 2) writersCompressed[i] = new LasWriteItemCompressedGpstime11v2(encoder);
                            else return false;
                            break;
                        case LasItemType.Rgb12:
                            if (items[i].Version == 1) throw new NotSupportedException("Version 1 RGB12 is no longer supported, use version 2.");
                            else if (items[i].Version == 2) writersCompressed[i] = new LasWriteItemCompressedRgb12v2(encoder);
                            else return false;
                            break;
                        case LasItemType.Wavepacket13:
                            if (items[i].Version == 1) writersCompressed[i] = new LasWriteItemCompressedWavepacket13v1(encoder);
                            else return false;
                            break;
                        case LasItemType.Byte:
                            if (items[i].Version == 1) throw new NotSupportedException("Version 1 BYTE is no longer supported, use version 2.");
                            else if (items[i].Version == 2) writersCompressed[i] = new LasWriteItemCompressedByteV2(encoder, items[i].Size);
                            else return false;
                            break;
                        default: return false;
                    }
                }

                if (lazZip.Compressor == LasZip.CompressorPointwiseChunked)
                {
                    if (lazZip.ChunkSize != 0) { this.chunkSize = lazZip.ChunkSize; }
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

                outstream.Write(BitConverter.GetBytes(chunkTableStartPosition), 0, 8);

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

        public bool Write(LasPoint point)
        {
            if (chunkCount == chunkSize)
            {
                encoder.Done();
                AddChunkToTable();
                Init(outStream);
                chunkCount = 0;
            }
            chunkCount++;

            if (writers != null)
            {
                for (UInt32 i = 0; i < numWriters; i++)
                {
                    writers[i].Write(point);
                }
            }
            else
            {
                for (UInt32 i = 0; i < numWriters; i++)
                {
                    writersRaw[i].Write(point);
                    ((LasWriteItemCompressed)(writersCompressed[i])).Init(point);
                }
                writers = writersCompressed;
                encoder.Init(outStream);
            }

            return true;
        }

        public bool Chunk()
        {
            if (chunkStartPosition == 0 || chunkSize != UInt32.MaxValue)
            {
                return false;
            }
            if (this.encoder == null)
            {
                throw new InvalidOperationException();
            }

            encoder.Done();
            AddChunkToTable();
            Init(outStream);
            chunkCount = 0;

            return true;
        }

        public bool Done()
        {
            if (writers == writersCompressed)
            {
                if (this.encoder == null)
                {
                    throw new InvalidOperationException();
                }

                this.encoder.Done();
                if (chunkStartPosition != 0)
                {
                    if (chunkCount != 0) AddChunkToTable();
                    return WriteChunkTable();
                }
            }
            else if (writers == null)
            {
                if (chunkStartPosition != 0)
                {
                    return WriteChunkTable();
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
                outStream.Write(BitConverter.GetBytes(position), 0, 8);
                outStream.Seek(position, SeekOrigin.Begin);
            }

            UInt32 version = 0;
            outStream.Write(BitConverter.GetBytes(version), 0, 4);
            outStream.Write(BitConverter.GetBytes(chunkBytes.Count), 0, 4);

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
                outStream.Write(BitConverter.GetBytes(position), 0, 8);
            }

            return true;
        }
    }
}
