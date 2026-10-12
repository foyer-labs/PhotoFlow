// SPDX-License-Identifier: Apache-2.0

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PhotoFlow.App.Localization;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

public sealed partial class LibrariesView : UserControl
{
    private LibrariesViewModel? _viewModel;

    public LibrariesView()
    {
        InitializeComponent();
        PageKeys.Attach(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FolderPickRequested -= OnFolderPickRequested;
        }

        _viewModel = DataContext as LibrariesViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FolderPickRequested += OnFolderPickRequested;
        }
    }

    private async void OnFolderPickRequested(object? sender, EventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage || _viewModel is null)
        {
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Strings.Get("Libraries.AddFolder") });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await _viewModel.AddLibraryAsync(path);
        }
    }
}
