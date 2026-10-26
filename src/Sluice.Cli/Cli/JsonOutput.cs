using System.Text.Json;

namespace Sluice.Cli.Cli;

/// <summary>Shared JSON serialisation settings for <c>--json</c> output.</summary>
internal static class JsonOutput
{
    /// <remarks>
    /// Keys are camelCase everywhere. Documents used to be written with whatever name the anonymous type
    /// or record carried, so <c>doctor --json</c> emitted <c>IsWindows</c> and <c>plan --json</c> emitted
    /// <c>issues[].Message.Code</c> while every hand written document emitted <c>succeeded</c> and
    /// <c>kind</c>: a script had to know which document it was reading before it could name a field.
    /// Only keys are changed - values keep the spelling Windows uses, such as <c>InstallMain</c>.
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
