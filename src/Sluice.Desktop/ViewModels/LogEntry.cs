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

/// <summary>
/// One line of the activity log. Lines built from a message key or a core diagnostic keep the recipe
/// they were made from, so a language change re-renders the whole transcript instead of leaving the
/// old language behind in the middle of the list.
/// </summary>
public sealed class LogEntry : ObservableObject
{
    private readonly Localization _loc;
    private readonly Func<Localization, string>? _render;
    private string _text;

    private LogEntry(Localization loc, LogLevel level, Func<Localization, string>? render, string text)
    {
        _loc = loc;
        Level = level;
        _render = render;
        _text = text;
        Timestamp = DateTimeOffset.Now;
    }

    /// <summary>Line whose text is final, for example raw tool output.</summary>
    /// <param name="loc">Localization the line belongs to.</param>
    /// <param name="level">Severity shown in the gutter.</param>
    /// <param name="text">Text to show as written.</param>
    public static LogEntry Plain(Localization loc, LogLevel level, string text) => new(loc, level, null, text);

    /// <summary>Line that can be re-rendered when the language changes.</summary>
    /// <param name="loc">Localization the line belongs to.</param>
    /// <param name="level">Severity shown in the gutter.</param>
    /// <param name="render">Recipe that turns a language into the text of this line.</param>
    public static LogEntry Rendered(Localization loc, LogLevel level, Func<Localization, string> render) =>
        new(loc, level, render, render(loc));

    public DateTimeOffset Timestamp { get; }

    public LogLevel Level { get; }

    public string Time => Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>The severity in the current language.</summary>
    public string LevelText => _loc.Get(Level switch
    {
        LogLevel.Success => "LogLevelSuccess",
        LogLevel.Warning => "LogLevelWarning",
        LogLevel.Error => "LogLevelError",
        LogLevel.Command => "LogLevelCommand",
        _ => "LogLevelInfo",
    });

    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    /// <summary>Re-renders this line after the language changed.</summary>
    public void RefreshLanguage()
    {
        if (_render is not null)
        {
            Text = _render(_loc);
        }

        Raise(nameof(LevelText));
    }

    public override string ToString() => $"{Time}  {Text}";
}
