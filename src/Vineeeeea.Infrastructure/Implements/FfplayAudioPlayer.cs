using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.Infrastructure.Implements;

public sealed class FfplayAudioPlayer(IToolRunner processRunner, AppConfig config) : IAudioPlayer
{
    public Task PlayAsync(RenderPlan plan, CancellationToken cancellationToken)
    {
        var graph = FfmpegGraphBuilder.Build(plan, includeVideo: false, includeAudio: true);
        List<string> mixArguments = ["-v", "error", "-nostdin", .. graph.InputArguments];
        mixArguments.AddRange(["-filter_complex", graph.FilterComplex, "-map", FfmpegGraphBuilder.AudioOutput, "-f", "wav", "pipe:1"]);
        string[] playerArguments = ["-v", "error", "-nodisp", "-autoexit", "-i", "pipe:0"];

        return processRunner.RunPipelineAsync(config.FfmpegPath, mixArguments, config.FfplayPath, playerArguments, cancellationToken);
    }
}
