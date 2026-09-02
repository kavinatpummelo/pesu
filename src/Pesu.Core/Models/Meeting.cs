namespace Pesu.Core.Models;

public sealed record Meeting(
    long Id,
    string Title,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    string Summary,
    IReadOnlyList<Decision> Decisions,
    IReadOnlyList<TranscriptSegment> Transcript,
    string? SystemAudioPath,
    string? MicrophoneAudioPath,
    bool IsAllDay,
    string? CalendarName
)
{
    public static Meeting Empty { get; } = new(
        0,
        string.Empty,
        DateTimeOffset.MinValue,
        TimeSpan.Zero,
        string.Empty,
        Array.Empty<Decision>(),
        Array.Empty<TranscriptSegment>(),
        null,
        null,
        false,
        null
    );
}
