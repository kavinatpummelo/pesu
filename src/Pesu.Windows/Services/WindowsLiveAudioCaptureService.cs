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
    private WasapiCapture? _defaultMicrophoneCapture;
    private QueueWaveStream? _audioStream;
    private WasapiLoopbackCapture? _systemAudioCapture;
    private WaveFileWriter? _microphoneWriter;
    private WaveFileWriter? _systemAudioWriter;
    private Stopwatch _stopwatch = new();
    private SynchronizationContext? _uiContext;

    public event EventHandler<TranscriptSegment>? TranscriptSegmentCaptured;

    public string? SystemAudioPath { get; private set; }
    public string? MicrophoneAudioPath { get; private set; }

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

        SystemAudioPath = null;
        MicrophoneAudioPath = null;

        _uiContext = SynchronizationContext.Current;
        _stopwatch = Stopwatch.StartNew();

        var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
            .FirstOrDefault(info =>
                info.Culture.Equals(CultureInfo.CurrentUICulture)
                || info.Culture.TwoLetterISOLanguageName == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
            ?? SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault();

        try
        {
            string? recognitionMessage = null;
            if (recognizerInfo is null)
            {
                recognitionMessage = "No Windows speech recognizer is installed. Audio is recording, but live transcription is unavailable.";
            }
            else
            {
                try
                {
                    _recognizer = new SpeechRecognitionEngine(recognizerInfo);
                    _recognizer.LoadGrammar(new DictationGrammar());
                }
                catch (Exception ex)
                {
                    _recognizer?.Dispose();
                    _recognizer = null;
                    recognitionMessage = $"Windows speech recognition is unavailable ({ex.Message}). Audio is recording without live transcription.";
                }
            }

            CreateAudioFiles();
            var configuredInputMessage = ConfigureAudioInput(microphoneDeviceId);
            if (_recognizer is not null)
            {
                try
                {
                    _recognizer.SpeechRecognized += OnSpeechRecognized;
                    _recognizer.RecognizeCompleted += OnRecognizeCompleted;
                    _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch (Exception ex)
                {
                    _recognizer.SpeechRecognized -= OnSpeechRecognized;
                    _recognizer.RecognizeCompleted -= OnRecognizeCompleted;
                    _recognizer.Dispose();
                    _recognizer = null;
                    recognitionMessage = $"Windows speech recognition could not start ({ex.Message}). Audio is recording without live transcription.";
                }
            }
            StartSystemAudioCapture();

            if (!string.IsNullOrWhiteSpace(recognitionMessage))
            {
                RaiseSystemMessage(recognitionMessage);
            }
            if (!string.IsNullOrWhiteSpace(configuredInputMessage))
            {
                RaiseSystemMessage(configuredInputMessage);
            }
        }
        catch
        {
            await StopInternalAsync();
            throw;
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
        var useDefaultDevice = string.IsNullOrWhiteSpace(microphoneDeviceId) ||
            microphoneDeviceId.Equals("default", StringComparison.OrdinalIgnoreCase);
        var deviceNumber = -1;
        if (!useDefaultDevice && !int.TryParse(microphoneDeviceId, out deviceNumber))
        {
            throw new InvalidOperationException("The selected microphone is invalid.");
        }

        if (!useDefaultDevice && (deviceNumber < 0 || deviceNumber >= WaveIn.DeviceCount))
        {
            throw new InvalidOperationException("The selected microphone is unavailable.");
        }

        try
        {
            var waveFormat = new WaveFormat(16000, 16, 1);
            _audioStream = new QueueWaveStream(waveFormat.AverageBytesPerSecond * 2);
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = waveFormat,
                BufferMilliseconds = 200,
                NumberOfBuffers = 3
            };
            if (MicrophoneAudioPath is not null)
            {
                _microphoneWriter = new WaveFileWriter(MicrophoneAudioPath, waveFormat);
            }
            _waveIn.DataAvailable += OnWaveDataAvailable;
            _waveIn.StartRecording();

            _recognizer?.SetInputToAudioStream(
                _audioStream,
                new SpeechAudioFormatInfo(
                    EncodingFormat.Pcm,
                    waveFormat.SampleRate,
                    waveFormat.BitsPerSample,
                    waveFormat.Channels,
                    waveFormat.AverageBytesPerSecond,
                    waveFormat.BlockAlign,
                    null));

            return _recognizer is null
                ? "Audio is recording, but live transcription is unavailable."
                : null;
        }
        catch (Exception ex)
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
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;
            _audioStream?.Dispose();
            _audioStream = null;

            if (useDefaultDevice)
            {
                return StartDefaultMicrophoneCapture();
            }

            throw new InvalidOperationException("Could not open the selected microphone. Check Windows microphone permissions and that no other app is using it.", ex);
        }
    }

    private string StartDefaultMicrophoneCapture()
    {
        try
        {
            _defaultMicrophoneCapture = new WasapiCapture();
            if (MicrophoneAudioPath is not null)
            {
                _microphoneWriter = new WaveFileWriter(MicrophoneAudioPath, _defaultMicrophoneCapture.WaveFormat);
            }
            _defaultMicrophoneCapture.DataAvailable += OnDefaultMicrophoneDataAvailable;
            _defaultMicrophoneCapture.StartRecording();
            return _recognizer is null
                ? "Audio is recording, but live transcription is unavailable."
                : SetDefaultRecognizerInput();
        }
        catch (Exception wasapiException)
        {
            _defaultMicrophoneCapture?.Dispose();
            _defaultMicrophoneCapture = null;
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;
            MicrophoneAudioPath = null;
            return $"Raw microphone capture is unavailable ({wasapiException.Message}).";
        }
    }

    private string SetDefaultRecognizerInput()
    {
        try
        {
            _recognizer?.SetInputToDefaultAudioDevice();
            return "Recording the Windows default microphone through WASAPI.";
        }
        catch (Exception ex)
        {
            _recognizer?.Dispose();
            _recognizer = null;
            return $"Audio is recording, but live transcription is unavailable ({ex.Message}).";
        }
    }

    private void CreateAudioFiles()
    {
        var recordingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pesu",
            "Recordings");
        Directory.CreateDirectory(recordingsDirectory);

        var recordingId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        MicrophoneAudioPath = Path.Combine(recordingsDirectory, $"{recordingId}-microphone.wav");
        SystemAudioPath = Path.Combine(recordingsDirectory, $"{recordingId}-system.wav");
    }

    private void StartSystemAudioCapture()
    {
        if (SystemAudioPath is null)
        {
            return;
        }

        try
        {
            _systemAudioCapture = new WasapiLoopbackCapture();
            _systemAudioWriter = new WaveFileWriter(SystemAudioPath, _systemAudioCapture.WaveFormat);
            _systemAudioCapture.DataAvailable += OnSystemAudioDataAvailable;
            _systemAudioCapture.StartRecording();
        }
        catch (Exception ex)
        {
            _systemAudioWriter?.Dispose();
            _systemAudioWriter = null;
            _systemAudioCapture?.Dispose();
            _systemAudioCapture = null;
            SystemAudioPath = null;
            RaiseSystemMessage($"System audio could not be captured: {ex.Message}");
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

        var defaultMicrophoneCapture = _defaultMicrophoneCapture;
        if (defaultMicrophoneCapture is not null)
        {
            try
            {
                defaultMicrophoneCapture.DataAvailable -= OnDefaultMicrophoneDataAvailable;
                defaultMicrophoneCapture.StopRecording();
            }
            catch
            {
            }
            defaultMicrophoneCapture.Dispose();
            _defaultMicrophoneCapture = null;
        }

        var systemAudioCapture = _systemAudioCapture;
        if (systemAudioCapture is not null)
        {
            try
            {
                systemAudioCapture.DataAvailable -= OnSystemAudioDataAvailable;
                systemAudioCapture.StopRecording();
            }
            catch
            {
            }
            systemAudioCapture.Dispose();
            _systemAudioCapture = null;
        }

        _microphoneWriter?.Dispose();
        _microphoneWriter = null;
        _systemAudioWriter?.Dispose();
        _systemAudioWriter = null;

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
        _microphoneWriter?.Write(e.Buffer, 0, e.BytesRecorded);
        _audioStream?.Enqueue(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnSystemAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        _systemAudioWriter?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnDefaultMicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        _microphoneWriter?.Write(e.Buffer, 0, e.BytesRecorded);
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
