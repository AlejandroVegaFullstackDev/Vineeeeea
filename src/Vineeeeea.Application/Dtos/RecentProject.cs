namespace Vineeeeea.Application.Dtos;

public sealed record RecentProject(string FilePath, string Name, long DurationUs, string? PreviewMediaPath, DateTimeOffset LastOpened);
