namespace Pesu.Core.Models;

public sealed record TranscriptSegment(
    string Id,
    string Timestamp,
    string Speaker,
    string Text
);
