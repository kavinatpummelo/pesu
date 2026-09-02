namespace Pesu.Core.Models;

public sealed record Decision(
    string Id,
    string Text,
    string EvidenceSegmentId
);
