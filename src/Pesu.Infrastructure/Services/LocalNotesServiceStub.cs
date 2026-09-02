using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Infrastructure.Services;

public sealed class LocalNotesServiceStub : INotesService
{
    public Task<(string Brief, IReadOnlyList<Decision> Decisions)> BuildNotesAsync(
        IReadOnlyList<TranscriptSegment> transcript,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<Decision> decisions =
        [
            new Decision("01", "Proceed with the WinUI 3 shell and sidebar parity first.", transcript.FirstOrDefault()?.Id ?? string.Empty),
            new Decision("02", "Keep all transcript and audio data local by default.", transcript.LastOrDefault()?.Id ?? string.Empty)
        ];

        const string brief = "This meeting confirmed that the first Windows milestone is a native WinUI shell with local-first recording and summary workflow parity.";
        return Task.FromResult((brief, decisions));
    }
}
