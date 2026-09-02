using System.Diagnostics;
using System.Threading;
using Pesu.Core.Models;
using Pesu.Core.Services;
using Windows.Media.SpeechRecognition;

namespace Pesu.Windows.Services;

public sealed class WindowsLiveAudioCaptureService : IAudioCaptureService
{
    private readonly List<TranscriptSegment> _segments = [];
    private readonly object _sync = new();
    private SpeechRecognizer? _recognizer;
    private Stopwatch _stopwatch = new();
    private SynchronizationContext? _uiContext;

    public event EventHandler<TranscriptSegment>? TranscriptSegmentCaptured;

    public async Task StartAsync(string? microphoneDeviceId, CancellationToken cancellationToken = default)
    {
        await StopInternalAsync();
        lock (_sync)
        {
            _segments.Clear();
        }

        _uiContext = SynchronizationContext.Current;
        _stopwatch = Stopwatch.StartNew();

        _recognizer = new SpeechRecognizer();
        _recognizer.Constraints.Clear();
        _recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation"));

        var compiled = await _recognizer.CompileConstraintsAsync().AsTask(cancellationToken);
        if (compiled.Status != SpeechRecognitionResultStatus.Success)
        {
            await StopInternalAsync();
            throw new InvalidOperationException($"Speech recognition setup failed: {compiled.Status}");
        }

        _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
        _recognizer.ContinuousRecognitionSession.Completed += OnCompleted;
        await _recognizer.ContinuousRecognitionSession.StartAsync().AsTask(cancellationToken);
    }

    public async Task<IReadOnlyList<TranscriptSegment>> StopAsync(CancellationToken cancellationToken = default)
    {
        await StopInternalAsync();
        lock (_sync)
        {
            return _segments.ToList();
        }
    }

    private async Task StopInternalAsync()
    {
        var recognizer = _recognizer;
        if (recognizer is null)
        {
            return;
        }

        try
        {
            await recognizer.ContinuousRecognitionSession.StopAsync();
        }
        catch
        {
        }

        recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
        recognizer.ContinuousRecognitionSession.Completed -= OnCompleted;
        recognizer.Dispose();
        _recognizer = null;
        _stopwatch.Stop();
    }

    private void OnResultGenerated(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Status != SpeechRecognitionResultStatus.Success)
        {
            return;
        }

        var text = args.Result.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var seconds = (int)Math.Max(0, _stopwatch.Elapsed.TotalSeconds);
        var timestamp = $"{seconds / 60:00}:{seconds % 60:00}";
        var segment = new TranscriptSegment(Guid.NewGuid().ToString("N"), timestamp, "You", text);

        lock (_sync)
        {
            _segments.Add(segment);
        }

        RaiseTranscriptSegment(segment);
    }

    private void OnCompleted(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionCompletedEventArgs args)
    {
        if (args.Status == SpeechRecognitionResultStatus.Success || args.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
        {
            return;
        }

        var statusText = args.Status.ToString();
        var message = statusText.Contains("Privacy", StringComparison.OrdinalIgnoreCase)
            ? "Speech privacy settings are disabled."
            : args.Status switch
            {
                SpeechRecognitionResultStatus.AudioQualityFailure => "Microphone audio quality is too low.",
                _ => $"Speech recognition stopped: {args.Status}"
            };

        var seconds = (int)Math.Max(0, _stopwatch.Elapsed.TotalSeconds);
        var timestamp = $"{seconds / 60:00}:{seconds % 60:00}";
        var segment = new TranscriptSegment(Guid.NewGuid().ToString("N"), timestamp, "System", message);
        RaiseTranscriptSegment(segment);
    }

    private void RaiseTranscriptSegment(TranscriptSegment segment)
    {
        var handler = TranscriptSegmentCaptured;
        if (handler is null)
        {
            return;
        }

        if (_uiContext is null)
        {
            handler(this, segment);
            return;
        }

        _uiContext.Post(_ => handler(this, segment), null);
    }
}
