// SPDX-License-Identifier: Apache-2.0

using Avalonia.Controls;
using PhotoFlow.App.ViewModels;

namespace PhotoFlow.App.Views;

/// <summary>Installs the page's command table as key bindings of the page view and gives it the keyboard focus.</summary>
public static class PageKeys
{
    public static void Attach(UserControl view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.Focusable = true;
        view.DataContextChanged += (_, _) => Install(view);
        view.AttachedToVisualTree += (_, _) => view.Focus();
    }

    private static void Install(UserControl view)
    {
        view.KeyBindings.Clear();
        if (view.DataContext is PageViewModel { Commands: { } table } page)
        {
            foreach (var binding in table.CreateKeyBindings(page.ResolveCommand))
            {
                view.KeyBindings.Add(binding);
            }
        }
    }
}
