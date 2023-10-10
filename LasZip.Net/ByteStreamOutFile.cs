// bytestreamout_file.hpp
using System;
using System.IO;

namespace LasZip
{
    internal abstract class ByteStreamOutFile : ByteStreamOut, IDisposable
    {
        private bool isDisposed;

        protected FileStream File { get; private set; }

        public ByteStreamOutFile(FileStream file)
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

        public bool SetFile(FileStream file)
        {
            this.File = file;
            return true;
        }

        public override bool PutByte(byte value)
        {
            this.File.WriteByte(value);
            return true;
        }

        public override bool PutBytes(ReadOnlySpan<byte> bytes, UInt32 num_bytes)
        {
            this.File.Write(bytes[0..(int)num_bytes]);
            return true;
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
            this.File.Seek(position, SeekOrigin.Begin);
            return true;
        }

        public override bool SeekEnd()
        {
            this.File.Seek(0, SeekOrigin.End);
            return true;
        }
    }
}
