namespace Sluice.Cli.Cli;

/// <summary>Values the CLI uses when the operator does not supply one.</summary>
internal static class Defaults
{
    /// <summary>
    /// Locale used for resource packages: the Windows display language, so a Chinese machine gets
    /// Chinese resources without extra flags.
    /// </summary>
    public static string Locale => Store.LocaleTag.Default;
}
