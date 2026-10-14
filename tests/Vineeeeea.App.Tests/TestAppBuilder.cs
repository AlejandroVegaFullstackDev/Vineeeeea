using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Vineeeeea.App.Tests.TestAppBuilder))]

namespace Vineeeeea.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
