/********************************************************************************
* ErrorDetails.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace NanoRoute
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class ErrorDetails
    {
        /// <summary>
        /// Short, human readable description of the error.
        /// </summary>
        public required string Title { get; init; }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public required int Status { get; init; }

        /// <summary>
        /// Unique identifier of the request (logs entries should contain the same id).
        /// </summary>
        public required string TraceId { get; init; }

        /// <summary>
        /// Detailed error information (should NOT contain sensitive information).
        /// </summary>
        public IEnumerable<string>? Errors { get; init; }

        /// <summary>
        /// Message to the devs (may contain sensitive information). Won't be set in production environment.
        /// </summary>
        public IEnumerable<string>? DeveloperMessage { get; init; }
    }
}
