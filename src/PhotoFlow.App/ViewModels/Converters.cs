// SPDX-License-Identifier: Apache-2.0

using Avalonia.Data.Converters;
using PhotoFlow.App.Localization;
using PhotoFlow.Core.Pipeline;

namespace PhotoFlow.App.ViewModels;

/// <summary>Small value converters of the views.</summary>
public static class Converters
{
    /// <summary>A count greater than zero (visibility of optional sections).</summary>
    public static readonly IValueConverter Positive = new FuncValueConverter<int, bool>(count => count > 0);

    /// <summary>Stages of later phases are dimmed (F1-6).</summary>
    public static readonly IValueConverter ActiveOpacity = new FuncValueConverter<bool, double>(active => active ? 1.0 : 0.4);

    /// <summary>True when the outcome key equals the parameter (outcome color classes of the grid).</summary>
    public static readonly IValueConverter IsOutcome = new FuncValueConverter<string, string, bool>((key, expected) => key == expected);

    /// <summary>Width in pixels of the signal bars of the details panel.</summary>
    public const double SignalBarWidth = 96;

    /// <summary>Position of the profile threshold tick on a signal bar.</summary>
    public static readonly IValueConverter ThresholdOffset = new FuncValueConverter<double?, double>(t => Math.Clamp(t ?? -1, -1, 1) * SignalBarWidth);

    /// <summary>Label of an archive analysis order.</summary>
    public static readonly IValueConverter OrderLabel = new FuncValueConverter<ArchiveOrder, string>(order => Strings.Get("Libraries.Order." + order));
}
