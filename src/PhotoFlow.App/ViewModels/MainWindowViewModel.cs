// SPDX-License-Identifier: Apache-2.0

using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoFlow.App.Commands;
using PhotoFlow.App.Localization;

namespace PhotoFlow.App.ViewModels;

/// <summary>Window shell: navigation, title bar, status bar. No content in phase 0.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>At or below this width the navigation collapses to icons automatically (board 04).</summary>
    public const double AutoCompactWidth = 1280;

    public MainWindowViewModel()
    {
        Commands = GlobalCommands.Create();
        StatusHints = Commands.CreateStatusHints();
        MainSections = SectionCatalog.All.Where(s => !s.IsSecondary).ToList();
        SecondarySections = SectionCatalog.All.Where(s => s.IsSecondary).ToList();
        _selectedSection = SectionCatalog.All[0];
    }

    public event EventHandler? SearchFocusRequested;

    public CommandTable Commands { get; }

    public IReadOnlyList<StatusHint> StatusHints { get; }

    public IReadOnlyList<Section> MainSections { get; }

    public IReadOnlyList<Section> SecondarySections { get; }

    public string LibraryName => Strings.Get("TitleBar.NoLibrary");

    public string StatusText => Strings.Get("Status.Ready");

    public string PageEmptyText => Strings.Get("Page.Empty");

    public string SearchShortcut => CommandTable.FormatGesture(Commands.Commands.First(c => c.Id == GlobalCommands.FocusSearch).Gesture);

    public string NavigationToggleIcon => IsNavigationCompact ? "PF.Icon.ChevronRight" : "PF.Icon.ChevronLeft";

    public string NavigationToggleTip => Strings.Get(IsNavigationCompact ? "Nav.Expand" : "Nav.Collapse");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMainSection), nameof(SelectedSecondarySection))]
    private Section _selectedSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNavigationCompact), nameof(NavigationToggleIcon), nameof(NavigationToggleTip))]
    private bool _isNavigationCollapsedByUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNavigationCompact), nameof(NavigationToggleIcon), nameof(NavigationToggleTip))]
    private double _windowWidth = double.MaxValue;

    public bool IsNavigationCompact => IsNavigationCollapsedByUser || WindowWidth <= AutoCompactWidth;

    /// <summary>Selection proxy for the main list (null when a secondary section is active).</summary>
    public Section? SelectedMainSection
    {
        get => SelectedSection.IsSecondary ? null : SelectedSection;
        set
        {
            if (value is not null)
            {
                SelectedSection = value;
            }
        }
    }

    /// <summary>Selection proxy for the secondary list.</summary>
    public Section? SelectedSecondarySection
    {
        get => SelectedSection.IsSecondary ? SelectedSection : null;
        set
        {
            if (value is not null)
            {
                SelectedSection = value;
            }
        }
    }

    public ICommand ResolveCommand(string id) => id switch
    {
        GlobalCommands.Navigate => NavigateCommand,
        GlobalCommands.FocusSearch => FocusSearchCommand,
        GlobalCommands.OpenSettings => OpenSettingsCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown command."),
    };

    [RelayCommand]
    private void Navigate(string? sectionId)
    {
        var section = SectionCatalog.All.FirstOrDefault(s => s.Id == sectionId);
        if (section is not null)
        {
            SelectedSection = section;
        }
    }

    [RelayCommand]
    private void FocusSearch() => SearchFocusRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSettings() => Navigate(SectionCatalog.Settings);

    [RelayCommand]
    private void ToggleNavigation() => IsNavigationCollapsedByUser = !IsNavigationCollapsedByUser;
}
