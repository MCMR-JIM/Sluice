namespace Sluice.Install;

/// <summary>
/// What a run may honestly say about the registration it was supposed to check.
/// </summary>
/// <remarks>
/// The install document used to answer <c>verified: true</c> for a run that never checked anything: with
/// <c>--no-verify</c> no registration is read back, so the outcome carries neither a registration nor a
/// problem, and "succeeded and no problem" was printed as verified. Checked, unchecked and failed are three
/// answers, not two.
/// </remarks>
public static class InstallVerdict
{
    /// <summary>True only when the check ran and found the package.</summary>
    /// <param name="succeeded">Whether the deployment steps succeeded.</param>
    /// <param name="problem">Why the check could not run, when it could not.</param>
    /// <param name="requested">Whether a check was asked for at all.</param>
    public static bool Verified(bool succeeded, string? problem, bool requested) =>
        requested && succeeded && problem is null;

    /// <summary>The three states as one word: <c>checked</c>, <c>unverified</c> or <c>skipped</c>.</summary>
    /// <param name="requested">Whether a check was asked for at all.</param>
    /// <param name="problem">Why the check could not run, when it could not.</param>
    public static string Verification(bool requested, string? problem) =>
        !requested ? "skipped" : problem is null ? "checked" : "unverified";
}
