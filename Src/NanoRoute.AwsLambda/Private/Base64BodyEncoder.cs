/********************************************************************************
* Base64BodyEncoder.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NanoRoute.AwsLambda
{
    internal static class Base64BodyEncoder
    {
        #region Private
        private const int BufferSize = 81920;
        #endregion

        public static async Task<string> EncodeToStringAsync(Stream source, CancellationToken cancellationToken = default)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using Base64BodyWriterStream destination = new();

            await source.CopyToAsync(destination, BufferSize, cancellationToken);

            return destination.GetBody();
        }
    }
}
