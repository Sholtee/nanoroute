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

        /// <summary>
        /// Gets or sets the URI scheme used when mapping AWS HTTP API and Lambda Function URL events to
        /// <see cref="System.Net.Http.HttpRequestMessage.RequestUri"/>.
        /// </summary>
        /// <remarks>
        /// The default is <c>https</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// builder.ConfigureRouting(config =&gt; config with
        /// {
        ///     RequestScheme = "https"
        /// });
        /// </code>
        /// </example>
        public string RequestScheme
        {
            get;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                field = value;
            }
        } = "https";

        /// <summary>
        /// Gets or sets the host, optionally including a port, used when mapping AWS HTTP API and Lambda Function URL
        /// events to <see cref="System.Net.Http.HttpRequestMessage.RequestUri"/>.
        /// </summary>
        /// <remarks>
        /// When this value is <see langword="null"/>, the adapter uses <c>requestContext.domainName</c> from the AWS event.
        /// Set this value when your application needs a canonical public host that differs from the event domain name.
        /// </remarks>
        /// <example>
        /// <code>
        /// builder.ConfigureRouting(config =&gt; config with
        /// {
        ///     RequestDomain = "api.example.com"
        /// });
        /// </code>
        /// </example>
        public string? RequestDomain { get; set; }
    }
}
