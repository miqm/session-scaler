using System.Text.Json.Serialization;

namespace miqm.sbss;


[JsonSourceGenerationOptions(
    WriteIndented = true, 
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowTrailingCommas = true
)]
[JsonSerializable(typeof(ScalerMetadata))]
// ReSharper disable once UnusedType.Global
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
    
}
