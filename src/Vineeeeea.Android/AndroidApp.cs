using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Fonts.Inter;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.Android;

[Application]
public class AndroidApp(IntPtr handle, JniHandleOwnership transfer) : AvaloniaAndroidApplication<App.App>(handle, transfer)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Android cannot launch ffmpeg as a process; swap in the native engine here when it exists.
        App.App.MobileToolRunner = new UnsupportedToolRunner("Android");
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
