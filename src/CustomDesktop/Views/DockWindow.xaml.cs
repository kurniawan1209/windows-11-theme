using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using CustomDesktop.Core.Models;
using CustomDesktop.Core.Native;
using CustomDesktop.ViewModels;
using CustomDesktop.Views.Base;

namespace CustomDesktop.Views;

public partial class DockWindow : NonActivatingWindow
{
    private readonly DockViewModel _viewModel;
    private DockItem? _currentPickerItem;
    private DateTime _lastPickerClosedTime = DateTime.MinValue;

    public DockWindow(DockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnDockItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is DockItem item)
        {
            // Windows native behavior: Middle-click spawns a new application instance
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                item.LaunchNewInstance();
            }
        }
    }

    private void OnDockItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is DockItem item)
        {
            // Windows native behavior: Shift + Left-Click spawns a new application instance
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                item.LaunchNewInstance();
                return;
            }

            // Multi-window scenario: Open or toggle interactive selection picker
            if (item.HasMultipleWindows)
            {
                // Debounce toggle if this click occurred as part of closing the popup
                if ((DateTime.UtcNow - _lastPickerClosedTime).TotalMilliseconds < 150)
                {
                    return;
                }

                if (WindowPickerPopup.IsOpen && _currentPickerItem == item)
                {
                    WindowPickerPopup.IsOpen = false;
                    _currentPickerItem = null;
                    return;
                }

                // Restore all windows so they are open on screen as requested
                foreach (var h in item.WindowHandles)
                {
                    if (User32.IsIconic(h))
                    {
                        User32.ShowWindowAsync(h, User32.SW_RESTORE);
                    }
                }

                var items = item.GetWindowSelectionItems();
                if (items.Count > 0)
                {
                    _currentPickerItem = item;
                    PickerHeader.Text = $"{item.Name} ({items.Count} windows)";
                    PickerItemsList.ItemsSource = items;
                    WindowPickerPopup.PlacementTarget = elem;
                    WindowPickerPopup.IsOpen = true;
                }
                return;
            }

            // Single window or inactive app: Focus/minimize toggle or launch
            WindowPickerPopup.IsOpen = false;
            _currentPickerItem = null;
            item.ActivateOrLaunch();
        }
    }

    private void OnWindowPickerClosed(object? sender, EventArgs e)
    {
        _lastPickerClosedTime = DateTime.UtcNow;
        _currentPickerItem = null;
    }

    private void OnPickerCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is WindowSelectionItem item)
        {
            User32.ForceSetForegroundWindow(item.WindowHandle);
            WindowPickerPopup.IsOpen = false;
        }
    }

    private void OnPickerCloseButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is WindowSelectionItem item)
        {
            User32.PostMessage(item.WindowHandle, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            if (_currentPickerItem != null)
            {
                _currentPickerItem.WindowHandles.Remove(item.WindowHandle);
                var remaining = _currentPickerItem.GetWindowSelectionItems();
                if (remaining.Count <= 1)
                {
                    WindowPickerPopup.IsOpen = false;
                }
                else
                {
                    PickerHeader.Text = $"{_currentPickerItem.Name} ({remaining.Count} windows)";
                    PickerItemsList.ItemsSource = remaining;
                }
            }
            else
            {
                WindowPickerPopup.IsOpen = false;
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyDimensionsInstant(_viewModel.CalculatedWidth, _viewModel.CalculatedHeight);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockViewModel.CalculatedWidth) ||
            e.PropertyName == nameof(DockViewModel.CalculatedHeight) ||
            e.PropertyName == nameof(DockViewModel.IconSize) ||
            e.PropertyName == nameof(DockViewModel.ItemGap) ||
            e.PropertyName == nameof(DockViewModel.MarginFromScreen))
        {
            Dispatcher.InvokeAsync(() => AnimateDimensions(_viewModel.CalculatedWidth, _viewModel.CalculatedHeight));
        }
    }

    private void ApplyDimensionsInstant(double targetWidth, double targetHeight)
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        BeginAnimation(WidthProperty, null);
        BeginAnimation(LeftProperty, null);

        Width = targetWidth;
        Height = targetHeight;
        Left = Math.Round((screenWidth - targetWidth) / 2.0);
        Top = Math.Round(screenHeight - targetHeight - _viewModel.MarginFromScreen);
    }

    private void AnimateDimensions(double targetWidth, double targetHeight)
    {
        if (!IsLoaded)
        {
            ApplyDimensionsInstant(targetWidth, targetHeight);
            return;
        }

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        double targetLeft = Math.Round((screenWidth - targetWidth) / 2.0);
        double targetTop = Math.Round(screenHeight - targetHeight - _viewModel.MarginFromScreen);

        if (!_viewModel.EnableAnimations)
        {
            ApplyDimensionsInstant(targetWidth, targetHeight);
            return;
        }

        // Avoid re-animating if dimensions are virtually identical
        if (Math.Abs(Width - targetWidth) < 1.0 && Math.Abs(Left - targetLeft) < 1.0)
        {
            Top = targetTop;
            Height = targetHeight;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        double startWidth = ActualWidth > 0 ? ActualWidth : Width;
        double startLeft = Left;

        var widthAnimation = new DoubleAnimation
        {
            From = startWidth,
            To = targetWidth,
            Duration = duration,
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };

        var leftAnimation = new DoubleAnimation
        {
            From = startLeft,
            To = targetLeft,
            Duration = duration,
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };

        widthAnimation.Completed += (_, _) =>
        {
            Width = targetWidth;
            BeginAnimation(WidthProperty, null);
        };

        leftAnimation.Completed += (_, _) =>
        {
            Left = targetLeft;
            BeginAnimation(LeftProperty, null);
        };

        Top = targetTop;
        Height = targetHeight;

        BeginAnimation(WidthProperty, widthAnimation);
        BeginAnimation(LeftProperty, leftAnimation);
    }
}
