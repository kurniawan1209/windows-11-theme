using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

    // Drag-and-drop state
    private Point _dragStartPoint;
    private DockItem? _draggedItem;
    private bool _isDraggingActive;

    // Auto-hide mechanics
    private readonly DispatcherTimer _autoHideDelayTimer;
    private readonly DispatcherTimer _edgePeekTimer;
    private bool _isDockHidden;
    private bool _isContextMenuOpen;

    public DockWindow(DockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        MouseEnter += OnDockMouseEnter;
        MouseLeave += OnDockMouseLeave;

        ContextMenuOpening += (_, _) => _isContextMenuOpen = true;
        ContextMenuClosing += (_, _) => _isContextMenuOpen = false;

        _autoHideDelayTimer = new DispatcherTimer();
        _autoHideDelayTimer.Tick += OnAutoHideDelayTimerTick;

        _edgePeekTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _edgePeekTimer.Tick += OnEdgePeekTimerTick;
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
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStartPoint = e.GetPosition(this);
                _draggedItem = item;
            }
        }
    }

    private void OnDockItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null || _isDraggingActive)
        {
            return;
        }

        Point currentPoint = e.GetPosition(this);
        Vector diff = _dragStartPoint - currentPoint;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            // Only allow dragging pinned items to reorder them
            if (!_draggedItem.IsPinned) return;

            _isDraggingActive = true;
            _draggedItem.IsBeingDragged = true;

            try
            {
                var data = new DataObject("CustomDesktop.DockItem", _draggedItem);
                DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
            }
            finally
            {
                _draggedItem.IsBeingDragged = false;
                _draggedItem = null;
                _isDraggingActive = false;

                // Reset all drop targets
                foreach (var i in _viewModel.Items)
                {
                    i.IsDropTarget = false;
                }
            }
        }
    }

    private void OnDockItemDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CustomDesktop.DockItem"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement elem && elem.DataContext is DockItem targetItem)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            foreach (var i in _viewModel.Items)
            {
                i.IsDropTarget = (i == targetItem && targetItem != _draggedItem);
            }
        }
    }

    private void OnDockItemDragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is DockItem targetItem)
        {
            targetItem.IsDropTarget = false;
        }
    }

    private void OnDockItemDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CustomDesktop.DockItem")) return;

        var sourceItem = e.Data.GetData("CustomDesktop.DockItem") as DockItem;
        if (sourceItem == null || !sourceItem.IsPinned) return;

        if (sender is FrameworkElement elem && elem.DataContext is DockItem targetItem)
        {
            targetItem.IsDropTarget = false;
            int oldIndex = _viewModel.Items.IndexOf(sourceItem);
            int newIndex = _viewModel.Items.IndexOf(targetItem);

            if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            {
                _viewModel.ReorderPinnedApps(oldIndex, newIndex);
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
        UpdateAutoHideState();
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
        else if (e.PropertyName == nameof(DockViewModel.AutoHide) ||
                 e.PropertyName == nameof(DockViewModel.AutoHideDelayMs))
        {
            Dispatcher.InvokeAsync(UpdateAutoHideState);
        }
    }

    private void UpdateAutoHideState()
    {
        if (_viewModel.AutoHide)
        {
            _edgePeekTimer.Start();
            if (!IsMouseOver)
            {
                StartAutoHideTimer();
            }
        }
        else
        {
            _edgePeekTimer.Stop();
            _autoHideDelayTimer.Stop();
            _isDockHidden = false;
            BeginAnimation(TopProperty, null);
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            Top = Math.Round(screenHeight - Height - _viewModel.MarginFromScreen);
        }
    }

    private void OnDockMouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideDelayTimer.Stop();
        if (_isDockHidden)
        {
            ShowDockSmooth();
        }
    }

    private void OnDockMouseLeave(object sender, MouseEventArgs e)
    {
        if (_viewModel.AutoHide)
        {
            StartAutoHideTimer();
        }
    }

    private void StartAutoHideTimer()
    {
        _autoHideDelayTimer.Stop();
        _autoHideDelayTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, _viewModel.AutoHideDelayMs));
        _autoHideDelayTimer.Start();
    }

    private void OnAutoHideDelayTimerTick(object? sender, EventArgs e)
    {
        _autoHideDelayTimer.Stop();

        if (!_viewModel.AutoHide) return;

        // Never hide if mouse is currently over the dock
        if (IsMouseOver) return;

        // Never hide if window picker popup, context menu, or active drag is open
        if (WindowPickerPopup.IsOpen || _isContextMenuOpen || _isDraggingActive)
        {
            StartAutoHideTimer();
            return;
        }

        HideDockSmooth();
    }

    private void OnEdgePeekTimerTick(object? sender, EventArgs e)
    {
        if (!_viewModel.AutoHide || !_isDockHidden) return;

        if (User32.GetCursorPos(out var pt))
        {
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double screenWidth = SystemParameters.PrimaryScreenWidth;

            // Trigger peek when cursor reaches bottom 4px within the dock's horizontal extent
            if (pt.Y >= screenHeight - 4)
            {
                double dockLeft = Left;
                double dockRight = dockLeft + Width;

                // Expand detection range slightly beyond dock width
                if (pt.X >= dockLeft - 40 && pt.X <= dockRight + 40)
                {
                    ShowDockSmooth();
                }
            }
        }
    }

    private void HideDockSmooth()
    {
        if (_isDockHidden) return;
        _isDockHidden = true;

        double screenHeight = SystemParameters.PrimaryScreenHeight;
        // Slide down leaving a 2px peek zone at the bottom
        double targetTop = screenHeight - 2;

        if (!_viewModel.EnableAnimations)
        {
            BeginAnimation(TopProperty, null);
            Top = targetTop;
            return;
        }

        var anim = new DoubleAnimation
        {
            From = Top,
            To = targetTop,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };

        anim.Completed += (_, _) =>
        {
            Top = targetTop;
            BeginAnimation(TopProperty, null);
        };

        BeginAnimation(TopProperty, anim);
    }

    private void ShowDockSmooth()
    {
        if (!_isDockHidden) return;
        _isDockHidden = false;

        double screenHeight = SystemParameters.PrimaryScreenHeight;
        double targetTop = Math.Round(screenHeight - Height - _viewModel.MarginFromScreen);

        if (!_viewModel.EnableAnimations)
        {
            BeginAnimation(TopProperty, null);
            Top = targetTop;
            return;
        }

        var anim = new DoubleAnimation
        {
            From = Top,
            To = targetTop,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        anim.Completed += (_, _) =>
        {
            Top = targetTop;
            BeginAnimation(TopProperty, null);
        };

        BeginAnimation(TopProperty, anim);
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
        double targetTop = _isDockHidden && _viewModel.AutoHide
            ? screenHeight - 2
            : Math.Round(screenHeight - targetHeight - _viewModel.MarginFromScreen);

        if (!_viewModel.EnableAnimations)
        {
            ApplyDimensionsInstant(targetWidth, targetHeight);
            return;
        }

        // Avoid re-animating if dimensions are virtually identical
        if (Math.Abs(Width - targetWidth) < 1.0 && Math.Abs(Left - targetLeft) < 1.0)
        {
            if (!_isDockHidden || !_viewModel.AutoHide)
            {
                Top = targetTop;
            }
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
