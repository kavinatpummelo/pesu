using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pesu.Core.Models;
using Pesu.Core.ViewModels;
using Pesu.Windows.Pages;

namespace Pesu.Windows;

public sealed partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        ShellNav.SelectedItem = ShellNav.MenuItems.OfType<NavigationViewItem>().First();
        Navigate(AppScreen.Present);
    }

    private void ShellNav_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string selectedTag)
        {
            return;
        }

        if (!Enum.TryParse<AppScreen>(selectedTag, out var targetScreen))
        {
            return;
        }

        _viewModel.NavigateTo(targetScreen);
        Navigate(targetScreen);
    }

    private void Navigate(AppScreen screen)
    {
        var targetPage = screen switch
        {
            AppScreen.Present => typeof(PresentPage),
            AppScreen.Past => typeof(PastPage),
            AppScreen.Future => typeof(FuturePage),
            AppScreen.Stats => typeof(StatsPage),
            AppScreen.Settings => typeof(SettingsPage),
            AppScreen.Recording => typeof(RecordingPage),
            AppScreen.Summary => typeof(SummaryPage),
            _ => typeof(PresentPage)
        };

        ContentFrame.Navigate(targetPage, _viewModel);
    }
}
