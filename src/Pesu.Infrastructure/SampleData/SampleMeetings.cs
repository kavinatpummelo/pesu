using Pesu.Core.Models;

namespace Pesu.Infrastructure.SampleData;

public static class SampleMeetings
{
    public static IReadOnlyList<Meeting> Create()
    {
        var now = DateTimeOffset.Now;

        return
        [
            new Meeting(
                0,
                "Daily product review",
                now.AddHours(-2),
                TimeSpan.FromMinutes(32),
                "The team aligned on shipping the WinUI shell and local recording pipeline as the first parity milestone.",
                [new Decision("01", "Ship the Present/Past/Future flow in the first Windows milestone.", "a1")],
                [new TranscriptSegment("a1", "00:04", "You", "Let's lock the first milestone around shell parity.")],
                null,
                null,
                false,
                "Product"
            ),
            new Meeting(
                0,
                "Windows architecture session",
                now.AddDays(-1).AddHours(-1),
                TimeSpan.FromMinutes(47),
                "We agreed to keep the app local-first and map each macOS service to a Windows-native counterpart.",
                [new Decision("01", "Use WinUI 3 + WASAPI + SQLite as core stack.", "b1")],
                [new TranscriptSegment("b1", "00:09", "Meeting", "WinUI and WASAPI should be our base implementation.")],
                null,
                null,
                false,
                "Engineering"
            ),
            new Meeting(
                0,
                "Upcoming sprint planning",
                now.AddDays(1).AddHours(3),
                TimeSpan.FromMinutes(60),
                "",
                Array.Empty<Decision>(),
                Array.Empty<TranscriptSegment>(),
                null,
                null,
                false,
                "Planning"
            )
        ];
    }
}
