/********************************************************************************
* Base64BodyWriterStreamTests.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.AwsLambda.Tests
{
    [TestFixture]
    internal sealed class Base64BodyWriterStreamTests
    {
        [Test]
        public void Write_ShouldEncodeFullBlocks()
        {
            byte[] bytes = { 0, 1, 2, 253, 254, 255 };

            Assert.That(WriteAll(bytes, chunkSize: bytes.Length), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [Test]
        public void WriteSpan_ShouldEncodePayload()
        {
            byte[] bytes = { 0, 1, 2, 3, 4, 5 };

            using Base64BodyWriterStream stream = new();
            stream.Write(bytes.AsSpan());

            Assert.That(stream.GetBody(), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [Test]
        public void Write_ShouldEncodeThroughTinyWrites()
        {
            byte[] bytes = { 0, 1, 2, 3, 4, 252, 253, 254, 255 };

            Assert.That(WriteAll(bytes, chunkSize: 1), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [Test]
        public void Write_ShouldEncodeThroughNonBlockAlignedWrites()
        {
            byte[] bytes = CreateBytes(256);

            Assert.That(WriteAll(bytes, chunkSize: 5), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [TestCaseSource(nameof(RoundTripCases))]
        public void Write_ShouldMatchBclBase64Encoding(byte[] bytes, int chunkSize)
        {
            Assert.That(WriteAll(bytes, chunkSize), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [TestCaseSource(nameof(PaddingCases))]
        public void GetBody_ShouldEncodePaddingCases(byte[] bytes, string expected)
        {
            Assert.That(WriteAll(bytes, chunkSize: 1), Is.EqualTo(expected));
        }

        [Test]
        public void GetBody_ShouldReturnEmptyBodyForEmptyInput()
        {
            using Base64BodyWriterStream stream = new();

            Assert.That(stream.GetBody(), Is.Empty);
        }

        [Test]
        public void GetBody_ShouldReturnASnapshot()
        {
            using Base64BodyWriterStream stream = new();
            stream.Write(new byte[] { 1 }, 0, 1);

            Assert.That(stream.GetBody(), Is.EqualTo("AQ=="));
            Assert.That(stream.GetBody(), Is.EqualTo("AQ=="));
            Assert.That(stream.CanWrite, Is.True);

            stream.Write(new byte[] { 2 }, 0, 1);
            Assert.That(stream.GetBody(), Is.EqualTo("AQI="));

            stream.Write(new byte[] { 3 }, 0, 1);
            Assert.That(stream.GetBody(), Is.EqualTo("AQID"));
        }

        [Test]
        public async Task FlushAsync_ShouldCompleteWithoutChangingBody()
        {
            using Base64BodyWriterStream stream = new();

            await stream.FlushAsync(CancellationToken.None);

            Assert.That(stream.GetBody(), Is.Empty);
        }

        [Test]
        public void WriteAsync_ShouldSupportCancellation()
        {
            using Base64BodyWriterStream stream = new();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => stream.WriteAsync(new byte[1], 0, 1, cts.Token));
        }

        [TestCaseSource(nameof(RoundTripCases))]
        public async Task WriteAsync_ShouldMatchBclBase64Encoding(byte[] bytes, int chunkSize)
        {
            using Base64BodyWriterStream stream = new();

            for (int offset = 0; offset < bytes.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, bytes.Length - offset);

                await stream.WriteAsync(bytes.AsMemory(offset, count), CancellationToken.None);
            }

            Assert.That(stream.GetBody(), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [Test]
        public async Task CopyToAsync_ShouldEncodeHttpContent()
        {
            byte[] bytes = CreateBytes(1024);
            using ByteArrayContent content = new(bytes);
            using Base64BodyWriterStream stream = new();

            await content.CopyToAsync(stream);

            Assert.That(stream.GetBody(), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [Test]
        public void UnsupportedMembers_ShouldBehaveAsWriteOnlyNonSeekableStream()
        {
            using Base64BodyWriterStream stream = new();

            Assert.That(stream.CanRead, Is.False);
            Assert.That(stream.CanSeek, Is.False);
            Assert.That(stream.CanWrite, Is.True);

            Assert.Throws<NotSupportedException>(() => { _ = stream.Length; });
            Assert.Throws<NotSupportedException>(() => { _ = stream.Position; });
            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
#pragma warning disable CA2022 // This test verifies the unsupported member throws before reading anything.
            Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
#pragma warning restore CA2022
            Assert.DoesNotThrow(() => stream.Flush());
        }

        [Test]
        public void Write_ShouldValidateBufferArguments()
        {
            using Base64BodyWriterStream stream = new();
            byte[] buffer = new byte[2];

            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, buffer.Length + 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, 1, buffer.Length));
        }

        [Test]
        public void Write_ShouldThrowAfterDispose()
        {
            Base64BodyWriterStream stream = new();
            stream.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[1], 0, 1));
            Assert.Throws<ObjectDisposedException>(() => stream.GetBody());
        }

        #region Private
        private static IEnumerable<TestCaseData> PaddingCases()
        {
            yield return new TestCaseData(new byte[] { 1 }, "AQ==");
            yield return new TestCaseData(new byte[] { 1, 2 }, "AQI=");
            yield return new TestCaseData(new byte[] { 1, 2, 3 }, "AQID");
        }

        private static IEnumerable<TestCaseData> RoundTripCases()
        {
            foreach (int length in new[] { 0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 31, 32, 63, 64, 127, 128, 255, 256, 1024, 4097 })
            {
                byte[] bytes = CreateBytes(length);

                yield return new TestCaseData(bytes, 1);
                yield return new TestCaseData(bytes, 5);
                yield return new TestCaseData(bytes, 8192);
            }
        }

        private static string WriteAll(byte[] bytes, int chunkSize)
        {
            using Base64BodyWriterStream stream = new();

            for (int offset = 0; offset < bytes.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, bytes.Length - offset);
                stream.Write(bytes, offset, count);
            }

            return stream.GetBody();
        }

        private static byte[] CreateBytes(int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < result.Length; i++)
                result[i] = (byte) i;

            return result;
        }
        #endregion
    }
}
