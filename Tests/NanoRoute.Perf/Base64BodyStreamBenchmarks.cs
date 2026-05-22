/********************************************************************************
* Base64BodyStreamBenchmarks.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using BenchmarkDotNet.Attributes;

using NanoRoute.AwsLambda;

namespace NanoRoute.Perf
{
    [MemoryDiagnoser]
    public class Base64BodyReaderStreamBenchmarks
    {
        private byte[] _buffer = null!;
        private string _encodedBody = null!;

        [Params(256, 4096, 65536)]
        public int PayloadLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            byte[] payload = CreateBytes(PayloadLength);

            _encodedBody = Convert.ToBase64String(payload);
            _buffer = new byte[65536];
        }

        [Benchmark]
        [Arguments(1)]
        [Arguments(5)]
        [Arguments(8192)]
        public int ReadWithBase64BodyReaderStream(int bufferSize)
        {
            using Base64BodyReaderStream stream = new(_encodedBody);

            int
                total = 0,
                read;

            while ((read = stream.Read(_buffer, 0, bufferSize)) > 0)
                total += read;

            return total;
        }

        [Benchmark(Baseline = true)]
        public int ReadWithConvertFromBase64String() => Convert.FromBase64String(_encodedBody).Length;

        private static byte[] CreateBytes(int count)
        {
            byte[] result = new byte[count];

            for (int i = 0; i < result.Length; i++)
                result[i] = (byte) i;

            return result;
        }
    }

    [MemoryDiagnoser]
    public class Base64BodyWriterStreamBenchmarks
    {
        private byte[] _payload = [];

        [Params(256, 4096, 65536)]
        public int PayloadLength { get; set; }

        [GlobalSetup]
        public void Setup() => _payload = CreateBytes(PayloadLength);

        [Benchmark]
        [Arguments(1)]
        [Arguments(5)]
        [Arguments(8192)]
        public string WriteWithBase64BodyWriterStream(int chunkSize)
        {
            using Base64BodyWriterStream stream = new();

            for (int offset = 0; offset < _payload.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, _payload.Length - offset);

                stream.Write(_payload, offset, count);
            }

            return stream.GetBody();
        }

        [Benchmark(Baseline = true)]
        public string WriteWithConvertToBase64String() => Convert.ToBase64String(_payload);

        private static byte[] CreateBytes(int count)
        {
            byte[] result = new byte[count];

            for (int i = 0; i < result.Length; i++)
                result[i] = (byte) i;

            return result;
        }
    }
}
