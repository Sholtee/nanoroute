/********************************************************************************
* JsonContext.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NanoRoute.Internals
{
    [JsonSerializable(typeof(ErrorDetails))]
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = false)]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
