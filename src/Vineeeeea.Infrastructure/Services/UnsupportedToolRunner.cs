using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Services;

/// <summary>Placeholder for platforms whose in-process media engine is not wired yet: fails with a clear message instead of a missing-executable error.</summary>
public sealed class UnsupportedToolRunner(string platformName) : IToolRunner
{
    public Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
        throw Unsupported();

    public Task<int> RunStreamingAsync(string executable, IReadOnlyList<string> arguments, Func<Stream, CancellationToken, Task> consumeOutput, CancellationToken cancellationToken) =>
        throw Unsupported();

    public Task RunPipelineAsync(string producerExecutable, IReadOnlyList<string> producerArguments, string consumerExecutable, IReadOnlyList<string> consumerArguments, CancellationToken cancellationToken) =>
        throw Unsupported();

    private ExternalServiceException Unsupported() => new($"El motor de video todavía no está disponible en {platformName}.");
}
