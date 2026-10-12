// SPDX-License-Identifier: Apache-2.0

using Avalonia.Controls;
using Avalonia.Input;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

public sealed partial class ReviewView : UserControl
{
    /// <summary>Cell width plus its right margin (Border.cell in the view styles).</summary>
    public const double CellStride = 208;

    private ReviewViewModel? _viewModel;

    public ReviewView()
    {
        InitializeComponent();
        PageKeys.Attach(this);
        RowsList.SizeChanged += (_, e) => UpdateColumns(e.NewSize.Width);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.RowFocusRequested -= OnRowFocusRequested;
        }

        _viewModel = DataContext as ReviewViewModel;
        if (_viewModel is not null)
        {
            _viewModel.RowFocusRequested += OnRowFocusRequested;
            UpdateColumns(RowsList.Bounds.Width);
        }
    }

    private void UpdateColumns(double width)
    {
        if (_viewModel is not null && width > 0)
        {
            _viewModel.SetColumns((int)Math.Floor((width - RowsList.Padding.Left - RowsList.Padding.Right) / CellStride));
        }
    }

    private void OnRowFocusRequested(object? sender, ReviewRowViewModel row) => RowsList.ScrollIntoView(row);

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: ReviewItemViewModel item } && _viewModel is not null)
        {
            _viewModel.SelectCommand.Execute(item);
            Focus();
        }
    }
}
