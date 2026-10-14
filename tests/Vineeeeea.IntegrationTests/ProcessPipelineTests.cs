using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.IntegrationTests;

public class ProcessPipelineTests
{
    [Fact]
    public async Task Pipes_a_producer_into_a_consumer_until_it_finishes()
    {
        using var directory = new TemporaryDirectory();
        var outputPath = directory.Combine("tono.wav");
        string[] producer = ["-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-f", "wav", "pipe:1"];
        string[] consumer = ["-v", "error", "-y", "-i", "pipe:0", outputPath];

        await new ProcessRunner().RunPipelineAsync("ffmpeg", producer, "ffmpeg", consumer, CancellationToken.None);

        Assert.True(new FileInfo(outputPath).Length > 80_000);
    }

    [Fact]
    public async Task Cancelling_the_pipeline_stops_both_processes()
    {
        string[] endlessProducer = ["-v", "error", "-re", "-f", "lavfi", "-i", "sine=frequency=440", "-f", "wav", "pipe:1"];
        string[] consumer = ["-v", "error", "-i", "pipe:0", "-f", "null", "-"];
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ProcessRunner().RunPipelineAsync("ffmpeg", endlessProducer, "ffmpeg", consumer, stop.Token));
    }
}
