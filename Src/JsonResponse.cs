/********************************************************************************
* JsonResponse.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json.Serialization;

namespace NanoRoute
{
    /// <summary>
    /// JSON response for "internal server error" and "not found" events
    /// </summary>
    /// <remarks>This cannot be nested within the <see cref="Router{TRequest, TResponse}"/> class as it is used as an argument in <see cref="JsonSerializableAttribute"/>.</remarks>
    internal sealed class JsonResponse
    {
        public required string Message { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reason { get; init; }
    }

    [JsonSerializable(typeof(JsonResponse))]
    internal sealed partial class JsonContext : JsonSerializerContext
    {
    }
}
