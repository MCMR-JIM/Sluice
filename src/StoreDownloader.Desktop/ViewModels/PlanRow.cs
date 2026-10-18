using StoreDownloader.Install;

namespace StoreDownloader.Desktop.ViewModels;

/// <summary>One step of the install plan as the list shows it.</summary>
public sealed class PlanRow : ObservableObject
{
    private string _state = "pending";
    private string? _detail;

    public required int Order { get; init; }

    public required InstallStep Step { get; init; }

    public string KindLabel => Step.Kind switch
    {
        InstallStepKind.InstallFramework => "框架",
        InstallStepKind.InstallMain => "主程序",
        InstallStepKind.InstallResource => "资源包",
        _ => "校验",
    };

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

    public string StateLabel => State switch
    {
        "ok" => "完成",
        "failed" => "失败",
        "running" => "进行中",
        _ => "等待",
    };

    public string? Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    public bool IsDone => State == "ok";

    public bool IsFailed => State == "failed";
}
