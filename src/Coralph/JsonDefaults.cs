using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coralph;

internal static class JsonDefaults
{
    internal static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true
    };

    internal static JsonSerializerOptions IndentedIgnoreNull { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
