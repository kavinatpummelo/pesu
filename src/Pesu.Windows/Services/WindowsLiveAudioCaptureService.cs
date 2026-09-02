using System.Diagnostics;
using System.Globalization;
using System.Speech.Recognition;
using System.Threading;
using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Windows.Services;

public sealed class WindowsLiveAudioCaptureService : IAudioCaptureService
{
    private readonly List<TranscriptSegment> _segments = [];
    private readonly object _sync = new();
    private SpeechRecognitionEngine? _recognizer;
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

        var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
            .FirstOrDefault(info =>
                info.Culture.Equals(CultureInfo.CurrentUICulture)
                || info.Culture.TwoLetterISOLanguageName == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
            ?? SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault();

        if (recognizerInfo is null)
        {
            await StopInternalAsync();
            throw new InvalidOperationException("No Windows speech recognizer is installed. Install a Speech language pack in Windows Settings.");
        }

        _recognizer = new SpeechRecognitionEngine(recognizerInfo);
        _recognizer.SetInputToDefaultAudioDevice();
        _recognizer.LoadGrammar(new DictationGrammar());
        _recognizer.SpeechRecognized += OnSpeechRecognized;
        _recognizer.RecognizeCompleted += OnRecognizeCompleted;
        _recognizer.RecognizeAsync(RecognizeMode.Multiple);
        await Task.CompletedTask;
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
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<RecognizeCompletedEventArgs>? handler = null;
            handler = (_, _) =>
            {
                recognizer.RecognizeCompleted -= handler;
                tcs.TrySetResult(true);
            };

            recognizer.RecognizeCompleted += handler;
            recognizer.RecognizeAsyncStop();
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        recognizer.SpeechRecognized -= OnSpeechRecognized;
        recognizer.RecognizeCompleted -= OnRecognizeCompleted;
        recognizer.Dispose();
        _recognizer = null;
        _stopwatch.Stop();
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs args)
    {
        if (args.Result is null || args.Result.Confidence < 0.35f)
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

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs args)
    {
        if (args.Cancelled || args.Error is null)
        {
            return;
        }

        var message = $"Speech recognition stopped: {args.Error.Message}";

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
