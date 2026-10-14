namespace Vineeeeea.IntegrationTests;

internal sealed class TemporaryDirectory : IDisposable
{
    private const int DeleteAttempts = 20;
    private const int DeleteRetryMilliseconds = 250;

    public string Path { get; } = Directory.CreateTempSubdirectory("vineeeeea tests ").FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Background ffmpeg work (thumbnails, previews) may still hold a file for a moment after a test ends.</summary>
    public void Dispose()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(Path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteAttempts)
            {
                Thread.Sleep(DeleteRetryMilliseconds);
            }
        }
    }
}
