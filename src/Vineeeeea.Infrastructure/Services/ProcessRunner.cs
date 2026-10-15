using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Services;

public sealed class ProcessRunner : IToolRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = Start(executable, arguments);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var standardOutput = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var standardError = process.StandardError.ReadToEndAsync(deadline.Token);

        try
        {
            await process.WaitForExitAsync(deadline.Token);
            return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
        }
        catch (OperationCanceledException)
        {
            StopIfRunning(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new ExternalServiceException($"{ToolName(executable)} no respondió en {timeout.TotalSeconds} s.");
        }
    }

    /// <summary>Hands the raw standard output stream to <paramref name="consumeOutput"/>; the process is killed if the consumer stops early.</summary>
    public async Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Func<Stream, CancellationToken, Task> consumeOutput,
        CancellationToken cancellationToken)
    {
        using var process = Start(executable, arguments);
        var drainStandardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await consumeOutput(process.StandardOutput.BaseStream, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await drainStandardError;
            return process.ExitCode;
        }
        finally
        {
            StopIfRunning(process);
        }
    }

    /// <summary>Pipes the producer's standard output into the consumer's standard input until the consumer exits.</summary>
    public async Task RunPipelineAsync(
        string producerExecutable,
        IReadOnlyList<string> producerArguments,
        string consumerExecutable,
        IReadOnlyList<string> consumerArguments,
        CancellationToken cancellationToken)
    {
        using var producer = Start(producerExecutable, producerArguments);
        using var consumer = Start(consumerExecutable, consumerArguments, redirectStandardInput: true);
        var drainProducerErrors = producer.StandardError.ReadToEndAsync(CancellationToken.None);
        var drainConsumerOutput = consumer.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var drainConsumerErrors = consumer.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await producer.StandardOutput.BaseStream.CopyToAsync(consumer.StandardInput.BaseStream, cancellationToken);
            consumer.StandardInput.Close();
            await consumer.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(drainProducerErrors, drainConsumerOutput, drainConsumerErrors);
        }
        catch (IOException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException($"{ToolName(consumerExecutable)} se cerró antes de tiempo.");
        }
        finally
        {
            StopIfRunning(producer);
            StopIfRunning(consumer);
        }
    }

    private static Process Start(string executable, IReadOnlyList<string> arguments, bool redirectStandardInput = false)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            return process;
        }
        catch (Win32Exception)
        {
            process.Dispose();
            throw new ExternalServiceException($"No se pudo ejecutar {ToolName(executable)}. Comprueba que esté instalado.");
        }
    }

    private static void StopIfRunning(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    private static string ToolName(string executable) => Path.GetFileNameWithoutExtension(executable);
}
