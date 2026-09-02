using Pesu.Core.Models;

namespace Pesu.Core.Services;

public interface IAudioCaptureService
{
    Task StartAsync(string? microphoneDeviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TranscriptSegment>> StopAsync(CancellationToken cancellationToken = default);
}
