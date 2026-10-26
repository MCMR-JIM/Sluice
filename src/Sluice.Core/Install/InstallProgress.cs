namespace Sluice.Install;

/// <summary>
/// The small numbers a progress bar and a status line need while a plan runs. Kept here, apart from the
/// window, so the arithmetic can be checked without a user interface.
/// </summary>
public static class InstallProgress
{
    /// <summary>Completion of a plan as a percentage.</summary>
    /// <param name="completed">Steps that finished, successfully or not.</param>
    /// <param name="total">Steps in the plan.</param>
    /// <returns>Between 0 and 100, or 0 when the plan is empty.</returns>
    public static double Percent(int completed, int total) =>
        total <= 0 ? 0 : Math.Round(Math.Clamp(completed, 0, total) * 100d / total, 1);

    /// <summary>Position of a step in its plan, counting from one.</summary>
    /// <param name="plan">Plan the step belongs to.</param>
    /// <param name="step">Step that just ran.</param>
    /// <returns>The 1 based position, or 0 when the step is not part of the plan.</returns>
    public static int StepNumber(InstallPlan plan, InstallStep step)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(step);

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            if (ReferenceEquals(plan.Steps[index], step) || plan.Steps[index] == step)
            {
                return index + 1;
            }
        }

        return 0;
    }
}
