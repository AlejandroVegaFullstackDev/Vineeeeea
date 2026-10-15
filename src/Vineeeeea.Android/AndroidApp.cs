using System;
using System.IO;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Fonts.Inter;

namespace Vineeeeea.Android;

[Application]
public class AndroidApp(IntPtr handle, JniHandleOwnership transfer) : AvaloniaAndroidApplication<App.App>(handle, transfer)
{
    private const string FontsFolder = "/system/fonts";

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        ConfigureTools();
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }

    /// <summary>
    /// ffmpeg and ffprobe ship inside the APK as lib*.so and Android extracts them next to the app's native libraries.
    /// AppConfig already reads these variables, so the rest of the app runs them like on the desktop.
    /// </summary>
    private void ConfigureTools()
    {
        var nativeLibraries = ApplicationInfo?.NativeLibraryDir;
        if (nativeLibraries is null)
        {
            return;
        }

        SetIfMissing("VINEEEEEA_FFMPEG_PATH", Path.Combine(nativeLibraries, "libffmpeg.so"));
        SetIfMissing("VINEEEEEA_FFPROBE_PATH", Path.Combine(nativeLibraries, "libffprobe.so"));
        SetIfMissing("VINEEEEEA_FONT_PATH", Path.Combine(FontsFolder, "Roboto-Regular.ttf"));
        SetIfMissing("VINEEEEEA_BOLD_FONT_PATH", File.Exists(Path.Combine(FontsFolder, "Roboto-Bold.ttf"))
            ? Path.Combine(FontsFolder, "Roboto-Bold.ttf")
            : Path.Combine(FontsFolder, "Roboto-Regular.ttf"));
    }

    private static void SetIfMissing(string name, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
