namespace Vineeeeea.Infrastructure.Utils;

public static class FfmpegConventions
{
    /// <summary>Forces the local file protocol so a media path can never be read as a URL or a command-line option.</summary>
    public const string LocalFileProtocol = "file:";
}
