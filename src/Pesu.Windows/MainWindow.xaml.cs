using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pesu.Core.Models;
using Pesu.Core.ViewModels;
using Pesu.Windows.Pages;
using System.ComponentModel;

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
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShellNav.SelectedItem = ShellNav.MenuItems.OfType<NavigationViewItem>().First();
        Navigate(AppScreen.Present);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null || e.PropertyName != nameof(MainViewModel.CurrentScreen))
        {
            return;
        }

        Navigate(_viewModel.CurrentScreen);
        SelectNavItem(_viewModel.CurrentScreen);
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

    private void SelectNavItem(AppScreen screen)
    {
        var allItems = ShellNav.MenuItems
            .OfType<NavigationViewItem>()
            .Concat(ShellNav.FooterMenuItems.OfType<NavigationViewItem>());

        var match = allItems.FirstOrDefault(item =>
            item.Tag is string tag &&
            Enum.TryParse<AppScreen>(tag, out var itemScreen) &&
            itemScreen == screen);

        if (match is not null && !ReferenceEquals(ShellNav.SelectedItem, match))
        {
            ShellNav.SelectedItem = match;
        }
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
