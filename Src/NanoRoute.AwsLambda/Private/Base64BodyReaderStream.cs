/********************************************************************************
* Base64BodyReaderStream.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.AwsLambda
{
    internal sealed class Base64BodyReaderStream(string body) : Stream
    {
        #region Private
        private readonly byte[] _stagedBytes = new byte[3];
        private ReadOnlyMemory<char> _remainingBody = body.AsMemory();
        private Memory<byte> _remainingStagedBytes;

        private bool _disposed;

        private int CopyStagedBytes(ref Span<byte> buffer)
        {
            int bytesToCopy = Math.Min(_remainingStagedBytes.Length, buffer.Length);
            if (bytesToCopy is 0)
                return 0;

            _remainingStagedBytes.Span.Slice(0, bytesToCopy).CopyTo(buffer.Slice(0, bytesToCopy));
            _remainingStagedBytes = _remainingStagedBytes.Slice(bytesToCopy);
            buffer = buffer.Slice(bytesToCopy);

            return bytesToCopy;
        }

        private int DecodeStep(Span<byte> buffer)
        {
            ReadOnlySpan<char> bodySpan = _remainingBody.Span;
            int inputLength = Math.Min(bodySpan.Length / 4, buffer.Length / 3) * 4;

            if (inputLength is 0)
            {
                if (!bodySpan.IsEmpty)
                    throw new FormatException();

                return 0;
            }

            if (!Convert.TryFromBase64Chars(bodySpan.Slice(0, inputLength), buffer, out int written))
                throw new FormatException();

            _remainingBody = _remainingBody.Slice(inputLength);

            return written;
        }

        private int ReadCore(Span<byte> buffer, CancellationToken cancellation)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (buffer.IsEmpty)
                return 0;

            int read = CopyStagedBytes(ref buffer);

            while (buffer.Length >= 3 && !_remainingBody.IsEmpty)
            {
                cancellation.ThrowIfCancellationRequested();

                int decoded = DecodeStep(buffer);
                if (decoded is 0)
                    break;

                buffer = buffer.Slice(decoded);
                read += decoded;
            }

            if (!buffer.IsEmpty && !_remainingBody.IsEmpty)
            {
                cancellation.ThrowIfCancellationRequested();

                int stagedByteCount = DecodeStep(_stagedBytes);
                if (stagedByteCount is not 0)
                {
                    _remainingStagedBytes = _stagedBytes.AsMemory(0, stagedByteCount);

                    read += CopyStagedBytes(ref buffer);
                }
            }

            return read;
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;

            base.Dispose(disposing);
        }
        #endregion

        public override bool CanRead => !_disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);

            return ReadCore(buffer.AsSpan(offset, count), CancellationToken.None);
        }

        public override int Read(Span<byte> buffer) => ReadCore(buffer, CancellationToken.None);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
        {
            ValidateBufferArguments(buffer, offset, count);

            await Task.Yield();

            return ReadCore(buffer.AsSpan(offset, count), cancellation);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation)
        {
            await Task.Yield();

            return ReadCore(buffer.Span, cancellation);
        }

        #region Not Supported members
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        #endregion
    }
}
