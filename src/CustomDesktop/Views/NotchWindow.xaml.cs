using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CustomDesktop.ViewModels;
using CustomDesktop.Views.Base;

namespace CustomDesktop.Views;

public partial class NotchWindow : NonActivatingWindow
{
    private readonly NotchViewModel _viewModel;
    private readonly DispatcherTimer _clockTimer;

    public NotchWindow(NotchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => Reposition();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Reposition();
        UpdateClock();
        _clockTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Stop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchViewModel.WidthCompact) ||
            e.PropertyName == nameof(NotchViewModel.HeightCompact) ||
            e.PropertyName == nameof(NotchViewModel.MarginFromScreen))
        {
            Dispatcher.InvokeAsync(Reposition);
        }
    }

    private void Reposition()
    {
        PositionAtTopCenter(_viewModel.MarginFromScreen);
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("h:mm tt");
    }
}
