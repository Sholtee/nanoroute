/********************************************************************************
* ApiGatewayV2RouterConfig.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace NanoRoute.AwsLambda
{
    using System;

    /// <summary>
    /// Configuration settings shared by the AWS Lambda router adapters.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.ConfigureRouting(config =&gt; config with
    /// {
    ///     LambdaTimeoutBuffer = TimeSpan.FromSeconds(2)
    /// });
    /// </code>
    /// </example>
    public sealed record ApiGatewayV2RouterConfig : RouterConfig
    {
        /// <summary>
        /// Gets or sets the amount of time reserved before the Lambda invocation timeout is reached.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the assigned value is negative.</exception>
        /// <example>
        /// <code>
        /// builder.ConfigureRouting(config =&gt; config with
        /// {
        ///     LambdaTimeoutBuffer = TimeSpan.FromSeconds(2)
        /// });
        /// </code>
        /// </example>
        public TimeSpan LambdaTimeoutBuffer
        {
            get;
            init
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
                field = value;
            }
        } = TimeSpan.FromSeconds(1);
    }
}
