using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Threading;
using NAudio.Wave;
using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Windows.Services;

public sealed class WindowsLiveAudioCaptureService : IAudioCaptureService
{
    private readonly List<TranscriptSegment> _segments = [];
    private readonly object _sync = new();
    private SpeechRecognitionEngine? _recognizer;
    private WaveInEvent? _waveIn;
    private QueueWaveStream? _audioStream;
    private Stopwatch _stopwatch = new();
    private SynchronizationContext? _uiContext;

    public event EventHandler<TranscriptSegment>? TranscriptSegmentCaptured;

    public IReadOnlyList<MicrophoneOption> GetAvailableMicrophones()
    {
        var options = new List<MicrophoneOption>
        {
            new("default", "System Default", "Uses current Windows default input device")
        };

        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            options.Add(new MicrophoneOption(
                i.ToString(CultureInfo.InvariantCulture),
                caps.ProductName,
                $"{caps.Channels} channel(s)"
            ));
        }

        return options;
    }

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
        _recognizer.LoadGrammar(new DictationGrammar());
        var configuredInputMessage = ConfigureAudioInput(microphoneDeviceId);
        _recognizer.SpeechRecognized += OnSpeechRecognized;
        _recognizer.RecognizeCompleted += OnRecognizeCompleted;
        _recognizer.RecognizeAsync(RecognizeMode.Multiple);

        if (!string.IsNullOrWhiteSpace(configuredInputMessage))
        {
            RaiseSystemMessage(configuredInputMessage);
        }

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

    private string? ConfigureAudioInput(string? microphoneDeviceId)
    {
        if (_recognizer is null)
        {
            return "Speech recognizer is not initialized.";
        }

        if (string.IsNullOrWhiteSpace(microphoneDeviceId) || microphoneDeviceId.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            _recognizer.SetInputToDefaultAudioDevice();
            return null;
        }

        if (!int.TryParse(microphoneDeviceId, out var parsedDeviceNumber))
        {
            _recognizer.SetInputToDefaultAudioDevice();
            return "Invalid microphone selection. Using system default microphone.";
        }

        if (parsedDeviceNumber < 0 || parsedDeviceNumber >= WaveIn.DeviceCount)
        {
            _recognizer.SetInputToDefaultAudioDevice();
            return "Selected microphone is unavailable. Using system default microphone.";
        }

        try
        {
            var waveFormat = new WaveFormat(16000, 16, 1);
            _audioStream = new QueueWaveStream(waveFormat.AverageBytesPerSecond * 2);
            _waveIn = new WaveInEvent
            {
                DeviceNumber = parsedDeviceNumber,
                WaveFormat = waveFormat,
                BufferMilliseconds = 200,
                NumberOfBuffers = 3
            };
            _waveIn.DataAvailable += OnWaveDataAvailable;
            _waveIn.StartRecording();

            _recognizer.SetInputToAudioStream(
                _audioStream,
                new SpeechAudioFormatInfo(
                    EncodingFormat.Pcm,
                    waveFormat.SampleRate,
                    waveFormat.BitsPerSample,
                    waveFormat.Channels,
                    waveFormat.AverageBytesPerSecond,
                    waveFormat.BlockAlign,
                    null));

            return null;
        }
        catch
        {
            try
            {
                if (_waveIn is not null)
                {
                    _waveIn.DataAvailable -= OnWaveDataAvailable;
                    _waveIn.StopRecording();
                    _waveIn.Dispose();
                }
            }
            catch
            {
            }

            _waveIn = null;
            _audioStream?.Dispose();
            _audioStream = null;

            _recognizer.SetInputToDefaultAudioDevice();
            return "Could not open selected microphone. Using system default microphone.";
        }
    }

    private async Task StopInternalAsync()
    {
        var recognizer = _recognizer;
        var waveIn = _waveIn;
        var audioStream = _audioStream;

        if (waveIn is not null)
        {
            try
            {
                waveIn.DataAvailable -= OnWaveDataAvailable;
                waveIn.StopRecording();
            }
            catch
            {
            }
            waveIn.Dispose();
            _waveIn = null;
        }

        audioStream?.Complete();

        if (recognizer is not null)
        {
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
                recognizer.RecognizeAsyncCancel();
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            recognizer.SpeechRecognized -= OnSpeechRecognized;
            recognizer.RecognizeCompleted -= OnRecognizeCompleted;
            recognizer.Dispose();
            _recognizer = null;
        }

        audioStream?.Dispose();
        _audioStream = null;
        _stopwatch.Stop();
    }

    private void OnWaveDataAvailable(object? sender, WaveInEventArgs e)
    {
        _audioStream?.Enqueue(e.Buffer, 0, e.BytesRecorded);
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

        RaiseSystemMessage($"Speech recognition stopped: {args.Error.Message}");
    }

    private void RaiseSystemMessage(string message)
    {
        var seconds = (int)Math.Max(0, _stopwatch.Elapsed.TotalSeconds);
        var timestamp = $"{seconds / 60:00}:{seconds % 60:00}";
        RaiseTranscriptSegment(new TranscriptSegment(Guid.NewGuid().ToString("N"), timestamp, "System", message));
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

    private sealed class QueueWaveStream : Stream
    {
        private readonly BlockingCollection<byte[]> _queue;
        private byte[]? _currentChunk;
        private int _currentOffset;

        public QueueWaveStream(int boundedCapacity)
        {
            _queue = new BlockingCollection<byte[]>(boundedCapacity: Math.Max(8, boundedCapacity / 3200));
        }

        public void Enqueue(byte[] buffer, int offset, int count)
        {
            if (_queue.IsAddingCompleted || count <= 0)
            {
                return;
            }

            var copy = new byte[count];
            Buffer.BlockCopy(buffer, offset, copy, 0, count);
            _queue.Add(copy);
        }

        public void Complete()
        {
            if (!_queue.IsAddingCompleted)
            {
                _queue.CompleteAdding();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (true)
            {
                if (_currentChunk is not null)
                {
                    var remaining = _currentChunk.Length - _currentOffset;
                    if (remaining > 0)
                    {
                        var toCopy = Math.Min(remaining, count);
                        Buffer.BlockCopy(_currentChunk, _currentOffset, buffer, offset, toCopy);
                        _currentOffset += toCopy;
                        if (_currentOffset >= _currentChunk.Length)
                        {
                            _currentChunk = null;
                            _currentOffset = 0;
                        }
                        return toCopy;
                    }
                    _currentChunk = null;
                    _currentOffset = 0;
                }

                if (!_queue.TryTake(out var next, Timeout.Infinite))
                {
                    return 0;
                }

                _currentChunk = next;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Complete();
                _queue.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
