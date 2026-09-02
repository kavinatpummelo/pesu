using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Infrastructure.Services;

public sealed class NoOpAudioCaptureService : IAudioCaptureService
{
    public event EventHandler<TranscriptSegment>? TranscriptSegmentCaptured;

    public IReadOnlyList<MicrophoneOption> GetAvailableMicrophones()
    {
        return
        [
            new MicrophoneOption("default", "System Default", "Uses the current Windows default input")
        ];
    }

    public Task StartAsync(string? microphoneDeviceId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TranscriptSegment>> StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TranscriptSegment>>(Array.Empty<TranscriptSegment>());
    }
}
