using Microsoft.UI.Xaml.Controls;
using Pesu.Core.ViewModels;

namespace Pesu.Windows.Pages;

public sealed partial class PastPage : Page
{
    public PastPage()
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
