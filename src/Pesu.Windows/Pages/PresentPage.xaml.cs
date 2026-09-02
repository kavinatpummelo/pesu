using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pesu.Core.ViewModels;

namespace Pesu.Windows.Pages;

public sealed partial class PresentPage : Page
{
    public PresentPage()
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

    private void NewRecording_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NewRecordingCommand.Execute(null);
        }
    }
}
