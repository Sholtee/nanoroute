/********************************************************************************
* RouterConfig.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace NanoRoute
{
    /// <summary>
    /// Configures runtime behavior of <see cref="Router"/> instances.
    /// </summary>
    public class RouterConfig
    {
        /// <summary>
        /// Gets or sets how NanoRoute prioritizes literal and parameterized child segments at the same depth.
        /// </summary>
        public MatchingBehavior MatchingBehavior { get; set; }

        /// <summary>
        /// Gets or sets the maximum time a request may spend in the router pipeline before its linked cancellation
        /// token is canceled.
        /// </summary>
        /// <remarks>
        /// The default is one minute. Handlers and asynchronous parameter parsers should observe the linked
        /// cancellation token exposed through <see cref="RequestContext.Cancellation"/> and
        /// <see cref="ParameterParserContext.Cancellation"/> if they want timeout expiration to stop their work
        /// promptly.
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
    }
}
