/********************************************************************************
* Base64BodyEncoderTests.cs                                                     *
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
    internal sealed class Base64BodyEncoderTests
    {
        [TestCaseSource(nameof(RoundTripCases))]
        public async Task EncodeToStringAsync_ShouldMatchBclBase64Encoding(byte[] bytes)
        {
            using MemoryStream source = new(bytes);

            Assert.That(await Base64BodyEncoder.EncodeToStringAsync(source), Is.EqualTo(Convert.ToBase64String(bytes)));
        }

        [Test]
        public void EncodeToStringAsync_ShouldThrowWhenSourceIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => Base64BodyEncoder.EncodeToStringAsync(null!));
        }

        [Test]
        public void EncodeToStringAsync_ShouldReturnCanceledTaskWhenCancellationIsRequested()
        {
            using MemoryStream source = new(new byte[1]);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await Base64BodyEncoder.EncodeToStringAsync(source, cts.Token));
        }

        #region Private
        private static IEnumerable<TestCaseData> RoundTripCases()
        {
            foreach (int length in new[] { 0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 31, 32, 63, 64, 127, 128, 255, 256, 1024, 4097 })
                yield return new TestCaseData(CreateBytes(length));
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
