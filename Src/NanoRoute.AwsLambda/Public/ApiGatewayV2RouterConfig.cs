/********************************************************************************
* ApiGatewayV2RouterConfig.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute.AwsLambda
{
    /// <summary>
    /// Configuration settings shared by the AWS Lambda router adapters.
    /// </summary>
    public sealed record ApiGatewayV2RouterConfig : RouterConfig
    {
        /// <summary>
        /// Gets or sets the amount of time reserved before the Lambda invocation timeout is reached.
        /// </summary>
        public TimeSpan LambdaTimeoutBuffer
        {
            get;
            init
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value));

                field = value;
            }
        } = TimeSpan.FromSeconds(1);
    }
}
