using Vineeeeea.App.Services;
using Vineeeeea.App.ViewModels;
using Vineeeeea.App.Views;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.App;

public static class CompositionRoot
{
    public static MainViewModel CreateMainViewModel(MainWindow window, AppConfig config, EditorSession session)
    {
        var processRunner = new ProcessRunner();
        var renderEngine = new FfmpegRenderEngine(processRunner, new TextFileStore(config), config);
        return new MainViewModel(
            session,
            new ProjectFileController(session, new JsonProjectStore(), new JsonRecentProjects(config), config),
            new MediaImportController(session, new FfprobeMediaProbe(processRunner, config)),
            new TimelineController(session),
            new MediaLibraryController(session),
            new HistoryController(session),
            new ProjectSettingsController(session),
            new PlaybackController(session, renderEngine, new FfplayAudioPlayer(processRunner, config), config),
            new ExportController(session, renderEngine),
            new ClipPreviewController(new FfmpegMediaPreviewCache(processRunner, config)),
            new CaptionsController(session, new WhisperTranscriber(processRunner, config)),
            new FileDialogService(window.StorageProvider, window.Launcher),
            new JsonUserPreferences(config));
    }
}
