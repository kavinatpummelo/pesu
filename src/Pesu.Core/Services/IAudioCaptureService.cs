using Pesu.Core.Models;

namespace Pesu.Core.Services;

public interface IAudioCaptureService
{
    event EventHandler<TranscriptSegment>? TranscriptSegmentCaptured;

    string? SystemAudioPath { get; }
    string? MicrophoneAudioPath { get; }

    IReadOnlyList<MicrophoneOption> GetAvailableMicrophones();

    Task StartAsync(string? microphoneDeviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TranscriptSegment>> StopAsync(CancellationToken cancellationToken = default);
}
