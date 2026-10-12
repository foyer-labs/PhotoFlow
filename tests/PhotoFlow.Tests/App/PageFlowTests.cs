// SPDX-License-Identifier: Apache-2.0

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PhotoFlow.App.Services;
using PhotoFlow.App.ViewModels;
using PhotoFlow.App.Views;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Paths;
using PhotoFlow.Tests.Support;

namespace PhotoFlow.Tests.App;

/// <summary>
/// The phase 1 flow through the real views (headless): first run, import procedure on a folder, job, review with
/// P/X/Ctrl+Z, confirmation and XMP. Set PHOTOFLOW_SCREENSHOTS to a folder outside the repository to save frames.
/// </summary>
[Collection(AvaloniaCollection.Name)]
[Trait(TestCategory.Name, TestCategory.Integration)]
public sealed class PageFlowTests : IDisposable
{
    private readonly TempFolder _temp = new("pf-flow");

    [AvaloniaFact]
    public async Task First_run_then_import_review_and_xmp_through_the_views()
    {
        var paths = new DefaultAppPaths(_temp.Sub("profile"));
        await using var host = await PhotoFlowHost.StartAsync(paths, CancellationToken.None);
        using var shell = new MainWindowViewModel(host);
        var window = new MainWindow { DataContext = shell, Width = 1440, Height = 900 };
        window.Show();
        await Settle(shell.CurrentPage.ActivateAsync());

        // 3b: first run until a library exists.
        var home = Assert.IsType<HomeViewModel>(shell.CurrentPage);
        Assert.True(home.IsFirstRun);
        Save(window, "3b-first-run");
        var library = _temp.Sub("library");
        home.FirstRun.LibraryRoot = library;
        await Settle(home.FirstRun.FinishCommand.ExecuteAsync(null));
        Assert.True(home.IsNormal);
        Save(window, "3a-home-empty");

        // 3d-3f: import procedure on a folder.
        var folder = SourceFolder();
        home.ImportFolder(folder);
        var wizard = Assert.IsType<ImportWizardViewModel>(shell.CurrentPage);
        await Settle(wizard.ActivateAsync());
        Assert.Equal(4, wizard.SelectedCount);
        wizard.EventName = "Prova import";
        Save(window, "3d-import-source");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.True(wizard.IsStep2);
        Save(window, "3e-import-analysis");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.True(wizard.IsStep3);
        Assert.True(wizard.CanStart);
        Save(window, "3f-import-destinations");
        await Settle(wizard.StartCommand.ExecuteAsync(null));

        // 3g: the job runs to the end.
        var jobs = Assert.IsType<JobsViewModel>(shell.CurrentPage);
        var job = (await host.Catalog.Jobs.ListRecentAsync(1, CancellationToken.None)).Single();
        await WaitAsync(() => !host.Jobs.IsRunning(job.Id));
        await Settle(jobs.ActivateAsync());
        Assert.Equal(JobState.Completed, (await host.Catalog.Jobs.GetAsync(job.Id, CancellationToken.None))!.State);
        Save(window, "3g-job-completed");

        // 2a: review, keyboard decisions and undo.
        shell.OpenReview(job.Id);
        var review = Assert.IsType<ReviewViewModel>(shell.CurrentPage);
        await Settle(review.OpenJobAsync(job.Id));
        Assert.Equal(3, review.Filters[0].Count); // the RAW+JPEG pair is one photo
        Assert.NotNull(review.Selected);
        await WaitAsync(() => review.Selected!.Thumbnail is not null);
        Save(window, "2a-review");

        var first = review.Selected!;
        await Settle(review.DiscardCommand.ExecuteAsync(null));
        Assert.Equal(CullingOutcome.Discard, first.Outcome);
        Assert.True(first.IsManual);
        await Settle(review.UndoCommand.ExecuteAsync(null));
        Assert.False(first.IsManual);
        await Settle(review.KeepCommand.ExecuteAsync(null));
        await Settle(review.RateCommand.ExecuteAsync("4"));
        Assert.Equal(4, first.Stars);
        Assert.Equal(1, review.ManualCount);

        // 2g/2h: confirmation and XMP: stars written to the pair (sidecar for the NEF, embedded in the JPEG).
        review.ConfirmCommand.Execute(null);
        Assert.True(review.IsConfirming);
        Save(window, "2g-confirm");
        await Settle(review.WriteXmpCommand.ExecuteAsync(null));
        Assert.NotNull(review.ResultMessage);
        Assert.Null(review.ResultError);
        Save(window, "2h-written");
        Assert.NotEmpty(Directory.EnumerateFiles(library, "*.xmp", SearchOption.AllDirectories));

        // Home now shows the library and no running job.
        shell.ShowSection(SectionCatalog.Home);
        await Settle(shell.CurrentPage.ActivateAsync());
        Assert.Single(home.Libraries);
        Save(window, "3a-home");
        window.Close();
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>Two photos as RAW+JPEG twins would be too heavy here: one JPEG pair member is simulated by a copy.</summary>
    private string SourceFolder()
    {
        var folder = _temp.Sub("source");
        for (var i = 1; i <= 3; i++)
        {
            SyntheticImages.WriteJpeg(Path.Combine(folder, $"IMG_{i:0000}.JPG"), 640, 480, i);
        }

        // A RAW twin of IMG_0001: TIFF header, analyzed through its JPEG.
        var raw = new byte[64 * 1024];
        new Random(9).NextBytes(raw);
        raw[0] = 0x4D;
        raw[1] = 0x4D;
        raw[2] = 0x00;
        raw[3] = 0x2A;
        File.WriteAllBytes(Path.Combine(folder, "IMG_0001.NEF"), raw);
        return folder;
    }

    private static async Task Settle(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        await task;
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the condition.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var folder = Environment.GetEnvironmentVariable("PHOTOFLOW_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            frame!.Save(Path.Combine(folder, name + ".png"), new PngBitmapEncoderOptions());
        }
    }
}
