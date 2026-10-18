using System.Globalization;

namespace Sluice.Desktop.ViewModels;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Command,
}

/// <summary>One line of the activity log.</summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Text)
{
    public string Time => Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public override string ToString() => $"{Time}  {Text}";
}
