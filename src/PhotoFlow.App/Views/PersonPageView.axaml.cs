// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

public sealed partial class PersonPageView : UserControl
{
    private PersonPageViewModel? _viewModel;

    public PersonPageView()
    {
        InitializeComponent();
        PageKeys.Attach(this);
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = DataContext as PersonPageViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelChanged;
            }
        };
    }

    /// <summary>F2 / Ctrl M move the focus into the name field; Enter or Esc gives it back to the page.</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PersonPageViewModel.IsEditing) || _viewModel is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel.IsEditing)
            {
                EditBox.Focus();
            }
            else
            {
                Focus();
            }
        });
    }

    private void OnFacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: FaceTileViewModel tile } && _viewModel is not null)
        {
            _viewModel.Click(tile, e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            Focus();
        }
    }
}
