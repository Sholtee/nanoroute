/********************************************************************************
* AwsLambdaRouterConfig.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.AwsLambda
{
    using System;

    /// <summary>
    /// Configuration settings shared by the AWS Lambda router adapters.
    /// </summary>
    public sealed record AwsLambdaRouterConfig : RouterConfig
    {
        /// <summary>
        /// Gets or sets the amount of time reserved before the Lambda invocation timeout is reached.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is negative.</exception>
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
