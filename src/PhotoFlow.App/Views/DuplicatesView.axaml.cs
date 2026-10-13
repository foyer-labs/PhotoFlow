// SPDX-License-Identifier: Apache-2.0

using Avalonia.Controls;
using Avalonia.Input;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

public sealed partial class DuplicatesView : UserControl
{
    public DuplicatesView()
    {
        InitializeComponent();
        PageKeys.Attach(this);
    }

    private void OnGroupPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: DuplicateGroupItem item } && DataContext is DuplicatesViewModel viewModel)
        {
            viewModel.Click(item);
            Focus();
        }
    }
}
