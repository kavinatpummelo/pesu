using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Core.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IMeetingRepository _meetingRepository;

    private AppScreen _currentScreen = AppScreen.Present;
    private Meeting _selectedMeeting = Meeting.Empty;
    private bool _isRecording;
    private string _captureStatus = "Ready";
    private string _calendarStatus = "Not connected";
    private string _calendarDetail = "Connect your calendar source in Settings.";

    public MainViewModel(IMeetingRepository meetingRepository)
    {
        _meetingRepository = meetingRepository;
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
        RaisePropertyChanged(nameof(Meetings));
        RaisePropertyChanged(nameof(PresentMeetings));
        RaisePropertyChanged(nameof(PastMeetings));
        RaisePropertyChanged(nameof(FutureMeetings));
    }

    public void NavigateTo(AppScreen screen)
    {
        CurrentScreen = screen;
    }

    private void StartRecording()
    {
        IsRecording = true;
        CaptureStatus = "Recording system audio + microphone locally";
        CurrentScreen = AppScreen.Recording;
    }

    private void StopRecording()
    {
        IsRecording = false;
        CaptureStatus = "Transcript finalized locally";
        CurrentScreen = AppScreen.Summary;
    }
}
