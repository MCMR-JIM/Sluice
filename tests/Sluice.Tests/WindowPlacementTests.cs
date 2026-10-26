using Sluice.Windows;
using Xunit;

namespace Sluice.Tests;

public sealed class WindowPlacementTests
{
    private static readonly WindowBox Work = new(0, 0, 1920, 1040);

    [Fact]
    public void KeepsAGoodBoxAsItIs()
    {
        var box = new WindowBox(200, 120, 1180, 760);

        Assert.Equal(box, WindowPlacement.Clamp(box, Work));
    }

    [Fact]
    public void ShrinksAWindowTallerThanTheScreen()
    {
        // A 1366x768 laptop restoring a window remembered on a bigger screen is the real case: the
        // bottom status row ended up below the work area.
        var box = WindowPlacement.Clamp(new WindowBox(0, 0, 1180, 1400), new WindowBox(0, 0, 1366, 728));

        Assert.Equal(728, box.Height);
        Assert.Equal(1180, box.Width);
    }

    [Fact]
    public void PullsAWindowBackFromADetachedMonitor()
    {
        // The remembered position is on a monitor that is not attached any more.
        var box = WindowPlacement.Clamp(new WindowBox(2500, 900, 1180, 760), Work);

        Assert.Equal(Work.Left + Work.Width - 1180, box.Left);
        Assert.Equal(Work.Top + Work.Height - 760, box.Top);
    }

    [Fact]
    public void RespectsAWorkAreaThatDoesNotStartAtTheOrigin()
    {
        var work = new WindowBox(100, 50, 1200, 800);
        var box = WindowPlacement.Clamp(new WindowBox(0, 0, 1180, 760), work);

        Assert.Equal(100, box.Left);
        Assert.Equal(50, box.Top);
    }

    [Fact]
    public void RaisesATinyWindowToTheMinimum()
    {
        var box = WindowPlacement.Clamp(new WindowBox(10, 10, 200, 100), Work);

        Assert.Equal(WindowPlacement.MinimumWidth, box.Width);
        Assert.Equal(WindowPlacement.MinimumHeight, box.Height);
    }

    [Fact]
    public void RemembersAndForgetsGeometry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sluice-window-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Null(WindowPlacementStore.Load(path));

            var box = new WindowBox(120, 80, 1180, 760);
            WindowPlacementStore.Save(box, path);
            Assert.Equal(box, WindowPlacementStore.Load(path));

            // A half written or hand edited file is treated as no memory at all.
            File.WriteAllText(path, "{ not json");
            Assert.Null(WindowPlacementStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
