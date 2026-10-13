// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

public sealed partial class PeopleView : UserControl
{
    private PeopleViewModel? _viewModel;

    public PeopleView()
    {
        InitializeComponent();
        PageKeys.Attach(this);
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = DataContext as PeopleViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelChanged;
            }
        };
    }

    /// <summary>F2 moves the focus into the name field; Enter or Esc gives it back to the page.</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PeopleViewModel.IsRenaming) || _viewModel is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel.IsRenaming)
            {
                RenameBox.Focus();
            }
            else
            {
                Focus();
            }
        });
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: PersonCardViewModel person } && _viewModel is not null)
        {
            _viewModel.Click(person, e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta));
            Focus();
        }
    }

    private void OnCardDoubleTapped(object? sender, TappedEventArgs e) => _viewModel?.OpenCommand.Execute(null);
}
