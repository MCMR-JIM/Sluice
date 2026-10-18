using System.Text.Json;

namespace StoreDownloader.Cli.Cli;

/// <summary>Shared JSON serialisation settings for <c>--json</c> output.</summary>
internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes <paramref name="value"/> as indented JSON to standard output.</summary>
    /// <param name="value">Value to serialise.</param>
    /// <param name="writer">Destination writer, standard output by default.</param>
    public static void Write(object value, TextWriter? writer = null)
    {
        (writer ?? Console.Out).WriteLine(JsonSerializer.Serialize(value, value.GetType(), Options));
    }
}
