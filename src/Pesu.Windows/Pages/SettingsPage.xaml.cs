using Microsoft.UI.Xaml.Controls;
using Pesu.Core.ViewModels;

namespace Pesu.Windows.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
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
}
