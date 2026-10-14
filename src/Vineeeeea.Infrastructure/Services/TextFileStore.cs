using System.Security.Cryptography;
using System.Text;
using Vineeeeea.Application.Config;

namespace Vineeeeea.Infrastructure.Services;

/// <summary>
/// ffmpeg reads text from a file instead of the command line, so whatever a user types can never be mistaken
/// for filter syntax.
/// </summary>
public sealed class TextFileStore(AppConfig config)
{
    private const string TextFolderName = "text";
    private const int FileKeyLength = 32;

    public string PathFor(string content)
    {
        var directory = Path.Combine(config.CacheDirectory, TextFolderName);
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes(content);
        var path = Path.Combine(directory, string.Concat(Convert.ToHexString(SHA256.HashData(bytes))[..FileKeyLength], ".txt"));
        if (!File.Exists(path))
        {
            var partialPath = string.Concat(path, ".", Guid.NewGuid().ToString("N"), ".part");
            File.WriteAllBytes(partialPath, bytes);
            File.Move(partialPath, path, overwrite: true);
        }

        return path;
    }
}
