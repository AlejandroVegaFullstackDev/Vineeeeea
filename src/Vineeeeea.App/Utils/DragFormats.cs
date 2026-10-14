using Avalonia.Input;

namespace Vineeeeea.App.Utils;

public static class DragFormats
{
    public static readonly DataFormat<string> LibraryAsset = DataFormat.CreateInProcessFormat<string>("vineeeeea-asset");
}
