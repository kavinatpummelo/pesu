using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Core.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IMeetingRepository _meetingRepository;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly INotesService _notesService;

    private AppScreen _currentScreen = AppScreen.Present;
    private Meeting _selectedMeeting = Meeting.Empty;
    private bool _isRecording;
    private string _captureStatus = "Ready";
    private string _calendarStatus = "Not connected";
    private string _calendarDetail = "Connect your calendar source in Settings.";

    public MainViewModel(
        IMeetingRepository meetingRepository,
        IAudioCaptureService audioCaptureService,
        INotesService notesService)
    {
        _meetingRepository = meetingRepository;
        _audioCaptureService = audioCaptureService;
        _notesService = notesService;
        Meetings = new List<Meeting>();
        NewRecordingCommand = new RelayCommand(StartRecording);
        StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
    }

    public AppScreen CurrentScreen
    {
        get => _currentScreen;
        set => SetProperty(ref _currentScreen, value);
    }

    public IReadOnlyList<Meeting> Meetings { get; private set; }

    public Meeting SelectedMeeting
    {
        get => _selectedMeeting;
        set => SetProperty(ref _selectedMeeting, value);
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                StopRecordingCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string CaptureStatus
    {
        get => _captureStatus;
        private set => SetProperty(ref _captureStatus, value);
    }

    public string CalendarStatus
    {
        get => _calendarStatus;
        set => SetProperty(ref _calendarStatus, value);
    }

    public string CalendarDetail
    {
        get => _calendarDetail;
        set => SetProperty(ref _calendarDetail, value);
    }

    public RelayCommand NewRecordingCommand { get; }
    public RelayCommand StopRecordingCommand { get; }

    public IReadOnlyList<Meeting> PresentMeetings => Meetings
        .Where(m => m.StartedAt.Date == DateTimeOffset.Now.Date)
        .OrderBy(m => m.StartedAt)
        .ToList();

    public IReadOnlyList<Meeting> PastMeetings => Meetings
        .Where(m => m.StartedAt.Date < DateTimeOffset.Now.Date)
        .OrderByDescending(m => m.StartedAt)
        .ToList();

    public IReadOnlyList<Meeting> FutureMeetings => Meetings
        .Where(m => m.StartedAt.Date > DateTimeOffset.Now.Date)
        .OrderBy(m => m.StartedAt)
        .ToList();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Meetings = await _meetingRepository.ListAsync(cancellationToken);
        SelectedMeeting = Meetings.FirstOrDefault() ?? Meeting.Empty;
        RefreshDerivedCollections();
    }

    public void NavigateTo(AppScreen screen)
    {
        CurrentScreen = screen;
    }

    private void StartRecording()
    {
        _ = StartRecordingAsync();
    }

    private void StopRecording()
    {
        _ = StopRecordingAsync();
    }

    private async Task StartRecordingAsync()
    {
        if (IsRecording)
        {
            return;
        }

        try
        {
            CaptureStatus = "Preparing local capture...";
            await _audioCaptureService.StartAsync(microphoneDeviceId: null);
            IsRecording = true;
            CaptureStatus = "Recording system audio + microphone locally";
            CurrentScreen = AppScreen.Recording;
        }
        catch (Exception ex)
        {
            IsRecording = false;
            CaptureStatus = $"Recording failed: {ex.Message}";
            CurrentScreen = AppScreen.Present;
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        try
        {
            CaptureStatus = "Finalizing transcript locally...";
            var transcript = await _audioCaptureService.StopAsync();
            var notes = await _notesService.BuildNotesAsync(transcript);
            var now = DateTimeOffset.Now;
            var duration = TimeSpan.FromMinutes(Math.Max(1, transcript.Count * 2));
            var meeting = new Meeting(
                0,
                $"Recording {now:yyyy-MM-dd HH:mm}",
                now,
                duration,
                notes.Brief,
                notes.Decisions,
                transcript,
                null,
                null,
                false,
                "Local Recording"
            );

            var saved = await _meetingRepository.SaveAsync(meeting);
            var updated = Meetings.ToList();
            updated.Insert(0, saved);
            Meetings = updated;
            SelectedMeeting = saved;

            IsRecording = false;
            CaptureStatus = "Transcript finalized locally";
            CurrentScreen = AppScreen.Summary;
            RefreshDerivedCollections();
        }
        catch (Exception ex)
        {
            IsRecording = false;
            CaptureStatus = $"Stop failed: {ex.Message}";
            CurrentScreen = AppScreen.Present;
        }
    }

    private void RefreshDerivedCollections()
    {
        RaisePropertyChanged(nameof(Meetings));
        RaisePropertyChanged(nameof(PresentMeetings));
        RaisePropertyChanged(nameof(PastMeetings));
        RaisePropertyChanged(nameof(FutureMeetings));
    }
}
