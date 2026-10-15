using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Services;
using Vineeeeea.Infrastructure.Utils;

namespace Vineeeeea.Infrastructure.Implements;

public sealed class FfprobeMediaProbe(IToolRunner processRunner, AppConfig config) : IMediaProbe
{
    public async Task<ProbedMedia> ProbeAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new NotFoundException("No se encontró el archivo de medio.");
        }

        string[] arguments =
        [
            "-v", "error",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            FfmpegConventions.LocalFileProtocol + filePath,
        ];

        var result = await processRunner.RunAsync(config.FfprobePath, arguments, config.ProbeTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new ExternalServiceException("ffprobe no reconoce el archivo como un medio válido.");
        }

        return FfprobeJsonParser.Parse(result.StandardOutput);
    }
}
