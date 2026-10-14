namespace Vineeeeea.Infrastructure.Dtos;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
