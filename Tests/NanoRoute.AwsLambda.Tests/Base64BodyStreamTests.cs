/********************************************************************************
* Base64BodyStreamTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace NanoRoute.AwsLambda.Tests
{
    [TestFixture]
    internal sealed class Base64BodyStreamTests
    {
        [Test]
        public void Read_ShouldDecodeFullBuffer()
        {
            byte[] expected = { 0, 1, 2, 253, 254, 255 };

            using Base64BodyStream stream = new(Convert.ToBase64String(expected));
            byte[] actual = new byte[expected.Length];

            Assert.That(stream.Read(actual, 0, actual.Length), Is.EqualTo(expected.Length));
            Assert.That(actual, Is.EquivalentTo(expected));
            Assert.That(stream.Read(actual, 0, actual.Length), Is.Zero);
        }

        [Test]
        public void Read_ShouldDecodeThroughTinyBuffers()
        {
            byte[] expected = { 0, 1, 2, 3, 4, 252, 253, 254, 255 };

            Assert.That(ReadAll(Convert.ToBase64String(expected), bufferSize: 1), Is.EquivalentTo(expected));
        }

        [Test]
        public void Read_ShouldDecodeThroughNonBlockAlignedBuffers()
        {
            byte[] expected = CreateBytes(256);

            Assert.That(ReadAll(Convert.ToBase64String(expected), bufferSize: 5), Is.EquivalentTo(expected));
        }

        [TestCaseSource(nameof(RoundTripCases))]
        public void Read_ShouldRoundTripBclEncodedPayloads(byte[] expected, int bufferSize)
        {
            Assert.That(ReadAll(Convert.ToBase64String(expected), bufferSize), Is.EqualTo(expected));
        }

        [TestCaseSource(nameof(PaddingCases))]
        public void Read_ShouldDecodePaddingCases(string encoded, byte[] expected)
        {
            Assert.That(ReadAll(encoded, bufferSize: 16), Is.EqualTo(expected));
        }

        [Test]
        public void Read_ShouldThrowFormatExceptionForWhitespace([Values(1, 4, 16)] int bufferSize)
        {
            Assert.Throws<FormatException>(() => ReadAll(" A Q \r\nI\tD ", bufferSize));
        }

        [Test]
        public void Read_ShouldThrowFormatExceptionForInvalidCharacters([Values("!!!!", "\u0100")] string encoded, [Values(1, 4, 16)] int bufferSize)
        {
            Assert.Throws<FormatException>(() => ReadAll(encoded, bufferSize: 16));
        }

        [Test]
        public void Read_ShouldReturnEofForEmptyInput()
        {
            using Base64BodyStream stream = new("");

            Assert.That(stream.Read(new byte[1], 0, 1), Is.Zero);
        }

        [Test]
        public void ReadAsync_ShouldReturnCanceledTaskWhenCancellationIsRequested()
        {
            using Base64BodyStream stream = new("");
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Task<int> read = stream.ReadAsync(new byte[1], 0, 1, cts.Token);

            Assert.That(read.IsCanceled, Is.True);
        }

        [TestCaseSource(nameof(RoundTripCases))]
        public async Task ReadAsync_ShouldRoundTripBclEncodedPayloads(byte[] expected, int bufferSize)
        {
            using Base64BodyStream stream = new(Convert.ToBase64String(expected));
            byte[] actual = new byte[expected.Length];
#if NETCOREAPP
            Assert.That(await stream.ReadAsync(actual.AsMemory(), CancellationToken.None), Is.EqualTo(expected.Length));
#else
            Assert.That(await stream.ReadAsync(actual, 0, actual.Length), Is.EqualTo(expected.Length));
#endif
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void UnsupportedMembers_ShouldBehaveAsReadOnlyNonSeekableStream()
        {
            using Base64BodyStream stream = new("");

            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.CanSeek, Is.False);
            Assert.That(stream.CanWrite, Is.False);

            Assert.Throws<NotSupportedException>(() => { _ = stream.Length; });
            Assert.Throws<NotSupportedException>(() => { _ = stream.Position; });
            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
            Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
            Assert.DoesNotThrow(() => stream.Flush());
        }

        #region Private
        private static IEnumerable<TestCaseData> PaddingCases()
        {
            yield return new TestCaseData("AQ==", new byte[] { 1 });
            yield return new TestCaseData("AQI=", new byte[] { 1, 2 });
            yield return new TestCaseData("AQID", new byte[] { 1, 2, 3 });
        }

        private static IEnumerable<TestCaseData> RoundTripCases()
        {
            foreach (int length in new[] { 0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 31, 32, 63, 64, 127, 128, 255, 256, 1024, 4097 })
            {
                byte[] expected = CreateBytes(length);

                yield return new TestCaseData(expected, 1);
                yield return new TestCaseData(expected, 5);
                yield return new TestCaseData(expected, 8192);
            }
        }

        private static byte[] ReadAll(string encoded, int bufferSize)
        {
            using Base64BodyStream stream = new(encoded);
            using MemoryStream output = new();
            byte[] buffer = new byte[bufferSize];

            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);

            return output.ToArray();
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
