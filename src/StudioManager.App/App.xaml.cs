using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudioManager.App.Windows;
using StudioManager.Core.Camera;
using StudioManager.Core.Configuration;
using StudioManager.Core.Media;
using StudioManager.Core.Services;

namespace StudioManager.App;

public partial class App : System.Windows.Application
{
    private IServiceProvider _services = null!;

    public static IServiceProvider Services => ((App)Current)._services;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<StudioOptions>(configuration.GetSection(StudioOptions.SectionName));
        services.AddSingleton(_ =>
        {
            var opts = new StudioOptions();
            configuration.GetSection(StudioOptions.SectionName).Bind(opts);
            if (string.IsNullOrWhiteSpace(opts.Audio.SavePath))
                opts.Audio.SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Recordings");
            return opts;
        });

        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(8) });
        services.AddSingleton<IPtzService, OnvifPtzService>();
        services.AddSingleton<ICameraRecordingService, FfmpegCameraRecordingService>();
        services.AddSingleton<IMediaExportService, MediaExportService>();
        services.AddSingleton<PresetStore>();
        services.AddSingleton<AudioCatalogStore>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        var main = _services.GetRequiredService<MainWindow>();
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        foreach (Window window in Windows)
        {
            if (window is SoundRecorderWindow recorder)
                recorder.ForceStopWithoutShutdown();
        }

        if (_services is IDisposable disposable)
            disposable.Dispose();

        base.OnExit(e);
    }
}
