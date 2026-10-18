using StoreDownloader.Install;

namespace StoreDownloader.Desktop.ViewModels;

/// <summary>One step of the install plan as the list shows it.</summary>
public sealed class PlanRow(Localization loc) : ObservableObject
{
    private string _state = "pending";
    private string? _detail;

    public required int Order { get; init; }

    public required InstallStep Step { get; init; }

    public string KindLabel => loc.Get(Step.Kind switch
    {
        InstallStepKind.InstallFramework => "KindFramework",
        InstallStepKind.InstallMain => "KindMain",
        InstallStepKind.InstallResource => "KindResource",
        _ => "KindVerify",
    });

    public string FileName => Step.FileName;

    public string Reason => Step.Reason;

    public string State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                Raise(nameof(StateLabel));
                Raise(nameof(IsDone));
                Raise(nameof(IsFailed));
            }
        }
    }

    public string StateLabel => loc.Get(State switch
    {
        "ok" => "StateOk",
        "failed" => "StateFailed",
        "running" => "StateRunning",
        _ => "StatePending",
    });

    public string? Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    public bool IsDone => State == "ok";

    public bool IsFailed => State == "failed";

    /// <summary>Re-reads the label after the language changed.</summary>
    public void RefreshLanguage()
    {
        Raise(nameof(KindLabel));
        Raise(nameof(StateLabel));
    }
}
