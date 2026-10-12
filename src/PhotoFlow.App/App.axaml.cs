// SPDX-License-Identifier: Apache-2.0

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoFlow.App.Localization;
using PhotoFlow.App.Services;
using PhotoFlow.App.ViewModels;
using PhotoFlow.Core.Catalog;
using PhotoFlow.App.Views;
using PhotoFlow.Core.Paths;
using Serilog;

namespace PhotoFlow.App;

public sealed class App : Application
{
    private readonly ThemeVariant? _startupTheme;
    private ServiceProvider? _services;
    private PhotoFlowHost? _host;

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

            var window = new MainWindow { DataContext = new MainWindowViewModel() };
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                (window.DataContext as IDisposable)?.Dispose();
                _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _services.Dispose();
            };
            _ = StartHostAsync(window, _services.GetRequiredService<IAppPaths>(), logger);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Opens catalog, exiftool and the other services off the UI thread, then shows the real pages.</summary>
    private async Task StartHostAsync(MainWindow window, IAppPaths paths, Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            _host = await Task.Run(() => PhotoFlowHost.StartAsync(paths, CancellationToken.None));
            var viewModel = new MainWindowViewModel(_host);
            viewModel.RefreshStatus();
            window.DataContext = viewModel;
            await viewModel.CurrentPage.ActivateAsync();
        }
        catch (CatalogLockedException e)
        {
            logger.LogWarning(e, "Catalog in use by another window");
            ((MainWindowViewModel)window.DataContext!).StatusText = Strings.Get("Status.CatalogLocked");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or CatalogCorruptException)
        {
            logger.LogError(e, "Startup failed");
            ((MainWindowViewModel)window.DataContext!).StatusText = Strings.Format("Status.StartupFailed", e.Message);
        }
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
        return services.BuildServiceProvider();
    }
}
