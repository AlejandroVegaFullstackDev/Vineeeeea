using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Vineeeeea.App.Services;
using Vineeeeea.App.ViewModels;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.App;

public static class CompositionRoot
{
    public static MainViewModel CreateMainViewModel(TopLevel topLevel, AppConfig config, EditorSession session, IToolRunner? toolRunner = null) =>
        CreateMainViewModel(topLevel.StorageProvider, topLevel.Launcher, config, session, toolRunner);

    public static MainViewModel CreateMainViewModel(IStorageProvider storageProvider, ILauncher launcher, AppConfig config, EditorSession session, IToolRunner? toolRunner = null)
    {
        var processRunner = toolRunner ?? new ProcessRunner();
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
            new FreezeController(session, new FfmpegFrameExtractor(processRunner, config)),
            new FileDialogService(storageProvider, launcher),
            new JsonUserPreferences(config));
    }
}
