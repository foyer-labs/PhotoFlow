// SPDX-License-Identifier: Apache-2.0

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PhotoFlow.App.Localization;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

public sealed partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        PageKeys.Attach(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PickRequested -= OnPickRequested;
            _viewModel.ApplyTheme = null;
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PickRequested += OnPickRequested;
            _viewModel.ApplyTheme = App.ApplyTheme;
        }
    }

    private async void OnPickRequested(object? sender, SettingsPick pick)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || _viewModel is null)
        {
            return;
        }

        if (pick == SettingsPick.OfflinePackage)
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Strings.Get("Settings.Models.ImportOffline") });
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder)
            {
                await _viewModel.ImportOfflinePackageAsync(folder);
            }
        }
        else
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Strings.Get("Settings.ExifTool.Other"), AllowMultiple = false });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } file)
            {
                await _viewModel.UseExifToolAsync(file);
            }
        }
    }
}
