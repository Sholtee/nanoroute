/********************************************************************************
* Dtos.cs                                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NanoRoute.TestLambda
{
    [JsonSerializable(typeof(EchoRequest))]
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = false)]
    internal sealed partial class JsonContext : JsonSerializerContext  // cannot be nested =(
    {
    }

    public sealed class EchoRequest
    {
        public string? Message { get; set; }
    }
}
