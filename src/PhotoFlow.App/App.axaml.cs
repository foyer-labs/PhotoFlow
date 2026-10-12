// SPDX-License-Identifier: Apache-2.0

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoFlow.App.ViewModels;
using PhotoFlow.App.Views;
using PhotoFlow.Core.Paths;
using Serilog;

namespace PhotoFlow.App;

public sealed class App : Application
{
    private readonly ThemeVariant? _startupTheme;
    private ServiceProvider? _services;

    public App()
    {
    }

    /// <param name="startupTheme">Theme requested on the command line (<c>--theme light|dark</c>); dark by default.</param>
    public App(ThemeVariant? startupTheme) => _startupTheme = startupTheme;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (_startupTheme is not null)
        {
            RequestedThemeVariant = _startupTheme;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = ConfigureServices();
            var logger = _services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("PhotoFlow starting, version {Version}", typeof(App).Assembly.GetName().Version);

            desktop.MainWindow = new MainWindow { DataContext = _services.GetRequiredService<MainWindowViewModel>() };
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        IAppPaths paths = new DefaultAppPaths();
        var serilog = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "photoflow-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddLogging(builder => builder.AddSerilog(serilog, dispose: true));
        services.AddSingleton<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }
}
