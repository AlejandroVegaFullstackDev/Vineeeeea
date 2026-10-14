using Vineeeeea.Application.Dtos;

namespace Vineeeeea.Application.Services;

public interface ISpeechTranscriber
{
    Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        string mediaPath,
        long sourceStartUs,
        long sourceDurationUs,
        CancellationToken cancellationToken);
}
