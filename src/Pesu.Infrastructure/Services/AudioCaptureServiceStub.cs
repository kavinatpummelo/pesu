using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Infrastructure.Services;

public sealed class AudioCaptureServiceStub : IAudioCaptureService
{
    public Task StartAsync(string? microphoneDeviceId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TranscriptSegment>> StopAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TranscriptSegment> transcript =
        [
            new TranscriptSegment(Guid.NewGuid().ToString("N"), "00:03", "You", "Let's continue with the current local-first roadmap."),
            new TranscriptSegment(Guid.NewGuid().ToString("N"), "00:18", "Meeting", "We should complete the Windows native replica shell this sprint.")
        ];

        return Task.FromResult(transcript);
    }
}
