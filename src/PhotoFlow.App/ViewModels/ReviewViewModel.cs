// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoFlow.Analysis.Culling;
using PhotoFlow.Analysis.Profiles;
using PhotoFlow.App.Commands;
using PhotoFlow.App.Localization;
using PhotoFlow.App.Services;
using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;

namespace PhotoFlow.App.ViewModels;

/// <summary>A photo of the review grid: the files of a RAW+JPEG pair are one photo.</summary>
public sealed partial class ReviewItemViewModel : ObservableObject
{
    private Bitmap? _thumbnail;
    private bool _loading;

    public ReviewItemViewModel(IReadOnlyList<long> assetIds, long analyzedAssetId, string name, string? thumbnailPath, DateTimeOffset? capturedAt, CullingSignals? signals, ExifSummary? exif)
    {
        AssetIds = assetIds;
        AnalyzedAssetId = analyzedAssetId;
        Name = name;
        ThumbnailPath = thumbnailPath;
        CapturedAt = capturedAt;
        Signals = signals;
        Exif = exif;
    }

    public IReadOnlyList<long> AssetIds { get; }

    public long AnalyzedAssetId { get; }

    public string Name { get; }

    public string? ThumbnailPath { get; }

    public DateTimeOffset? CapturedAt { get; }

    public CullingSignals? Signals { get; }

    public ExifSummary? Exif { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outcome), nameof(OutcomeKey), nameof(IsManual), nameof(Stars), nameof(StarsText), nameof(OutcomeLabel), nameof(OriginLabel), nameof(Why))]
    private Decision? _decision;

    [ObservableProperty]
    private bool _isSelected;

    public string StarsText => Stars > 0 ? new string('★', Stars) : string.Empty;

    public CullingOutcome? Outcome => Decision?.EffectiveOutcome;

    /// <summary>Key of the outcome color/glyph in the views: Discard, ToReview, Best, Similar, Kept or None.</summary>
    public string OutcomeKey => Outcome?.ToString() ?? "None";

    public bool IsManual => Decision?.UserOutcome is not null || Decision?.Rating is not null;

    public int Stars => Decision?.Rating ?? 0;

    public string OutcomeLabel => Outcome is { } o ? Strings.Get("Outcome." + o) : Strings.Get("Outcome.None");

    public string OriginLabel => Strings.Get(IsManual ? "Review.Origin.Manual" : "Review.Origin.Automatic");

    public string Why => Decision is null || Decision.Reasons.Count == 0 || Decision.UserOutcome is not null
        ? string.Empty
        : string.Join(" · ", Decision.Reasons.Select(r => Strings.Get("Reason." + r)));

    /// <summary>Thumbnail decoded on demand (rows are virtualized: only visible photos load).</summary>
    public Bitmap? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !_loading && ThumbnailPath is { } path && File.Exists(path))
            {
                _loading = true;
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var stream = File.OpenRead(path);
                        var bitmap = Bitmap.DecodeToWidth(stream, 320);
                        Ui.Post(() =>
                        {
                            _thumbnail = bitmap;
                            OnPropertyChanged(nameof(Thumbnail));
                        });
                    }
                    catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
                    {
                        // Unreadable thumbnail: the cell stays empty.
                    }
                });
            }

            return _thumbnail;
        }
    }
}

/// <summary>A row of the virtualized grid.</summary>
public sealed record ReviewRowViewModel(IReadOnlyList<ReviewItemViewModel> Items);

/// <summary>Outcome filter chip with its count (board 2a).</summary>
public sealed partial class OutcomeFilterViewModel(CullingOutcome? outcome) : ObservableObject
{
    public CullingOutcome? Outcome { get; } = outcome;

    public string Label => Outcome is { } o ? Strings.Get("Outcome." + o) : Strings.Get("Review.Filter.All");

    public string Key => Outcome?.ToString() ?? "All";

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isActive;
}

/// <summary>A signal bar of the details panel: value, profile threshold, text.</summary>
public sealed record SignalViewModel(string Label, double Value, double? Threshold, string Text);

/// <summary>
/// Review (board 2a, phase 1 subset): grid of the photos of a job in shooting order, outcome filters, P/X/1-5 and
/// Ctrl+Z, details with the signals, confirmation and XMP writing (2g, 2h). User decisions win over the automatic
/// ones (I5); XMP are written only after the confirmation.
/// </summary>
public sealed partial class ReviewViewModel : PageViewModel
{
    private readonly PhotoFlowHost _host;
    private readonly Stack<(IReadOnlyList<Decision?> Before, ReviewItemViewModel Item)> _undo = new();
    private List<ReviewItemViewModel> _all = [];
    private int _columns = 6;
    private string _profileId = "events";

    public ReviewViewModel(PhotoFlowHost host)
    {
        _host = host;
        Filters = [new(null), new(CullingOutcome.ToReview), new(CullingOutcome.Discard), new(CullingOutcome.Kept)];
        Filters[0].IsActive = true;
    }

    public override string Title => Strings.Get("Nav.Review");

    private static readonly CommandTable Table = new(
    [
        new("Review.Move", new KeyGesture(Key.Left), "Review.Command.Navigate", StatusGroupKey: "Review.Command.Navigate", Parameter: "-1,0"),
        new("Review.Move", new KeyGesture(Key.Right), "Review.Command.Navigate", StatusGroupKey: "Review.Command.Navigate", Parameter: "1,0"),
        new("Review.Move", new KeyGesture(Key.Up), "Review.Command.Navigate", StatusGroupKey: "Review.Command.Navigate", Parameter: "0,-1"),
        new("Review.Move", new KeyGesture(Key.Down), "Review.Command.Navigate", StatusGroupKey: "Review.Command.Navigate", Parameter: "0,1"),
        new("Review.Keep", new KeyGesture(Key.P), "Review.Command.Keep", StatusGroupKey: "Review.Command.Keep"),
        new("Review.Discard", new KeyGesture(Key.X), "Review.Command.Discard", StatusGroupKey: "Review.Command.Discard"),
        new("Review.Rate", new KeyGesture(Key.D0), "Review.Command.Stars", Parameter: "0"),
        new("Review.Rate", new KeyGesture(Key.D1), "Review.Command.Stars", StatusGroupKey: "Review.Command.Stars", Parameter: "1"),
        new("Review.Rate", new KeyGesture(Key.D2), "Review.Command.Stars", StatusGroupKey: "Review.Command.Stars", Parameter: "2"),
        new("Review.Rate", new KeyGesture(Key.D3), "Review.Command.Stars", StatusGroupKey: "Review.Command.Stars", Parameter: "3"),
        new("Review.Rate", new KeyGesture(Key.D4), "Review.Command.Stars", StatusGroupKey: "Review.Command.Stars", Parameter: "4"),
        new("Review.Rate", new KeyGesture(Key.D5), "Review.Command.Stars", StatusGroupKey: "Review.Command.Stars", Parameter: "5"),
        new("Review.Undo", new KeyGesture(Key.Z, KeyModifiers.Control), "Review.Command.Undo", StatusGroupKey: "Review.Command.Undo"),
        new("Review.Confirm", new KeyGesture(Key.Enter, KeyModifiers.Control), "Review.Confirm", StatusGroupKey: "Review.Confirm"),
        new("Review.CancelConfirm", new KeyGesture(Key.Escape), "Common.Cancel"),
    ]);

    public override CommandTable Commands => Table;

    public override ICommand ResolveCommand(string id) => id switch
    {
        "Review.Move" => MoveCommand,
        "Review.Keep" => KeepCommand,
        "Review.Discard" => DiscardCommand,
        "Review.Rate" => RateCommand,
        "Review.Undo" => UndoCommand,
        "Review.Confirm" => ConfirmOrWriteCommand,
        "Review.CancelConfirm" => CancelConfirmCommand,
        _ => base.ResolveCommand(id),
    };

    /// <summary>Raised when the selection moves, so that the view scrolls the row into view.</summary>
    public event EventHandler<ReviewRowViewModel>? RowFocusRequested;

    [RelayCommand]
    private void MoveBy(string? delta)
    {
        var parts = (delta ?? "0,0").Split(',');
        Move(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public IRelayCommand<string?> MoveCommand => MoveByCommand;

    /// <summary>Ctrl+Enter: opens the confirmation, or writes when it is already open.</summary>
    [RelayCommand]
    private async Task ConfirmOrWriteAsync()
    {
        if (IsConfirming)
        {
            await WriteXmpAsync();
        }
        else
        {
            Confirm();
        }
    }

    public IReadOnlyList<OutcomeFilterViewModel> Filters { get; }

    public ObservableCollection<ReviewRowViewModel> Rows { get; } = [];

    public ObservableCollection<SignalViewModel> SelectedSignals { get; } = [];

    [ObservableProperty]
    private long? _jobId;

    [ObservableProperty]
    private string _jobTitle = string.Empty;

    [ObservableProperty]
    private string _jobMeta = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedExif))]
    private ReviewItemViewModel? _selected;

    [ObservableProperty]
    private Bitmap? _selectedPreview;

    [ObservableProperty]
    private int _manualCount;

    [ObservableProperty]
    private bool _isConfirming;

    [ObservableProperty]
    private bool _isWriting;

    [ObservableProperty]
    private string? _resultMessage;

    [ObservableProperty]
    private string? _resultError;

    [ObservableProperty]
    private bool _prudentMode = true;

    public bool HasSelection => Selected is not null;

    public bool IsEmpty => _all.Count == 0;

    public string EmptyText => Strings.Get(JobId is null ? "Review.Empty.NoJob" : "Review.Empty.Filter");

    public bool HasRows => Rows.Count > 0;

    public string SelectedExif => Selected?.Exif is { } e
        ? string.Join(" · ", new[]
        {
            e.ExposureTimeSeconds is { } t ? (t >= 1 ? $"{t:0.#} s" : $"1/{Math.Round(1 / t):0} s") : null,
            e.Aperture is { } f ? string.Create(CultureInfo.CurrentCulture, $"f/{f:0.#}") : null,
            e.Iso is { } iso ? $"ISO {iso}" : null,
            e.FocalLengthMm is { } mm ? string.Create(CultureInfo.CurrentCulture, $"{mm:0} mm") : null,
        }.Where(x => x is not null))
        : string.Empty;

    public string ConfirmQuestion => Strings.Format("Review.Confirm.Question", _all.Count.ToString("N0", CultureInfo.CurrentCulture));

    public string ConfirmToReview => Strings.Format("Review.Confirm.ToReview", Count(CullingOutcome.ToReview).ToString("N0", CultureInfo.CurrentCulture));

    public string ConfirmCounts => string.Join(" · ",
        new[] { CullingOutcome.ToReview, CullingOutcome.Kept, CullingOutcome.Discard }
            .Select(o => $"{Count(o).ToString("N0", CultureInfo.CurrentCulture)} {Strings.Get("Outcome." + o)}"));

    public void SetColumns(int columns)
    {
        columns = Math.Max(1, columns);
        if (columns != _columns)
        {
            _columns = columns;
            Rebuild();
        }
    }

    /// <summary>Opens the review of a job (from Home, Jobs or the end of an import).</summary>
    public async Task OpenJobAsync(long jobId)
    {
        JobId = jobId;
        Selected = null;
        await ActivateAsync();
    }

    partial void OnJobIdChanged(long? value) => Selected = null;

    public override async Task ActivateAsync()
    {
        var catalog = _host.Catalog;
        PrudentMode = _host.Settings.PrudentMode;
        JobId ??= (await catalog.Jobs.ListRecentAsync(10, CancellationToken.None)).FirstOrDefault(j => j.State == JobState.Completed || j.Type == JobType.Import)?.Id;
        if (JobId is not { } jobId || await catalog.Jobs.GetAsync(jobId, CancellationToken.None) is not { } job)
        {
            _all = [];
            Rebuild();
            return;
        }

        _profileId = job.ProfileId ?? "events";
        var items = (await catalog.Jobs.GetItemsAsync(jobId, CancellationToken.None)).Where(i => i.AssetId is not null).ToList();
        var photos = new List<ReviewItemViewModel>();
        foreach (var group in items.GroupBy(i => Path.Combine(Path.GetDirectoryName(i.SourcePath) ?? string.Empty, Path.GetFileNameWithoutExtension(i.SourcePath)), StringComparer.OrdinalIgnoreCase))
        {
            var assetIds = group.Select(i => i.AssetId!.Value).Distinct().ToList();
            AnalyzerResultRecord? result = null;
            long analyzed = assetIds[0];
            foreach (var id in assetIds)
            {
                if (await catalog.Analysis.GetResultAsync(id, CullingAnalyzer.AnalyzerId, CancellationToken.None) is { } r)
                {
                    (result, analyzed) = (r, id);
                    break;
                }
            }

            var asset = await catalog.Assets.GetAsync(analyzed, CancellationToken.None);
            if (asset is null || asset.Handler == "video")
            {
                continue;
            }

            var name = string.Join(" + ", group.Select(i => Path.GetExtension(i.SourcePath).TrimStart('.').ToUpperInvariant()).Distinct());
            var item = new ReviewItemViewModel(
                assetIds, analyzed, $"{Path.GetFileNameWithoutExtension(group.First().SourcePath)} · {name}",
                asset.FullHash is { } hash ? _host.Thumbnails.PathFor(hash) : null, asset.CapturedAt,
                result is null ? null : SafeSignals(result.Scores), asset.Exif)
            {
                Decision = await catalog.Decisions.GetAsync(analyzed, CancellationToken.None),
            };
            photos.Add(item);
        }

        _all = [.. photos.OrderBy(p => p.CapturedAt ?? DateTimeOffset.MaxValue).ThenBy(p => p.Name, StringComparer.Ordinal)];
        JobTitle = string.IsNullOrWhiteSpace(job.EventName) ? Strings.Get("Job.Unnamed") : job.EventName;
        JobMeta = Strings.Format("Review.Meta", job.EventDate?.ToString("d MMM yyyy", CultureInfo.CurrentCulture) ?? string.Empty,
            _all.Count.ToString("N0", CultureInfo.CurrentCulture), Strings.Get("Profile." + (job.ProfileId ?? "events")),
            Strings.Get(PrudentMode ? "Review.Mode.Prudent" : "Review.Mode.Normal"));
        Rebuild();
        Selected ??= Visible().FirstOrDefault();
    }

    partial void OnSelectedChanged(ReviewItemViewModel? oldValue, ReviewItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
            if (Rows.FirstOrDefault(r => r.Items.Contains(newValue)) is { } row)
            {
                RowFocusRequested?.Invoke(this, row);
            }
        }

        ShowDetails(newValue);
    }

    private void ShowDetails(ReviewItemViewModel? value)
    {
        SelectedSignals.Clear();
        SelectedPreview = null;
        if (value is null)
        {
            return;
        }

        var profile = BuiltInProfiles.All.FirstOrDefault(p => p.Id == _profileId) ?? BuiltInProfiles.All[0];
        if (value.Signals is { } s)
        {
            var t = profile.Thresholds;
            SelectedSignals.Add(new(Strings.Get("Signal.Sharpness." + s.Zone), s.ZoneSharpness, t.BlurReview, s.ZoneSharpness.ToString("0.00", CultureInfo.CurrentCulture)));
            SelectedSignals.Add(new(Strings.Get("Signal.SharpnessFrame"), s.FrameSharpness, t.BlurDiscard, s.FrameSharpness.ToString("0.00", CultureInfo.CurrentCulture)));
            SelectedSignals.Add(new(Strings.Get("Signal.Highlights"), s.ZoneHighlights, t.HighlightsReview, s.ZoneHighlights.ToString("P0", CultureInfo.CurrentCulture)));
            SelectedSignals.Add(new(Strings.Get("Signal.Shadows"), s.ZoneShadows, t.ShadowsReview, s.ZoneShadows.ToString("P0", CultureInfo.CurrentCulture)));
            if (s.MainFaceLeftEyeRatio is { } left && s.MainFaceRightEyeRatio is { } right)
            {
                var eyes = Math.Min(left, right);
                SelectedSignals.Add(new(Strings.Get("Signal.Eyes"), Math.Min(1, eyes / 0.4), t.EyesClosed / 0.4,
                    $"{Strings.Get("Eye." + s.MainFaceLeftEye)} / {Strings.Get("Eye." + s.MainFaceRightEye)}"));
            }
        }

        var path = value.ThumbnailPath;
        if (path is not null && File.Exists(path))
        {
            _ = Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    var bitmap = new Bitmap(stream);
                    Ui.Post(() =>
                    {
                        if (Selected == value)
                        {
                            SelectedPreview = bitmap;
                        }
                    });
                }
                catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
                {
                    // No preview.
                }
            });
        }
    }

    [RelayCommand]
    private void SetFilter(OutcomeFilterViewModel? filter)
    {
        foreach (var f in Filters)
        {
            f.IsActive = f == filter || (filter is null && f.Outcome is null);
        }

        Rebuild();
        if (Selected is not null && !Visible().Contains(Selected))
        {
            Selected = Visible().FirstOrDefault();
        }
    }

    [RelayCommand]
    private void Select(ReviewItemViewModel? item) => Selected = item;

    /// <summary>Arrow keys: ±1 within the shooting order, ±one row up/down.</summary>
    public void Move(int dx, int dy)
    {
        var visible = Visible();
        if (visible.Count == 0)
        {
            return;
        }

        var index = Selected is null ? -1 : visible.IndexOf(Selected);
        index = Math.Clamp(index < 0 ? 0 : index + dx + (dy * _columns), 0, visible.Count - 1);
        Selected = visible[index];
    }

    [RelayCommand]
    private Task KeepAsync() => DecideAsync(CullingOutcome.Kept, keepStars: true);

    [RelayCommand]
    private Task DiscardAsync() => DecideAsync(CullingOutcome.Discard, keepStars: false);

    [RelayCommand]
    private async Task RateAsync(string? stars)
    {
        if (Selected is not { } item || !int.TryParse(stars, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        await ApplyAsync(item, d => (d?.UserOutcome, value is >= 1 and <= 5 ? value : null, d?.Label));
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (!_undo.TryPop(out var entry))
        {
            return;
        }

        for (var i = 0; i < entry.Item.AssetIds.Count; i++)
        {
            var before = entry.Before[i];
            await _host.Catalog.Decisions.SetUserAsync(entry.Item.AssetIds[i], before?.UserOutcome, before?.Rating, before?.Label, CancellationToken.None);
        }

        entry.Item.Decision = await _host.Catalog.Decisions.GetAsync(entry.Item.AnalyzedAssetId, CancellationToken.None);
        Selected = entry.Item;
        RefreshCounts();
    }

    [RelayCommand]
    private void Confirm()
    {
        if (_all.Count > 0)
        {
            ResultMessage = null;
            ResultError = null;
            IsConfirming = true;
            OnPropertyChanged(nameof(ConfirmQuestion));
            OnPropertyChanged(nameof(ConfirmToReview));
            OnPropertyChanged(nameof(ConfirmCounts));
        }
    }

    [RelayCommand]
    private void CancelConfirm() => IsConfirming = false;

    [RelayCommand]
    private void ShowToReview()
    {
        IsConfirming = false;
        SetFilter(Filters.First(f => f.Outcome == CullingOutcome.ToReview));
    }

    [RelayCommand]
    private async Task WriteXmpAsync()
    {
        IsConfirming = false;
        IsWriting = true;
        try
        {
            if (PrudentMode != _host.Settings.PrudentMode)
            {
                await _host.UpdateSettingsAsync(s => s with { PrudentMode = PrudentMode }, CancellationToken.None);
            }

            var assets = _all.SelectMany(i => i.AssetIds).Distinct().ToList();
            var summary = await Task.Run(() => new XmpPublisher(_host.Catalog, _host.MetadataWriter).WriteAsync(assets, PrudentMode, null, CancellationToken.None));
            ResultMessage = Strings.Format("Review.Result.Written", DateTime.Now.ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture), summary.Written.ToString("N0", CultureInfo.CurrentCulture));
            ResultError = summary.Failed.Count == 0 ? null
                : Strings.Format("Review.Result.Failed", summary.Failed.Count, Path.GetDirectoryName(summary.Failed[0].Path) ?? string.Empty, summary.Failed[0].Error);
        }
        finally
        {
            IsWriting = false;
        }
    }

    private Task DecideAsync(CullingOutcome outcome, bool keepStars) =>
        Selected is { } item ? ApplyAsync(item, d => (outcome, keepStars ? d?.Rating : null, d?.Label)) : Task.CompletedTask;

    private async Task ApplyAsync(ReviewItemViewModel item, Func<Decision?, (CullingOutcome? Outcome, int? Rating, string? Label)> change)
    {
        var before = new List<Decision?>();
        foreach (var id in item.AssetIds)
        {
            before.Add(await _host.Catalog.Decisions.GetAsync(id, CancellationToken.None));
        }

        _undo.Push((before, item));
        var (outcome, rating, label) = change(item.Decision);
        foreach (var id in item.AssetIds)
        {
            await _host.Catalog.Decisions.SetUserAsync(id, outcome, rating, label, CancellationToken.None);
        }

        item.Decision = await _host.Catalog.Decisions.GetAsync(item.AnalyzedAssetId, CancellationToken.None);
        RefreshCounts();
    }

    private List<ReviewItemViewModel> Visible()
    {
        var filter = Filters.FirstOrDefault(f => f.IsActive)?.Outcome;
        return filter is null ? _all : _all.Where(i => i.Outcome == filter).ToList();
    }

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var chunk in Visible().Chunk(_columns))
        {
            Rows.Add(new ReviewRowViewModel(chunk));
        }

        RefreshCounts();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void RefreshCounts()
    {
        foreach (var filter in Filters)
        {
            filter.Count = filter.Outcome is null ? _all.Count : Count(filter.Outcome.Value);
        }

        ManualCount = _all.Count(i => i.IsManual);
    }

    private int Count(CullingOutcome outcome) => _all.Count(i => i.Outcome == outcome);

    private static CullingSignals? SafeSignals(IReadOnlyDictionary<string, double> scores)
    {
        try
        {
            return CullingScores.FromScores(scores);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
