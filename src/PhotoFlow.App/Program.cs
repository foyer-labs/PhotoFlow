// SPDX-License-Identifier: Apache-2.0

using Avalonia;
using Avalonia.Styling;

namespace PhotoFlow.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp(ParseTheme(args)).StartWithClassicDesktopLifetime(args);

    // Parameterless overload used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(null);

    private static AppBuilder BuildAvaloniaApp(ThemeVariant? theme) =>
        AppBuilder.Configure(() => new App(theme))
            .UsePlatformDetect()
            .LogToTrace();

    private static ThemeVariant? ParseTheme(string[] args)
    {
        var index = Array.IndexOf(args, "--theme");
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        return args[index + 1].ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => null,
        };
    }
}
