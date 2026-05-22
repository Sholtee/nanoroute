/********************************************************************************
* Base64BodyWriterStream.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.AwsLambda
{
    internal sealed class Base64BodyWriterStream : Stream
    {
        #region Private
        private static readonly ArrayPool<char> s_arrayPool = ArrayPool<char>.Create();

        private StringBuilder _body = new();
        private byte[] _stagedBytes = new byte[3];
        private char[] _encodedChars = s_arrayPool.Rent(4096);

        private int _stagedByteCount;

        private void AppendEncoded(ReadOnlySpan<byte> bytes, CancellationToken cancellation)
        {
            while (!bytes.IsEmpty)
            {
                cancellation.ThrowIfCancellationRequested();

                int bytesToEncode = Math.Min(bytes.Length, (_encodedChars.Length / 4) * 3);

                if (!Convert.TryToBase64Chars(bytes.Slice(0, bytesToEncode), _encodedChars, out int charsWritten))
                    throw new InvalidOperationException();

                _body.Append(_encodedChars, 0, charsWritten);
                bytes = bytes.Slice(bytesToEncode);
            }
        }

        private void EnsureNotDisposed()
        {
            if (_body is null)
                throw new ObjectDisposedException(nameof(Base64BodyWriterStream));
        }

        private void WriteCore(ReadOnlySpan<byte> buffer, CancellationToken cancellation)
        {
            EnsureNotDisposed();

            cancellation.ThrowIfCancellationRequested();

            if (buffer.IsEmpty)
                return;

            if (_stagedByteCount > 0)
            {
                int bytesToStage = Math.Min(_stagedBytes.Length - _stagedByteCount, buffer.Length);

                buffer.Slice(0, bytesToStage).CopyTo(_stagedBytes.AsSpan(_stagedByteCount));
                _stagedByteCount += bytesToStage;
                buffer = buffer.Slice(bytesToStage);

                if (_stagedByteCount < _stagedBytes.Length)
                    return;

                AppendEncoded(_stagedBytes, cancellation);
                _stagedByteCount = 0;
            }

            int blockLength = buffer.Length - (buffer.Length % 3);

            if (blockLength > 0)
            {
                AppendEncoded(buffer.Slice(0, blockLength), cancellation);
                buffer = buffer.Slice(blockLength);
            }

            if (!buffer.IsEmpty)
            {
                buffer.CopyTo(_stagedBytes);
                _stagedByteCount = buffer.Length;
            }
        }

        protected override void Dispose(bool disposing)
        {
            _body = null!;
            _stagedBytes = null!;

            s_arrayPool.Return(_encodedChars, clearArray: false);
            _encodedChars = null!;

            base.Dispose(disposing);
        }
        #endregion

        public override bool CanRead { get; }

        public override bool CanSeek { get; }

        public override bool CanWrite { get; } = true;

        public string GetBody()
        {
            EnsureNotDisposed();

            if (_stagedByteCount is 0)
                return _body.ToString();

            if (!Convert.TryToBase64Chars(_stagedBytes.AsSpan(0, _stagedByteCount), _encodedChars, out int charsWritten))
                throw new InvalidOperationException();

            return string.Concat
            (
                _body.ToString(),
                new string(_encodedChars, 0, charsWritten)
            );
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Ensure.NotNull(buffer);

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));

            WriteCore(buffer.AsSpan(offset, count), CancellationToken.None);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
        {
            await Task.Yield();

            WriteCore(buffer.AsSpan(offset, count), cancellation);
        }

        public override void Write(ReadOnlySpan<byte> buffer) => WriteCore(buffer, CancellationToken.None);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation)
        {
            await Task.Yield();

            WriteCore(buffer.Span, cancellation);
        }

        #region Not Supported members
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override Task FlushAsync(CancellationToken cancellation) => Task.CompletedTask;
        #endregion
    }
}
