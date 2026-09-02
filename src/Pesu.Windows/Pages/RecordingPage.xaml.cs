using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pesu.Core.ViewModels;

namespace Pesu.Windows.Pages;

public sealed partial class RecordingPage : Page
{
    public RecordingPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is MainViewModel vm)
        {
            DataContext = vm;
        }
    }

    private void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StopRecordingCommand.Execute(null);
        }
    }
}
