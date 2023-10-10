// bytestreamin_file.hpp
using System;
using System.IO;

namespace LasZip
{
    internal abstract class ByteStreamInFile : ByteStreamIn, IDisposable
    {
        private bool isDisposed;

        protected FileStream File { get; private init; }

        public ByteStreamInFile(FileStream file)
        {
            this.File = file;
            this.isDisposed = false;
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    this.File.Dispose();
                }

                isDisposed = true;
            }
        }

        public override UInt32 GetByte()
        {
            int value = File.ReadByte();
            if (value == -1)
            {
                throw new IOException("End of file.");
            }
            return (UInt32)value;
        }

        public override void GetBytes(Span<byte> bytes, int num_bytes)
        {
            if (this.File.Read(bytes[0..num_bytes]) != num_bytes)
            {
                throw new IOException("Could not read " + num_bytes + " bytes.");
            }
        }

        public override bool IsSeekable()
        {
            return this.File.CanSeek;
        }

        public override long Tell()
        {
            return this.File.Position;
        }

        public override bool Seek(long position)
        {
            if (Tell() != position)
            {
                this.File.Seek(position, SeekOrigin.Begin);
            }
            return true;
        }

        public override bool SeekEnd(long distance)
        {
            this.File.Seek(distance, SeekOrigin.End);
            return true;
        }
    }
}
