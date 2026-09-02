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
        if (transcript.Count == 0)
        {
            return Task.FromResult<(string Brief, IReadOnlyList<Decision> Decisions)>(
                ("No speech was captured for this recording.", Array.Empty<Decision>())
            );
        }

        var briefSource = string.Join(" ", transcript.Take(3).Select(t => t.Text.Trim()));
        var brief = briefSource.Length > 280
            ? briefSource[..280] + "..."
            : briefSource;

        var decisions = transcript
            .Where(t => ContainsActionCue(t.Text))
            .Take(5)
            .Select((t, index) => new Decision($"{index + 1:00}", NormalizeDecision(t.Text), t.Id))
            .ToList();

        if (decisions.Count == 0)
        {
            decisions = transcript
                .Take(3)
                .Select((t, index) => new Decision($"{index + 1:00}", NormalizeDecision(t.Text), t.Id))
                .ToList();
        }

        return Task.FromResult<(string Brief, IReadOnlyList<Decision> Decisions)>((brief, decisions));
    }

    private static bool ContainsActionCue(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("will ") ||
               lower.Contains("should ") ||
               lower.Contains("need to") ||
               lower.Contains("decide") ||
               lower.Contains("next") ||
               lower.Contains("action");
    }

    private static string NormalizeDecision(string text)
    {
        var cleaned = text.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "No clear decision text.";
        }

        cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        return cleaned.EndsWith('.') ? cleaned : cleaned + ".";
    }
}
