namespace Pesu.Core.Models;

public sealed record MeetingStatsSnapshot(
    int CompletedMeetings,
    TimeSpan TotalDuration,
    TimeSpan AverageDuration,
    int MeetingDays
)
{
    public static MeetingStatsSnapshot Empty { get; } = new(0, TimeSpan.Zero, TimeSpan.Zero, 0);
}
