using Pesu.Core.Models;

namespace Pesu.Core.Services;

public interface INotesService
{
    Task<(string Brief, IReadOnlyList<Decision> Decisions)> BuildNotesAsync(
        IReadOnlyList<TranscriptSegment> transcript,
        CancellationToken cancellationToken = default
    );
}
