/********************************************************************************
* ErrorDetails.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Net;

namespace NanoRoute
{
    /// <summary>
    /// Describes an HTTP error in a structured, serializer-agnostic format.
    /// </summary>
    /// <remarks>
    /// Instances of this type can be serialized to JSON, XML, or any other format chosen by the caller.
    /// NanoRoute's JSON helpers use it as the default payload shape for error responses.
    /// </remarks>
    /// <example>
    /// <code>
    /// var details = new ErrorDetails
    /// {
    ///     Title = "Bad Request",
    ///     Status = HttpStatusCode.BadRequest,
    ///     TraceId = "01HF...",
    ///     Errors = ["The id field is required."]
    /// };
    /// </code>
    /// </example>
    public sealed class ErrorDetails
    {
        /// <summary>
        /// Short, human readable description of the error.
        /// </summary>
        public required string Title { get; init; }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public required HttpStatusCode Status { get; init; }

        /// <summary>
        /// Unique identifier of the request (logs entries should contain the same id).
        /// </summary>
        public required string TraceId { get; init; }

        /// <summary>
        /// Detailed error information (should NOT contain sensitive information).
        /// </summary>
        public IEnumerable<string>? Errors { get; init; }

        /// <summary>
        /// Messages to the devs (may contain sensitive information). Should NOT be set in production environment.
        /// </summary>
        public IEnumerable<string>? DeveloperMessages { get; init; }
    }
}
