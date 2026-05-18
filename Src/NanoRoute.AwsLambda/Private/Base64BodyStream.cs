/********************************************************************************
* Base64BodyStream.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.AwsLambda
{
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by tests and intended for request-body mapping in a follow-up change.")]
    internal sealed class Base64BodyStream(string body) : Stream
    {
        #region Private
        private static readonly sbyte[] s_base64Ranks = CreateBase64Ranks();

        private readonly byte[] _stagedBytes = new byte[3];
        private ReadOnlyMemory<char> _remainingBody = body.AsMemory();
        private Memory<byte> _remainingStagedBytes;

        private bool _disposed;

        private int CopyStagedBytes(byte[] buffer, ref int offset, ref int count)
        {
            int bytesToCopy = Math.Min(_remainingStagedBytes.Length, count);
            if (bytesToCopy is 0)
                return 0;

            _remainingStagedBytes.Slice(0, bytesToCopy).CopyTo
            (
                buffer.AsMemory(offset, bytesToCopy)
            );

            _remainingStagedBytes = _remainingStagedBytes.Slice(bytesToCopy);

            offset += bytesToCopy;
            count -= bytesToCopy;

            return bytesToCopy;
        }

        private int DecodeStep(byte[] buffer, int offset, int count)
        {
            ReadOnlySpan<char> bodySpan = _remainingBody.Span;

            int
                written = 0,
                consumed = 0;

            while (count - written >= 3 && bodySpan.Length - consumed >= 4)
            {
                char
                    c0 = bodySpan[consumed],
                    c1 = bodySpan[consumed + 1],
                    c2 = bodySpan[consumed + 2],
                    c3 = bodySpan[consumed + 3];

                int save =
                    (DecodeBase64Rank(c0) << 18) |
                    (DecodeBase64Rank(c1) << 12) |
                    (DecodeBase64Rank(c2) <<  6) |
                     DecodeBase64Rank(c3);

                buffer[offset + written++] = (byte) (save >> 16);

                if (c2 is not '=')
                    buffer[offset + written++] = (byte) (save >> 8);

                if (c3 is not '=')
                    buffer[offset + written++] = (byte) save;

                consumed += 4;
            }

            _remainingBody = _remainingBody.Slice(consumed);

            if (written is 0 && !_remainingBody.IsEmpty)
                throw new FormatException();

            return written;

            static int DecodeBase64Rank(char ch)
            {
                if (ch >= s_base64Ranks.Length)
                    throw new FormatException();

                int rank = s_base64Ranks[ch];
                if (rank < 0)
                    throw new FormatException();

                return rank;
            }
        }

        private static sbyte[] CreateBase64Ranks()
        {
            sbyte[] ranks = new sbyte[128];

            ranks.AsSpan().Fill(-1);

            for (char ch = 'A'; ch <= 'Z'; ch++)
                ranks[ch] = (sbyte) (ch - 'A');

            for (char ch = 'a'; ch <= 'z'; ch++)
                ranks[ch] = (sbyte) (ch - 'a' + 26);

            for (char ch = '0'; ch <= '9'; ch++)
                ranks[ch] = (sbyte) (ch - '0' + 52);

            ranks['+'] = 62;
            ranks['/'] = 63;
            ranks['='] = 0;

            return ranks;
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
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (_disposed)
                throw new ObjectDisposedException(nameof(Base64BodyStream));

            if (count is 0)
                return 0;

            int read = CopyStagedBytes(buffer, ref offset, ref count);

            while (count >= 3 && !_remainingBody.IsEmpty)
            {
                int decoded = DecodeStep(buffer, offset, count);
                if (decoded is 0)
                    break;

                offset += decoded;
                count -= decoded;
                read += decoded;
            }

            while (count > 0 && !_remainingBody.IsEmpty)
            {
                int stagedByteCount = DecodeStep(_stagedBytes, 0, _stagedBytes.Length);
                if (stagedByteCount is 0)
                    break;

                _remainingStagedBytes = _stagedBytes.AsMemory(0, stagedByteCount);

                read += CopyStagedBytes(buffer, ref offset, ref count);
            }

            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<int>(cancellationToken);

            return Task.FromResult(Read(buffer, offset, count));
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
