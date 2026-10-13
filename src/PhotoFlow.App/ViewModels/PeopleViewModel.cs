// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoFlow.App.Commands;
using PhotoFlow.App.Localization;
using PhotoFlow.App.Services;
using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Pipeline;
using PhotoFlow.Faces.Protection;
using PhotoFlow.Faces.Recognition;

namespace PhotoFlow.App.ViewModels;

/// <summary>Face crops from the thumbnails (512 px), decoded once per photo.</summary>
public sealed class FaceCrops(PhotoFlowHost host)
{
    private readonly Dictionary<long, Bitmap?> _thumbnails = [];

    public async Task<IImage?> CropAsync(Face face)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (!_thumbnails.TryGetValue(face.AssetId, out var bitmap))
        {
            var asset = await host.Catalog.Assets.GetAsync(face.AssetId, CancellationToken.None);
            var path = asset is null ? null : new[] { asset.FullHash, asset.QuickHash }.OfType<string>().Select(host.Thumbnails.PathFor).FirstOrDefault(File.Exists);
            bitmap = path is null ? null : await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    return new Bitmap(stream);
                }
                catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
                {
                    return null;
                }
            });
            _thumbnails[face.AssetId] = bitmap;
        }

        if (bitmap is null)
        {
            return null;
        }

        // A square around the face, 20 % larger, inside the image.
        var (w, h) = (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var cx = (face.Box.X1 + face.Box.X2) / 2 * w;
        var cy = (face.Box.Y1 + face.Box.Y2) / 2 * h;
        var side = Math.Max((face.Box.X2 - face.Box.X1) * w, (face.Box.Y2 - face.Box.Y1) * h) * 1.2;
        var x = (int)Math.Clamp(cx - (side / 2), 0, w - 1);
        var y = (int)Math.Clamp(cy - (side / 2), 0, h - 1);
        var size = (int)Math.Max(1, Math.Min(side, Math.Min(w - x, h - y)));
        return new CroppedBitmap(bitmap, new PixelRect(x, y, size, size));
    }
}

/// <summary>A person or unnamed group of the list (board 4a).</summary>
public sealed partial class PersonCardViewModel(PersonSummary summary) : ObservableObject
{
    public PersonSummary Summary { get; } = summary;

    public long Id => Summary.Person.Id;

    public bool IsNamed => Summary.Person.Name is not null;

    public string DisplayName => Summary.Person.Name ?? Strings.Format("People.Unnamed", Id);

    public string CountText => Summary.FaceCount.ToString("N0", CultureInfo.CurrentCulture);

    public string Years => Summary.FirstYear is { } first ? (Summary.LastYear is { } last && last != first ? $"{first}–{last}" : first.ToString(CultureInfo.InvariantCulture)) : string.Empty;

    [ObservableProperty]
    private IImage? _cover;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isFocused;
}

public enum PeopleFilter
{
    New,
    Unnamed,
    Named,
    Ignored,
}

/// <summary>
/// People (boards 4a, 4c, 4c2, 4c3): groups ordered by size; new groups of the last job, unnamed, named, ignored;
/// multiple selection; rename with suggestions (F2), merge (M), ignore (I), open the person page (Enter). The
/// thresholds prefer splitting: merging is one key. Without the fingerprint key the gallery stays browsable and
/// recognition waits for the password or a recompute (P13).
/// </summary>
public sealed partial class PeopleViewModel : PageViewModel
{
    private static readonly CommandTable Table = new(
    [
        new("People.Move", new KeyGesture(Key.Left), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "-1"),
        new("People.Move", new KeyGesture(Key.Right), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "1"),
        new("People.Move", new KeyGesture(Key.Up), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "-6"),
        new("People.Move", new KeyGesture(Key.Down), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "6"),
        new("People.Toggle", new KeyGesture(Key.Space), "People.Command.Select", StatusGroupKey: "People.Command.Select"),
        new("People.Rename", new KeyGesture(Key.F2), "People.Command.Rename", StatusGroupKey: "People.Command.Rename"),
        new("People.Merge", new KeyGesture(Key.M), "People.Command.Merge", StatusGroupKey: "People.Command.Merge"),
        new("People.Ignore", new KeyGesture(Key.I), "People.Command.Ignore", StatusGroupKey: "People.Command.Ignore"),
        new("People.Open", new KeyGesture(Key.Enter), "People.Command.Open", StatusGroupKey: "People.Command.Open"),
        new("People.Filter", new KeyGesture(Key.D1, KeyModifiers.Alt), "People.Command.Filter", StatusGroupKey: "People.Command.Filter", Parameter: nameof(PeopleFilter.New)),
        new("People.Filter", new KeyGesture(Key.D2, KeyModifiers.Alt), "People.Command.Filter", Parameter: nameof(PeopleFilter.Unnamed)),
        new("People.Filter", new KeyGesture(Key.D3, KeyModifiers.Alt), "People.Command.Filter", Parameter: nameof(PeopleFilter.Named)),
        new("People.Filter", new KeyGesture(Key.D4, KeyModifiers.Alt), "People.Command.Filter", Parameter: nameof(PeopleFilter.Ignored)),
        new("People.Escape", new KeyGesture(Key.Escape), "Common.Cancel"),
    ]);

    private readonly PhotoFlowHost _host;
    private readonly IShellNavigator _navigator;
    private readonly FaceCrops _crops;
    private List<PersonCardViewModel> _all = [];
    private HashSet<long> _newIds = [];

    public PeopleViewModel(PhotoFlowHost host, IShellNavigator navigator)
    {
        _host = host;
        _navigator = navigator;
        _crops = new FaceCrops(host);
        host.Jobs.StatusChanged += OnStatus;
    }

    /// <summary>Grouping in progress (4c2): the groups already formed stay usable.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGrouping))]
    private string? _groupingText;

    public bool IsGrouping => GroupingText is not null;

    private void OnStatus(object? sender, JobStatus status) => Ui.Post(() =>
    {
        var p = status.Progress;
        var grouping = status.State == JobState.Running && p.Stage == PipelineStage.Faces;
        var wasGrouping = IsGrouping;
        GroupingText = grouping ? Strings.Format("People.Grouping", p.StageCompleted, p.StageTotal) : null;
        if (wasGrouping && !grouping)
        {
            _ = ActivateAsync();
        }
    });

    public override string Title => Strings.Get("Nav.People");

    public override CommandTable Commands => Table;

    public override ICommand ResolveCommand(string id) => id switch
    {
        "People.Move" => MoveCommand,
        "People.Toggle" => ToggleSelectionCommand,
        "People.Rename" => StartRenameCommand,
        "People.Merge" => MergeCommand,
        "People.Ignore" => IgnoreCommand,
        "People.Open" => OpenCommand,
        "People.Filter" => SetFilterCommand,
        "People.Escape" => EscapeCommand,
        _ => base.ResolveCommand(id),
    };

    public ObservableCollection<PersonCardViewModel> Persons { get; } = [];

    /// <summary>Names already given, for the suggestions while renaming.</summary>
    public ObservableCollection<string> Names { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewFilter), nameof(IsUnnamedFilter), nameof(IsNamedFilter), nameof(IsIgnoredFilter))]
    private PeopleFilter _filter = PeopleFilter.New;

    public bool IsNewFilter => Filter == PeopleFilter.New;

    public bool IsUnnamedFilter => Filter == PeopleFilter.Unnamed;

    public bool IsNamedFilter => Filter == PeopleFilter.Named;

    public bool IsIgnoredFilter => Filter == PeopleFilter.Ignored;

    [ObservableProperty]
    private string _newText = string.Empty;

    [ObservableProperty]
    private string _unnamedText = string.Empty;

    [ObservableProperty]
    private string _namedText = string.Empty;

    [ObservableProperty]
    private string _ignoredText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFocus))]
    private PersonCardViewModel? _focused;

    public bool HasFocus => Focused is not null;

    /// <summary>Rename in progress (F2): the text being typed.</summary>
    [ObservableProperty]
    private string? _renameText;

    public bool IsRenaming => RenameText is not null;

    [ObservableProperty]
    private string? _message;

    /// <summary>No face analyzed yet (4c).</summary>
    public bool IsEmpty => _all.Count == 0;

    /// <summary>The fingerprint key is not readable on this account (4c3).</summary>
    [ObservableProperty]
    private bool _isKeyLocked;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public string SelectionText => Strings.Format("People.Selected", _all.Count(p => p.IsSelected));

    public bool HasSelection => _all.Any(p => p.IsSelected);

    partial void OnRenameTextChanged(string? value) => OnPropertyChanged(nameof(IsRenaming));

    public override async Task ActivateAsync()
    {
        var catalog = _host.Catalog;
        IsKeyLocked = _host.FingerprintKeys.State == FingerprintKeyState.Locked;
        var summaries = await catalog.People.ListPersonsAsync(CancellationToken.None);
        var lastJob = (await catalog.Jobs.ListRecentAsync(1, CancellationToken.None)).FirstOrDefault();
        var lastAssets = lastJob is null ? [] : (await catalog.Jobs.GetItemsAsync(lastJob.Id, CancellationToken.None)).Where(i => i.AssetId is not null).Select(i => i.AssetId!.Value).ToHashSet();
        _newIds = [];
        foreach (var summary in summaries.Where(s => s.Person.Name is null && !s.Person.Ignored))
        {
            if ((await catalog.People.ListFacesOfPersonAsync(summary.Person.Id, CancellationToken.None)).Any(f => lastAssets.Contains(f.AssetId)))
            {
                _newIds.Add(summary.Person.Id);
            }
        }

        _all = summaries.Where(s => s.FaceCount > 0).Select(s => new PersonCardViewModel(s)).ToList();
        Names.Clear();
        foreach (var name in summaries.Select(s => s.Person.Name).OfType<string>().Order(StringComparer.CurrentCulture))
        {
            Names.Add(name);
        }

        var named = _all.Count(p => p.IsNamed && !p.Summary.Person.Ignored);
        var unnamed = _all.Count(p => !p.IsNamed && !p.Summary.Person.Ignored);
        var ignored = _all.Count(p => p.Summary.Person.Ignored);
        NewText = Strings.Format("People.Filter.New", lastJob?.EventName ?? string.Empty, _newIds.Count);
        UnnamedText = Strings.Format("People.Filter.Unnamed", unnamed);
        NamedText = Strings.Format("People.Filter.Named", named);
        IgnoredText = Strings.Format("People.Filter.Ignored", ignored);
        SummaryText = Strings.Format("People.Summary", named, unnamed, ignored);
        if (Filter == PeopleFilter.New && _newIds.Count == 0)
        {
            Filter = PeopleFilter.Unnamed;
        }

        await RebuildAsync();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task RebuildAsync()
    {
        var focused = Focused?.Id;
        Persons.Clear();
        foreach (var person in _all.Where(Matches).OrderByDescending(p => p.Summary.FaceCount).ThenBy(p => p.Id))
        {
            Persons.Add(person);
        }

        Focused = Persons.FirstOrDefault(p => p.Id == focused) ?? Persons.FirstOrDefault();
        UpdateSelection();
        foreach (var person in Persons.Take(120))
        {
            if (person.Cover is null)
            {
                person.Cover = await CoverAsync(person.Summary);
            }
        }
    }

    private bool Matches(PersonCardViewModel person) => Filter switch
    {
        PeopleFilter.New => _newIds.Contains(person.Id),
        PeopleFilter.Unnamed => !person.IsNamed && !person.Summary.Person.Ignored,
        PeopleFilter.Named => person.IsNamed && !person.Summary.Person.Ignored,
        _ => person.Summary.Person.Ignored,
    };

    private async Task<IImage?> CoverAsync(PersonSummary summary)
    {
        var faces = await _host.Catalog.People.ListFacesOfPersonAsync(summary.Person.Id, CancellationToken.None);
        var cover = faces.FirstOrDefault(f => f.Id == summary.Person.CoverFaceId)
                    ?? faces.OrderByDescending(f => f.AssignmentOrigin == AssignmentOrigin.Manual).ThenByDescending(f => f.Similarity ?? 0).FirstOrDefault();
        return cover is null ? null : await _crops.CropAsync(cover);
    }

    partial void OnFocusedChanged(PersonCardViewModel? oldValue, PersonCardViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsFocused = false;
        }

        if (newValue is not null)
        {
            newValue.IsFocused = true;
        }
    }

    [RelayCommand]
    private async Task SetFilterAsync(string? filter)
    {
        if (Enum.TryParse<PeopleFilter>(filter, out var value))
        {
            Filter = value;
            await RebuildAsync();
        }
    }

    [RelayCommand]
    private void Move(string? delta)
    {
        if (Persons.Count == 0 || !int.TryParse(delta, CultureInfo.InvariantCulture, out var d))
        {
            return;
        }

        var index = Focused is null ? 0 : Persons.IndexOf(Focused);
        Focused = Persons[Math.Clamp(index + d, 0, Persons.Count - 1)];
    }

    /// <summary>Space or Ctrl+click: adds or removes from the selection.</summary>
    [RelayCommand]
    private void ToggleSelection()
    {
        if (Focused is { } person)
        {
            person.IsSelected = !person.IsSelected;
            UpdateSelection();
        }
    }

    public void Click(PersonCardViewModel person, bool toggle)
    {
        ArgumentNullException.ThrowIfNull(person);
        Focused = person;
        if (toggle)
        {
            ToggleSelection();
        }
    }

    private void UpdateSelection()
    {
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private void StartRename()
    {
        if (Focused is { } person)
        {
            RenameText = person.IsNamed ? person.DisplayName : string.Empty;
        }
    }

    /// <summary>Enter while renaming: the selected groups (or the focused one) take the name; an existing name merges into that person.</summary>
    [RelayCommand]
    private async Task ApplyRenameAsync()
    {
        if (RenameText is not { } text || Focused is not { } focused)
        {
            return;
        }

        var name = text.Trim();
        RenameText = null;
        if (name.Length == 0)
        {
            return;
        }

        var targets = _all.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0)
        {
            targets = [focused];
        }

        var existing = _all.FirstOrDefault(p => string.Equals(p.Summary.Person.Name, name, StringComparison.CurrentCultureIgnoreCase));
        var target = existing?.Id ?? targets[0].Id;
        await _host.Catalog.People.RenameAsync(target, name, CancellationToken.None);
        await _host.Catalog.People.MergeAsync(target, targets.Select(t => t.Id).Where(id => id != target).ToList(), CancellationToken.None);
        await ConfirmGroupAsync(target);
        Message = Strings.Format("People.Renamed", name);
        await ActivateAsync();
    }

    /// <summary>M: the selected groups become one (into the named one, else the largest).</summary>
    [RelayCommand]
    private async Task MergeAsync()
    {
        var selected = _all.Where(p => p.IsSelected).ToList();
        if (selected.Count < 2)
        {
            Message = Strings.Get("People.MergeNeedsTwo");
            return;
        }

        var target = selected.OrderByDescending(p => p.IsNamed).ThenByDescending(p => p.Summary.FaceCount).First();
        await _host.Catalog.People.MergeAsync(target.Id, selected.Select(s => s.Id).Where(id => id != target.Id).ToList(), CancellationToken.None);
        await PeopleEngine.RefreshPrototypesAsync(_host.Catalog, target.Id, CancellationToken.None);
        Message = Strings.Format("People.Merged", selected.Count, target.DisplayName);
        await ActivateAsync();
    }

    /// <summary>I: strangers; their faces stay assigned so that they are not proposed again.</summary>
    [RelayCommand]
    private async Task IgnoreAsync()
    {
        var targets = _all.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0 && Focused is { } focused)
        {
            targets = [focused];
        }

        foreach (var person in targets)
        {
            await _host.Catalog.People.SetIgnoredAsync(person.Id, !person.Summary.Person.Ignored, CancellationToken.None);
        }

        await ActivateAsync();
    }

    [RelayCommand]
    private void Open()
    {
        if (RenameText is not null)
        {
            _ = ApplyRenameAsync();
            return;
        }

        if (Focused is { } person)
        {
            _navigator.ShowPage(new PersonPageViewModel(_host, _navigator, person.Id, _crops));
        }
    }

    [RelayCommand]
    private void Escape()
    {
        if (RenameText is not null)
        {
            RenameText = null;
            return;
        }

        foreach (var person in _all)
        {
            person.IsSelected = false;
        }

        UpdateSelection();
    }

    /// <summary>Naming a group confirms its faces: they become samples of the person (P12).</summary>
    private async Task ConfirmGroupAsync(long personId)
    {
        var faces = await _host.Catalog.People.ListFacesOfPersonAsync(personId, CancellationToken.None);
        await _host.Catalog.People.AssignManualAsync(faces.Where(f => f.AssignmentOrigin != AssignmentOrigin.Manual).Select(f => f.Id).ToList(), personId, CancellationToken.None);
        await PeopleEngine.RefreshPrototypesAsync(_host.Catalog, personId, CancellationToken.None);
    }

    // --- 4c3: key not available ---

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (_host.FingerprintKeys.Unlock(Password))
        {
            Password = string.Empty;
            Message = Strings.Get("People.Key.Unlocked");
            IsKeyLocked = false;
        }
        else
        {
            Message = Strings.Get("People.Key.WrongPassword");
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RecomputeAsync()
    {
        IsBusy = true;
        try
        {
            var (recomputed, missing) = await Task.Run(() => _host.RecomputeFingerprintsAsync(null, null, CancellationToken.None));
            Message = Strings.Format("People.Key.Recomputed", recomputed, missing);
            IsKeyLocked = false;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException)
        {
            Message = Strings.Format("People.Key.RecomputeFailed", e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AnalyzeLibrary() => _navigator.ShowSection(SectionCatalog.Libraries);

    [RelayCommand]
    private void FaceSettings() => _navigator.ShowSection(SectionCatalog.Settings);
}

/// <summary>A face of the person page (board 4b).</summary>
public sealed partial class FaceTileViewModel(Face face, int? year) : ObservableObject
{
    public Face Face { get; } = face;

    public int? Year { get; } = year;

    public string SimilarityText => (Face.Similarity ?? 1).ToString("0.00", CultureInfo.CurrentCulture);

    /// <summary>Low similarity: dashed border, to check (4b).</summary>
    public bool IsDoubtful => Face.AssignmentOrigin != AssignmentOrigin.Manual && Face.Similarity is { } s && s < FaceMatching.ArcFaceThreshold + 0.1;

    public bool IsConfirmed => Face.AssignmentOrigin == AssignmentOrigin.Manual;

    [ObservableProperty]
    private IImage? _crop;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isFocused;
}

/// <summary>A year of the person page with its faces.</summary>
public sealed record FaceYearViewModel(string Title, IReadOnlyList<FaceTileViewModel> Faces);

/// <summary>
/// The page of a person (board 4b): faces by year, the least similar first; select (Space, Shift for a range),
/// separate into a new person (S), move to another person (Ctrl M), "not this person" (Del), cover (C), rename (F2),
/// delete the person with all its fingerprints (the photos stay). Nothing is written in the photos until the review
/// is confirmed.
/// </summary>
public sealed partial class PersonPageViewModel : PageViewModel
{
    private static readonly CommandTable Table = new(
    [
        new("Person.Move", new KeyGesture(Key.Left), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "-1"),
        new("Person.Move", new KeyGesture(Key.Right), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "1"),
        new("Person.Move", new KeyGesture(Key.Up), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "-8"),
        new("Person.Move", new KeyGesture(Key.Down), "People.Command.Navigate", StatusGroupKey: "People.Command.Navigate", Parameter: "8"),
        new("Person.Toggle", new KeyGesture(Key.Space), "People.Command.Select", StatusGroupKey: "People.Command.Select"),
        new("Person.Range", new KeyGesture(Key.Space, KeyModifiers.Shift), "People.Command.Range"),
        new("Person.Separate", new KeyGesture(Key.S), "People.Command.Separate", StatusGroupKey: "People.Command.Separate"),
        new("Person.MoveTo", new KeyGesture(Key.M, KeyModifiers.Control), "People.Command.MoveTo", StatusGroupKey: "People.Command.MoveTo"),
        new("Person.NotThis", new KeyGesture(Key.Delete), "People.Command.NotThis", StatusGroupKey: "People.Command.NotThis"),
        new("Person.Cover", new KeyGesture(Key.C), "People.Command.Cover"),
        new("Person.Rename", new KeyGesture(Key.F2), "People.Command.Rename", StatusGroupKey: "People.Command.Rename"),
        new("Person.Apply", new KeyGesture(Key.Enter), "People.Command.Apply"),
        new("Person.Order", new KeyGesture(Key.O), "People.Command.Order"),
        new("Person.Back", new KeyGesture(Key.Escape), "People.Command.Back", StatusGroupKey: "People.Command.Back"),
    ]);

    private readonly PhotoFlowHost _host;
    private readonly IShellNavigator _navigator;
    private readonly long _personId;
    private readonly FaceCrops _crops;
    private List<FaceTileViewModel> _faces = [];
    private int _anchor;

    public PersonPageViewModel(PhotoFlowHost host, IShellNavigator navigator, long personId, FaceCrops crops)
    {
        _host = host;
        _navigator = navigator;
        _personId = personId;
        _crops = crops;
    }

    public override string Title => Strings.Get("Nav.People");

    public override CommandTable Commands => Table;

    public override ICommand ResolveCommand(string id) => id switch
    {
        "Person.Move" => MoveCommand,
        "Person.Toggle" => ToggleCommand,
        "Person.Range" => RangeCommand,
        "Person.Separate" => SeparateCommand,
        "Person.MoveTo" => StartMoveToCommand,
        "Person.NotThis" => NotThisCommand,
        "Person.Cover" => CoverCommand,
        "Person.Rename" => StartRenameCommand,
        "Person.Apply" => ApplyCommand,
        "Person.Order" => ToggleOrderCommand,
        "Person.Back" => BackCommand,
        _ => base.ResolveCommand(id),
    };

    public ObservableCollection<FaceYearViewModel> Years { get; } = [];

    public ObservableCollection<string> Samples { get; } = [];

    public ObservableCollection<string> Names { get; } = [];

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _leastSimilarFirst = true;

    [ObservableProperty]
    private FaceTileViewModel? _focused;

    /// <summary>Text being typed for a rename (F2) or a move (Ctrl M); null when none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(EditLabel))]
    private string? _editText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditLabel))]
    private bool _isMoving;

    public bool IsEditing => EditText is not null;

    public string EditLabel => Strings.Get(IsMoving ? "People.MoveTo" : "People.Rename");

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isConfirmingDelete;

    public string SelectionText => Strings.Format("People.FacesSelected", _faces.Count(f => f.IsSelected));

    public string MetadataText => Strings.Format("People.Metadata", Name);

    public override async Task ActivateAsync()
    {
        var person = await _host.Catalog.People.GetPersonAsync(_personId, CancellationToken.None);
        if (person is null)
        {
            _navigator.ShowSection(SectionCatalog.People);
            return;
        }

        Name = person.Name ?? Strings.Format("People.Unnamed", person.Id);
        var faces = await _host.Catalog.People.ListFacesOfPersonAsync(_personId, CancellationToken.None);
        var years = new Dictionary<long, int?>();
        foreach (var assetId in faces.Select(f => f.AssetId).Distinct())
        {
            years[assetId] = (await _host.Catalog.Assets.GetAsync(assetId, CancellationToken.None))?.CapturedAt?.Year;
        }

        _faces = faces.Select(f => new FaceTileViewModel(f, years[f.AssetId])).ToList();
        Summary = Strings.Format("People.PageSummary", years.Count, faces.Count,
            years.Values.Where(y => y is not null).DefaultIfEmpty().Min()?.ToString(CultureInfo.InvariantCulture) ?? "—",
            years.Values.Where(y => y is not null).DefaultIfEmpty().Max()?.ToString(CultureInfo.InvariantCulture) ?? "—");
        OnPropertyChanged(nameof(MetadataText));
        Samples.Clear();
        foreach (var group in (await _host.Catalog.People.ListPrototypesAsync(CancellationToken.None)).Where(p => p.PersonId == _personId).GroupBy(p => p.Year).OrderBy(g => g.Key))
        {
            Samples.Add($"{group.Key?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {group.Count()}");
        }

        Names.Clear();
        foreach (var other in (await _host.Catalog.People.ListPersonsAsync(CancellationToken.None)).Select(p => p.Person.Name).OfType<string>().Order(StringComparer.CurrentCulture))
        {
            Names.Add(other);
        }

        await RebuildAsync();
    }

    private async Task RebuildAsync()
    {
        Years.Clear();
        var ordered = LeastSimilarFirst
            ? _faces.OrderBy(f => f.IsConfirmed).ThenBy(f => f.Face.Similarity ?? 1)
            : _faces.OrderByDescending(f => f.Face.Similarity ?? 1);
        foreach (var year in ordered.GroupBy(f => f.Year).OrderByDescending(g => g.Key ?? 0))
        {
            Years.Add(new FaceYearViewModel(Strings.Format("People.Year", year.Key?.ToString(CultureInfo.InvariantCulture) ?? "—", year.Count()), year.ToList()));
        }

        Focused ??= Flat().FirstOrDefault();
        foreach (var tile in Flat().Take(400))
        {
            tile.Crop ??= await _crops.CropAsync(tile.Face);
        }
    }

    private List<FaceTileViewModel> Flat() => Years.SelectMany(y => y.Faces).ToList();

    partial void OnFocusedChanged(FaceTileViewModel? oldValue, FaceTileViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsFocused = false;
        }

        if (newValue is not null)
        {
            newValue.IsFocused = true;
        }
    }

    [RelayCommand]
    private void Move(string? delta)
    {
        var flat = Flat();
        if (flat.Count == 0 || !int.TryParse(delta, CultureInfo.InvariantCulture, out var d))
        {
            return;
        }

        Focused = flat[Math.Clamp((Focused is null ? 0 : flat.IndexOf(Focused)) + d, 0, flat.Count - 1)];
    }

    [RelayCommand]
    private void Toggle()
    {
        if (Focused is { } face)
        {
            face.IsSelected = !face.IsSelected;
            _anchor = Flat().IndexOf(face);
            OnPropertyChanged(nameof(SelectionText));
        }
    }

    /// <summary>Shift+Space (or Shift+click): selects from the last toggled face to the focused one.</summary>
    [RelayCommand]
    private void Range()
    {
        var flat = Flat();
        if (Focused is null || flat.Count == 0)
        {
            return;
        }

        var end = flat.IndexOf(Focused);
        for (var i = Math.Min(_anchor, end); i <= Math.Max(_anchor, end); i++)
        {
            flat[i].IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectionText));
    }

    public void Click(FaceTileViewModel tile, bool toggle, bool range)
    {
        ArgumentNullException.ThrowIfNull(tile);
        Focused = tile;
        if (range)
        {
            Range();
        }
        else if (toggle)
        {
            Toggle();
        }
    }

    private List<long> SelectedFaces()
    {
        var selected = _faces.Where(f => f.IsSelected).Select(f => f.Face.Id).ToList();
        return selected.Count > 0 ? selected : Focused is { } f ? [f.Face.Id] : [];
    }

    /// <summary>S: the selected faces become a new person.</summary>
    [RelayCommand]
    private async Task SeparateAsync()
    {
        var faces = SelectedFaces();
        if (faces.Count == 0)
        {
            return;
        }

        var person = await _host.Catalog.People.CreatePersonAsync(null, CancellationToken.None);
        await _host.Catalog.People.AssignManualAsync(faces, person, CancellationToken.None);
        await RefreshAsync(person);
        Message = Strings.Format("People.Separated", faces.Count);
    }

    /// <summary>Del: "not this person" — the faces leave the person and are not proposed to it again.</summary>
    [RelayCommand]
    private async Task NotThisAsync()
    {
        var faces = SelectedFaces();
        if (faces.Count == 0)
        {
            return;
        }

        await _host.Catalog.People.AssignManualAsync(faces, null, CancellationToken.None);
        await RefreshAsync(null);
        Message = Strings.Format("People.NotThis", faces.Count);
    }

    [RelayCommand]
    private async Task CoverAsync()
    {
        if (Focused is { } face)
        {
            await _host.Catalog.People.SetCoverAsync(_personId, face.Face.Id, CancellationToken.None);
            Message = Strings.Get("People.CoverSet");
        }
    }

    [RelayCommand]
    private void StartRename()
    {
        IsMoving = false;
        EditText = Name.StartsWith(Strings.Format("People.Unnamed", string.Empty).Trim(), StringComparison.Ordinal) ? string.Empty : Name;
    }

    [RelayCommand]
    private void StartMoveTo()
    {
        IsMoving = true;
        EditText = string.Empty;
    }

    /// <summary>Enter: applies the rename or the move being typed.</summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (EditText is not { } text || text.Trim().Length == 0)
        {
            EditText = null;
            return;
        }

        var name = text.Trim();
        EditText = null;
        var people = await _host.Catalog.People.ListPersonsAsync(CancellationToken.None);
        var existing = people.FirstOrDefault(p => string.Equals(p.Person.Name, name, StringComparison.CurrentCultureIgnoreCase))?.Person.Id;
        if (IsMoving)
        {
            var target = existing ?? await _host.Catalog.People.CreatePersonAsync(name, CancellationToken.None);
            var faces = SelectedFaces();
            await _host.Catalog.People.AssignManualAsync(faces, target, CancellationToken.None);
            await RefreshAsync(target);
            Message = Strings.Format("People.Moved", faces.Count, name);
            return;
        }

        if (existing is { } other && other != _personId)
        {
            await _host.Catalog.People.MergeAsync(other, [_personId], CancellationToken.None);
            await PeopleEngine.RefreshPrototypesAsync(_host.Catalog, other, CancellationToken.None);
            _navigator.ShowPage(new PersonPageViewModel(_host, _navigator, other, _crops));
            return;
        }

        await _host.Catalog.People.RenameAsync(_personId, name, CancellationToken.None);
        var all = await _host.Catalog.People.ListFacesOfPersonAsync(_personId, CancellationToken.None);
        await _host.Catalog.People.AssignManualAsync(all.Where(f => f.AssignmentOrigin != AssignmentOrigin.Manual).Select(f => f.Id).ToList(), _personId, CancellationToken.None);
        await RefreshAsync(null);
    }

    [RelayCommand]
    private async Task ToggleOrderAsync()
    {
        LeastSimilarFirst = !LeastSimilarFirst;
        await RebuildAsync();
    }

    [RelayCommand]
    private void RequestDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    /// <summary>Deletes the person: name and every fingerprint of its faces go (§7: no residue); the photos stay.</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        IsConfirmingDelete = false;
        await _host.Catalog.People.DeletePersonAsync(_personId, CancellationToken.None);
        _navigator.ShowSection(SectionCatalog.People);
    }

    [RelayCommand]
    private void Back()
    {
        if (EditText is not null)
        {
            EditText = null;
        }
        else if (IsConfirmingDelete)
        {
            IsConfirmingDelete = false;
        }
        else
        {
            _navigator.ShowSection(SectionCatalog.People);
        }
    }

    private async Task RefreshAsync(long? other)
    {
        await PeopleEngine.RefreshPrototypesAsync(_host.Catalog, _personId, CancellationToken.None);
        if (other is { } id)
        {
            await PeopleEngine.RefreshPrototypesAsync(_host.Catalog, id, CancellationToken.None);
        }

        Focused = null;
        await ActivateAsync();
        OnPropertyChanged(nameof(SelectionText));
    }
}
