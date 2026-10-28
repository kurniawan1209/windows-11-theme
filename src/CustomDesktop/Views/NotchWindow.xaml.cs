using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CustomDesktop.Core.Native;
using CustomDesktop.ViewModels;
using CustomDesktop.Views.Base;

namespace CustomDesktop.Views;

public partial class NotchWindow : NonActivatingWindow
{
    private readonly NotchViewModel _viewModel;
    private readonly DispatcherTimer _hoverDebounceTimer;
    private readonly User32.LowLevelMouseProc _mouseProc;
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private bool _isAnimating;

    public NotchWindow(NotchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _mouseProc = LowLevelMouseHookCallback;

        _hoverDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _hoverDebounceTimer.Tick += OnHoverDebounceTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        MouseEnter += OnWindowMouseEnter;
        MouseMove += OnWindowMouseMove;
        MouseLeave += OnWindowMouseLeave;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.RequestExpand += OnRequestExpand;
        _viewModel.RequestCollapse += OnRequestCollapse;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Reposition();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hoverDebounceTimer.Stop();
        UninstallMouseHook();
        _viewModel.RequestExpand -= OnRequestExpand;
        _viewModel.RequestCollapse -= OnRequestCollapse;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchViewModel.WidthCompact) ||
            e.PropertyName == nameof(NotchViewModel.HeightCompact) ||
            e.PropertyName == nameof(NotchViewModel.WidthExpanded) ||
            e.PropertyName == nameof(NotchViewModel.HeightExpanded) ||
            e.PropertyName == nameof(NotchViewModel.MarginFromScreen))
        {
            Dispatcher.InvokeAsync(Reposition);
        }
    }

    private void Reposition()
    {
        if (_isAnimating) return;

        double targetWidth = _viewModel.IsExpanded ? _viewModel.WidthExpanded : _viewModel.WidthCompact;
        double targetHeight = _viewModel.IsExpanded ? _viewModel.HeightExpanded : _viewModel.HeightCompact;

        Width = targetWidth;
        Height = targetHeight;
        PositionAtTopCenter(targetWidth, _viewModel.MarginFromScreen);
    }

    private void OnRootGridMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsExpanded)
        {
            _viewModel.Expand();
        }
    }

    private void OnCollapseButtonClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.Collapse();
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        if (_viewModel.ExpandOnHover)
        {
            _hoverDebounceTimer.Stop();
            if (!_viewModel.IsExpanded && !_isAnimating)
            {
                _viewModel.Expand();
            }
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel.ExpandOnHover && !_viewModel.IsExpanded && !_isAnimating)
        {
            _hoverDebounceTimer.Stop();
            _viewModel.Expand();
        }
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (_viewModel.ExpandOnHover && _viewModel.IsExpanded)
        {
            _hoverDebounceTimer.Stop();
            _hoverDebounceTimer.Start();
        }
    }

    private void OnHoverDebounceTimerTick(object? sender, EventArgs e)
    {
        _hoverDebounceTimer.Stop();

        if (_viewModel.ExpandOnHover && _viewModel.IsExpanded)
        {
            if (User32.GetCursorPos(out var pt))
            {
                try
                {
                    var clientPt = PointFromScreen(new Point(pt.X, pt.Y));
                    bool isInside = clientPt.X >= 0 && clientPt.X <= ActualWidth &&
                                    clientPt.Y >= 0 && clientPt.Y <= ActualHeight;
                    if (!isInside)
                    {
                        _viewModel.Collapse();
                    }
                }
                catch
                {
                    _viewModel.Collapse();
                }
            }
            else if (!IsMouseOver)
            {
                _viewModel.Collapse();
            }
        }
    }

    private void OnRequestExpand()
    {
        ExpandSmooth();
    }

    private void OnRequestCollapse()
    {
        CollapseSmooth();
    }

    private void ExpandSmooth()
    {
        InstallMouseHook();

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double targetWidth = _viewModel.WidthExpanded;
        double targetHeight = _viewModel.HeightExpanded;
        double targetLeft = Math.Round((screenWidth - targetWidth) / 2.0);
        double targetTop = _viewModel.MarginFromScreen;

        if (!_viewModel.EnableAnimations)
        {
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(HeightProperty, null);

            Width = targetWidth;
            Left = targetLeft;
            Height = targetHeight;
            Top = targetTop;

            CompactContentGrid.Visibility = Visibility.Collapsed;
            CompactContentGrid.Opacity = 0;
            ExpandedContentGrid.Visibility = Visibility.Visible;
            ExpandedContentGrid.Opacity = 1;
            return;
        }

        _isAnimating = true;
        var duration = TimeSpan.FromMilliseconds(260);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // 1. Simultaneous Width, Left, and Height Animations
        var widthAnim = new DoubleAnimation(Width, targetWidth, duration) { EasingFunction = ease };
        var leftAnim = new DoubleAnimation(Left, targetLeft, duration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(Height, targetHeight, duration) { EasingFunction = ease };

        heightAnim.Completed += (_, _) =>
        {
            _isAnimating = false;
            Width = targetWidth;
            Left = targetLeft;
            Height = targetHeight;
            Top = targetTop;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(HeightProperty, null);
        };

        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(HeightProperty, heightAnim);

        // 2. Crossfade Content Storyboard
        var fadeOutCompact = new DoubleAnimation(CompactContentGrid.Opacity, 0, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = ease
        };
        fadeOutCompact.Completed += (_, _) => CompactContentGrid.Visibility = Visibility.Collapsed;
        CompactContentGrid.BeginAnimation(OpacityProperty, fadeOutCompact);

        ExpandedContentGrid.Visibility = Visibility.Visible;
        var fadeInExpanded = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            BeginTime = TimeSpan.FromMilliseconds(60),
            EasingFunction = ease
        };
        ExpandedContentGrid.BeginAnimation(OpacityProperty, fadeInExpanded);
    }

    private void CollapseSmooth()
    {
        UninstallMouseHook();

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double targetWidth = _viewModel.WidthCompact;
        double targetHeight = _viewModel.HeightCompact;
        double targetLeft = Math.Round((screenWidth - targetWidth) / 2.0);
        double targetTop = _viewModel.MarginFromScreen;

        if (!_viewModel.EnableAnimations)
        {
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(HeightProperty, null);

            Width = targetWidth;
            Left = targetLeft;
            Height = targetHeight;
            Top = targetTop;

            ExpandedContentGrid.Visibility = Visibility.Collapsed;
            ExpandedContentGrid.Opacity = 0;
            CompactContentGrid.Visibility = Visibility.Visible;
            CompactContentGrid.Opacity = 1;
            return;
        }

        _isAnimating = true;
        var duration = TimeSpan.FromMilliseconds(240);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // 1. Simultaneous Width, Left, and Height Animations
        var widthAnim = new DoubleAnimation(Width, targetWidth, duration) { EasingFunction = ease };
        var leftAnim = new DoubleAnimation(Left, targetLeft, duration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(Height, targetHeight, duration) { EasingFunction = ease };

        heightAnim.Completed += (_, _) =>
        {
            _isAnimating = false;
            Width = targetWidth;
            Left = targetLeft;
            Height = targetHeight;
            Top = targetTop;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(HeightProperty, null);
        };

        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(HeightProperty, heightAnim);

        // 2. Crossfade Content Storyboard
        var fadeOutExpanded = new DoubleAnimation(ExpandedContentGrid.Opacity, 0, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = ease
        };
        fadeOutExpanded.Completed += (_, _) => ExpandedContentGrid.Visibility = Visibility.Collapsed;
        ExpandedContentGrid.BeginAnimation(OpacityProperty, fadeOutExpanded);

        CompactContentGrid.Visibility = Visibility.Visible;
        var fadeInCompact = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            BeginTime = TimeSpan.FromMilliseconds(50),
            EasingFunction = ease
        };
        CompactContentGrid.BeginAnimation(OpacityProperty, fadeInCompact);
    }

    private void InstallMouseHook()
    {
        if (_mouseHookHandle == IntPtr.Zero)
        {
            _mouseHookHandle = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc, IntPtr.Zero, 0);
        }
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }
    }

    private IntPtr LowLevelMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == User32.WM_LBUTTONDOWN || msg == User32.WM_RBUTTONDOWN || msg == User32.WM_NCLBUTTONDOWN)
            {
                if (User32.GetCursorPos(out var pt))
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (!_viewModel.IsExpanded) return;

                        try
                        {
                            var clientPt = PointFromScreen(new Point(pt.X, pt.Y));
                            bool isInside = clientPt.X >= 0 && clientPt.X <= ActualWidth &&
                                            clientPt.Y >= 0 && clientPt.Y <= ActualHeight;

                            if (!isInside)
                            {
                                _viewModel.Collapse();
                            }
                        }
                        catch
                        {
                            // In case of coordinate transform glitch during window close
                        }
                    });
                }
            }
        }

        return User32.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }
}
